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

    public async Task<IReadOnlyList<AttendanceDaySourceRow>> GetAttendanceDaySourceRowsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default)
    {
        var records = _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate);
        var corrections = _dbContext.AttendanceCorrectionRequests
            .AsNoTracking()
            .Where(x => x.WorkDate >= fromDate && x.WorkDate <= toDate);

        if (developerId.HasValue)
        {
            records = records.Where(x => x.DeveloperId == developerId.Value);
            corrections = corrections.Where(x => x.DeveloperId == developerId.Value);
        }

        // Keep the set operation over server-translatable scalar projections. EF Core
        // treats the positional read-model constructor as a client projection, so doing
        // that before Concat prevents UNION ALL translation.
        var recordRows = records.Select(x => new
        {
            x.DeveloperId,
            DeveloperName = x.Developer.Name,
            x.WorkDate,
            HasRecord = true,
            x.PunchInUtc,
            x.PunchOutUtc,
            x.PunchInIsCorrected,
            x.PunchOutIsCorrected,
            x.PunchInCorrectedAtUtc,
            x.PunchOutCorrectedAtUtc,
            CorrectionRequestId = (Guid?)null,
            CorrectionStatus = (AttendanceCorrectionStatus?)null,
            RequestedPunchInUtc = (DateTime?)null,
            RequestedPunchOutUtc = (DateTime?)null,
            CorrectionCreatedAtUtc = (DateTime?)null
        });

        var correctionRows = corrections.Select(x => new
        {
            x.DeveloperId,
            DeveloperName = x.Developer.Name,
            x.WorkDate,
            HasRecord = false,
            PunchInUtc = (DateTime?)null,
            PunchOutUtc = (DateTime?)null,
            PunchInIsCorrected = false,
            PunchOutIsCorrected = false,
            PunchInCorrectedAtUtc = (DateTime?)null,
            PunchOutCorrectedAtUtc = (DateTime?)null,
            CorrectionRequestId = (Guid?)x.Id,
            CorrectionStatus = (AttendanceCorrectionStatus?)x.Status,
            x.RequestedPunchInUtc,
            x.RequestedPunchOutUtc,
            CorrectionCreatedAtUtc = (DateTime?)x.CreatedAtUtc
        });

        var rows = await recordRows
            .Concat(correctionRows)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new AttendanceDaySourceRow(
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
}
