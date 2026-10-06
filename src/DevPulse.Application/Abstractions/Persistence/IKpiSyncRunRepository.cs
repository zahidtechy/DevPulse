namespace DevPulse.Application.Abstractions.Persistence;

public interface IKpiSyncRunRepository
{
    Task AddAsync(KpiSyncRun run, CancellationToken cancellationToken = default);

    Task UpdateAsync(KpiSyncRun run, CancellationToken cancellationToken = default);

    Task<KpiSyncRun?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task AddSnapshotsAsync(IReadOnlyList<DeveloperKpiSnapshot> snapshots, CancellationToken cancellationToken = default);

    Task DeleteSnapshotsForPeriodAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
