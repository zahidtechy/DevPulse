namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly DevPulseDbContext _dbContext;

    public AttendanceRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AttendanceSettings?> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.AttendanceSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

    public async Task EnsureSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.AttendanceSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        _dbContext.AttendanceSettings.Add(new AttendanceSettings());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSettingsAsync(AttendanceSettings settings, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AttendanceSettings.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            _dbContext.AttendanceSettings.Add(settings);
        }
        else
        {
            existing.WorkStartTime = settings.WorkStartTime;
            existing.WorkEndTime = settings.WorkEndTime;
            existing.BufferStartTime = settings.BufferStartTime;
            existing.BufferEndTime = settings.BufferEndTime;
            existing.PunchInAllowMinutesBeforeWorkStart = settings.PunchInAllowMinutesBeforeWorkStart;
            existing.PunchOutAllowMinutesAfterWorkEnd = settings.PunchOutAllowMinutesAfterWorkEnd;
            existing.StrictPunchIn = settings.StrictPunchIn;
            existing.StrictPunchOut = settings.StrictPunchOut;
            existing.OfficeTimeZoneId = settings.OfficeTimeZoneId;
            existing.UpdatedAtUtc = settings.UpdatedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceRecord?> GetRecordAsync(Guid developerId, DateOnly workDate, CancellationToken cancellationToken = default) =>
        await QueryRecords()
            .FirstOrDefaultAsync(x => x.DeveloperId == developerId && x.WorkDate == workDate, cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default)
    {
        var query = QueryRecords()
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate);

        if (developerId.HasValue)
        {
            query = query.Where(x => x.DeveloperId == developerId.Value);
        }

        return await query
            .OrderByDescending(x => x.WorkDate)
            .ThenBy(x => x.Developer!.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceDaySourcePage> GetAttendanceDaySourcePageAsync(
        AttendanceDayPageQuery query,
        CancellationToken cancellationToken = default)
    {
        var records = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(x => x.WorkDate >= query.FromDate && x.WorkDate <= query.ToDate);
        var corrections = _dbContext.AttendanceCorrectionRequests
            .AsNoTracking()
            .Where(x => x.WorkDate >= query.FromDate && x.WorkDate <= query.ToDate);

        if (query.DeveloperId.HasValue)
        {
            records = records.Where(x => x.DeveloperId == query.DeveloperId.Value);
            corrections = corrections.Where(x => x.DeveloperId == query.DeveloperId.Value);
        }

        var representedKeys = records
            .Select(x => new { x.DeveloperId, x.WorkDate })
            .Union(corrections.Select(x => new { x.DeveloperId, x.WorkDate }));

        var logicalRows =
            from key in representedKeys
            join developer in _dbContext.Developers.AsNoTracking()
                on key.DeveloperId equals developer.Id
            join attendanceRecord in records
                on new { key.DeveloperId, key.WorkDate }
                equals new { attendanceRecord.DeveloperId, attendanceRecord.WorkDate }
                into attendanceRecords
            from attendanceRecord in attendanceRecords.DefaultIfEmpty()
            select new
            {
                key.DeveloperId,
                DeveloperName = developer.Name,
                key.WorkDate,
                HasRecord = attendanceRecord != null,
                attendanceRecord.PunchInUtc,
                attendanceRecord.PunchOutUtc,
                HasPendingCorrection = corrections.Any(correction =>
                    correction.DeveloperId == key.DeveloperId
                    && correction.WorkDate == key.WorkDate
                    && correction.Status == AttendanceCorrectionStatus.Pending)
            };

        var filteredRows = logicalRows;
        var bufferStart = query.BufferStartTime.ToTimeSpan();
        var bufferEnd = query.BufferEndTime.ToTimeSpan();
        var sqlTimeZoneId = GetSqlServerTimeZoneId(query.OfficeTimeZoneId);

        switch (query.Status)
        {
            case AttendanceDayStatusFilter.Pending:
                filteredRows = filteredRows.Where(row => row.HasPendingCorrection);
                break;
            case AttendanceDayStatusFilter.OnTime:
                filteredRows = filteredRows.Where(row =>
                    !row.HasPendingCorrection
                    && row.HasRecord
                    && row.PunchInUtc.HasValue
                    && row.PunchOutUtc.HasValue
                    && EF.Functions.AtTimeZone(
                        EF.Functions.AtTimeZone(row.PunchInUtc.Value, "UTC"),
                        sqlTimeZoneId).TimeOfDay <= bufferStart
                    && EF.Functions.AtTimeZone(
                        EF.Functions.AtTimeZone(row.PunchOutUtc.Value, "UTC"),
                        sqlTimeZoneId).TimeOfDay >= bufferEnd);
                break;
            case AttendanceDayStatusFilter.Late:
                filteredRows = filteredRows.Where(row =>
                    !row.HasPendingCorrection
                    && row.HasRecord
                    && row.PunchInUtc.HasValue
                    && row.PunchOutUtc.HasValue
                    && EF.Functions.AtTimeZone(
                        EF.Functions.AtTimeZone(row.PunchInUtc.Value, "UTC"),
                        sqlTimeZoneId).TimeOfDay > bufferStart
                    && EF.Functions.AtTimeZone(
                        EF.Functions.AtTimeZone(row.PunchOutUtc.Value, "UTC"),
                        sqlTimeZoneId).TimeOfDay >= bufferEnd);
                break;
            case AttendanceDayStatusFilter.EarlyLeave:
                filteredRows = filteredRows.Where(row =>
                    !row.HasPendingCorrection
                    && row.HasRecord
                    && row.PunchInUtc.HasValue
                    && row.PunchOutUtc.HasValue
                    && EF.Functions.AtTimeZone(
                        EF.Functions.AtTimeZone(row.PunchOutUtc.Value, "UTC"),
                        sqlTimeZoneId).TimeOfDay < bufferEnd);
                break;
            case AttendanceDayStatusFilter.Incomplete:
                filteredRows = filteredRows.Where(row =>
                    !row.HasPendingCorrection
                    && row.HasRecord
                    && ((row.PunchInUtc.HasValue && !row.PunchOutUtc.HasValue)
                        || (!row.PunchInUtc.HasValue && row.PunchOutUtc.HasValue)));
                break;
            case AttendanceDayStatusFilter.Absent:
                filteredRows = filteredRows.Where(row =>
                    !row.HasPendingCorrection
                    && row.HasRecord
                    && !row.PunchInUtc.HasValue
                    && !row.PunchOutUtc.HasValue);
                break;
            case AttendanceDayStatusFilter.NoRecord:
                filteredRows = filteredRows.Where(row =>
                    !row.HasPendingCorrection
                    && !row.HasRecord);
                break;
        }

        var totalCount = await filteredRows.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var pageNumber = totalPages == 0
            ? 1
            : Math.Min(query.PageNumber, totalPages);

        if (totalCount == 0)
        {
            return new AttendanceDaySourcePage([], pageNumber, query.PageSize, totalCount);
        }

        var pageKeys = filteredRows
            .OrderByDescending(row => row.WorkDate)
            .ThenBy(row => row.DeveloperName)
            .ThenBy(row => row.DeveloperId)
            .Skip((pageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new
            {
                row.DeveloperId,
                row.DeveloperName,
                row.WorkDate
            });

        // Page the distinct logical day keys first, then join both source sets back to
        // those keys. This keeps every correction for a day on the same page.
        var recordRows =
            from record in records
            join key in pageKeys
                on new { record.DeveloperId, record.WorkDate }
                equals new { key.DeveloperId, key.WorkDate }
            select new
            {
                record.DeveloperId,
                key.DeveloperName,
                record.WorkDate,
                HasRecord = true,
                record.PunchInUtc,
                record.PunchOutUtc,
                record.PunchInIsCorrected,
                record.PunchOutIsCorrected,
                record.PunchInCorrectedAtUtc,
                record.PunchOutCorrectedAtUtc,
                CorrectionRequestId = (Guid?)null,
                CorrectionStatus = (AttendanceCorrectionStatus?)null,
                RequestedPunchInUtc = (DateTime?)null,
                RequestedPunchOutUtc = (DateTime?)null,
                CorrectionCreatedAtUtc = (DateTime?)null
            };

        var correctionRows =
            from correction in corrections
            join key in pageKeys
                on new { correction.DeveloperId, correction.WorkDate }
                equals new { key.DeveloperId, key.WorkDate }
            select new
            {
                correction.DeveloperId,
                key.DeveloperName,
                correction.WorkDate,
                HasRecord = false,
                PunchInUtc = (DateTime?)null,
                PunchOutUtc = (DateTime?)null,
                PunchInIsCorrected = false,
                PunchOutIsCorrected = false,
                PunchInCorrectedAtUtc = (DateTime?)null,
                PunchOutCorrectedAtUtc = (DateTime?)null,
                CorrectionRequestId = (Guid?)correction.Id,
                CorrectionStatus = (AttendanceCorrectionStatus?)correction.Status,
                correction.RequestedPunchInUtc,
                correction.RequestedPunchOutUtc,
                CorrectionCreatedAtUtc = (DateTime?)correction.CreatedAtUtc
            };

        // Keep the set operation over server-translatable scalar projections. EF Core
        // treats the positional read-model constructor as a client projection, so doing
        // that before Concat prevents UNION ALL translation.
        var rows = await recordRows
            .Concat(correctionRows)
            .OrderByDescending(row => row.WorkDate)
            .ThenBy(row => row.DeveloperName)
            .ThenBy(row => row.DeveloperId)
            .ThenByDescending(row => row.HasRecord)
            .ThenByDescending(row => row.CorrectionCreatedAtUtc)
            .ToListAsync(cancellationToken);

        var sourceRows = rows.Select(x => new AttendanceDaySourceRow(
                x.DeveloperId,
                x.DeveloperName,
                x.WorkDate,
                x.HasRecord,
                x.PunchInUtc,
                x.PunchOutUtc,
                x.PunchInIsCorrected,
                x.PunchOutIsCorrected,
                x.PunchInCorrectedAtUtc,
                x.PunchOutCorrectedAtUtc,
                x.CorrectionRequestId,
                x.CorrectionStatus,
                x.RequestedPunchInUtc,
                x.RequestedPunchOutUtc,
                x.CorrectionCreatedAtUtc))
            .ToList();

        return new AttendanceDaySourcePage(sourceRows, pageNumber, query.PageSize, totalCount);
    }

    public async Task AddRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.AttendanceRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default)
    {
        _dbContext.AttendanceRecords.Update(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceCorrectionRequest?> GetCorrectionRequestByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await QueryCorrectionRequests()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<AttendanceCorrectionRequest?> GetPendingCorrectionRequestAsync(
        Guid developerId,
        DateOnly workDate,
        CancellationToken cancellationToken = default) =>
        await QueryCorrectionRequests()
            .FirstOrDefaultAsync(
                x => x.DeveloperId == developerId
                     && x.WorkDate == workDate
                     && x.Status == AttendanceCorrectionStatus.Pending,
                cancellationToken);

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default)
    {
        var query = QueryCorrectionRequests()
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate);

        if (developerId.HasValue)
        {
            query = query.Where(x => x.DeveloperId == developerId.Value);
        }

        return await query
            .OrderBy(x => x.Status == AttendanceCorrectionStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<(int TotalCount, int DeveloperCount)> GetPendingCorrectionCountsAsync(
        Guid? developerId,
        bool includeTotal,
        CancellationToken cancellationToken = default)
    {
        var pending = _dbContext.AttendanceCorrectionRequests
            .AsNoTracking()
            .Where(x => x.Status == AttendanceCorrectionStatus.Pending);

        if (!includeTotal)
        {
            if (!developerId.HasValue)
            {
                return (0, 0);
            }

            var developerCount = await pending
                .CountAsync(x => x.DeveloperId == developerId.Value, cancellationToken);
            return (developerCount, developerCount);
        }

        var counts = await pending
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                DeveloperCount = developerId.HasValue
                    ? group.Count(x => x.DeveloperId == developerId.Value)
                    : 0
            })
            .FirstOrDefaultAsync(cancellationToken);

        return counts is null
            ? (0, 0)
            : (counts.TotalCount, counts.DeveloperCount);
    }

    public async Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        _dbContext.AttendanceCorrectionRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        _dbContext.AttendanceCorrectionRequests.Update(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        _dbContext.AttendanceCorrectionRequests.Remove(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AttendanceRecord> QueryRecords() =>
        _dbContext.AttendanceRecords.Include(x => x.Developer);

    private IQueryable<AttendanceCorrectionRequest> QueryCorrectionRequests() =>
        _dbContext.AttendanceCorrectionRequests.Include(x => x.Developer);

    private static string GetSqlServerTimeZoneId(string timeZoneId) =>
        TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsTimeZoneId)
            ? windowsTimeZoneId
            : timeZoneId;
}
