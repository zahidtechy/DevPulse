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
            existing.OfficeTimeZoneId = settings.OfficeTimeZoneId;
            existing.UpdatedAtUtc = settings.UpdatedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceRecord?> GetRecordAsync(Guid developerId, DateOnly workDate, CancellationToken cancellationToken = default) =>
        await QueryRecords()
            .FirstOrDefaultAsync(x => x.DeveloperId == developerId && x.WorkDate == workDate, cancellationToken);

    public async Task<AttendanceRecord?> GetRecordByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await QueryRecords().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecord>> GetRecordsByDeveloperAsync(
        Guid developerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default) =>
        await QueryRecords()
            .Where(x => x.DeveloperId == developerId && x.WorkDate >= fromDate && x.WorkDate <= toDate)
            .OrderByDescending(x => x.WorkDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

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

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsByDeveloperAsync(
        Guid developerId,
        CancellationToken cancellationToken = default) =>
        await QueryCorrectionRequests()
            .Where(x => x.DeveloperId == developerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetPendingCorrectionRequestsAsync(
        CancellationToken cancellationToken = default) =>
        await QueryCorrectionRequests()
            .Where(x => x.Status == AttendanceCorrectionStatus.Pending)
            .OrderBy(x => x.WorkDate)
            .ThenBy(x => x.Developer!.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

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
