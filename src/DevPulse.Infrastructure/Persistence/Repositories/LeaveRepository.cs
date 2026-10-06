namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class LeaveRepository : ILeaveRepository
{
    private readonly DevPulseDbContext _dbContext;

    public LeaveRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LeaveType>> GetAllLeaveTypesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.LeaveTypes
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveType>> GetActiveLeaveTypesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.LeaveTypes
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<LeaveType?> GetLeaveTypeByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        _dbContext.LeaveTypes.Add(leaveType);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default)
    {
        _dbContext.LeaveTypes.Update(leaveType);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var leaveType = await _dbContext.LeaveTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (leaveType is null)
        {
            return;
        }

        var inUse = await _dbContext.LeaveApplications.AnyAsync(x => x.LeaveTypeId == id, cancellationToken);
        if (inUse)
        {
            throw new InvalidOperationException("Cannot delete a leave type that has applications.");
        }

        _dbContext.LeaveTypes.Remove(leaveType);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeaveApplication?> GetApplicationByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await QueryApplications()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LeaveApplication>> GetApplicationsByApplicantAsync(
        Guid developerId,
        CancellationToken cancellationToken = default) =>
        await QueryApplications()
            .Where(x => x.ApplicantDeveloperId == developerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveApplication>> GetApplicationsByApproverAsync(
        Guid approverDeveloperId,
        LeaveApplicationStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = QueryApplications().Where(x => x.ApproverDeveloperId == approverDeveloperId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveApplication>> GetAllApplicationsAsync(CancellationToken cancellationToken = default) =>
        await QueryApplications()
            .OrderByDescending(x => x.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LeaveApplication>> GetApplicationsForBalanceAsync(
        Guid developerId,
        Guid leaveTypeId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        return await QueryApplications()
            .Where(x =>
                x.ApplicantDeveloperId == developerId
                && x.LeaveTypeId == leaveTypeId
                && x.FromDate <= yearEnd
                && x.ToDate >= yearStart
                && (x.Status == LeaveApplicationStatus.Approved || x.Status == LeaveApplicationStatus.Pending))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveApplication>> GetApplicationsForTeamBalanceAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        return await QueryApplications()
            .Where(x =>
                x.FromDate <= yearEnd
                && x.ToDate >= yearStart
                && (x.Status == LeaveApplicationStatus.Approved || x.Status == LeaveApplicationStatus.Pending))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasOverlappingLeaveAsync(
        Guid applicantDeveloperId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? excludeApplicationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.LeaveApplications
            .Where(x =>
                x.ApplicantDeveloperId == applicantDeveloperId
                && x.FromDate <= toDate
                && x.ToDate >= fromDate
                && (x.Status == LeaveApplicationStatus.Pending || x.Status == LeaveApplicationStatus.Approved));

        if (excludeApplicationId.HasValue)
        {
            query = query.Where(x => x.Id != excludeApplicationId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddApplicationAsync(LeaveApplication application, CancellationToken cancellationToken = default)
    {
        _dbContext.LeaveApplications.Add(application);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateApplicationAsync(LeaveApplication application, CancellationToken cancellationToken = default)
    {
        _dbContext.LeaveApplications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeaveSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.LeaveSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new LeaveSettings();
        _dbContext.LeaveSettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task UpdateSettingsAsync(LeaveSettings settings, CancellationToken cancellationToken = default)
    {
        settings.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.LeaveSettings.Update(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<LeaveApplication> QueryApplications() =>
        _dbContext.LeaveApplications
            .Include(x => x.ApplicantDeveloper)
            .Include(x => x.ApproverDeveloper)
            .Include(x => x.LeaveType);
}
