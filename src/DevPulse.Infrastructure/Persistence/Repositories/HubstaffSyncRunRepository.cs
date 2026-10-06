namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class HubstaffSyncRunRepository : IHubstaffSyncRunRepository
{
    private readonly DevPulseDbContext _dbContext;

    public HubstaffSyncRunRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(HubstaffSyncRun run, CancellationToken cancellationToken = default)
    {
        _dbContext.HubstaffSyncRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(HubstaffSyncRun run, CancellationToken cancellationToken = default)
    {
        _dbContext.HubstaffSyncRuns.Update(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<HubstaffSyncRun?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffSyncRuns
            .OrderByDescending(r => r.StartedAtUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<HubstaffSyncRun?> GetRunningAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffSyncRuns
            .Where(r => r.Status == HubstaffSyncRunStatus.Running)
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
