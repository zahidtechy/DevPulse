namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class KpiSyncRunRepository : IKpiSyncRunRepository
{
    private readonly DevPulseDbContext _dbContext;

    public KpiSyncRunRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(KpiSyncRun run, CancellationToken cancellationToken = default)
    {
        _dbContext.KpiSyncRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(KpiSyncRun run, CancellationToken cancellationToken = default)
    {
        _dbContext.KpiSyncRuns.Update(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<KpiSyncRun?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.KpiSyncRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddSnapshotsAsync(IReadOnlyList<DeveloperKpiSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        _dbContext.DeveloperKpiSnapshots.AddRange(snapshots);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSnapshotsForPeriodAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeveloperKpiSnapshots
            .Where(s => s.FromDate == fromDate && s.ToDate == toDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
