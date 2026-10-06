namespace DevPulse.Application.Abstractions.Persistence;

public interface ISyncedTaskRepository
{
    Task UpsertRangeAsync(IReadOnlyList<SyncedTask> tasks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncedTask>> GetByAccountAndTaskIdsAsync(
        IReadOnlyList<Guid> accountIds,
        IReadOnlyList<string> taskIds,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetLatestSyncedAtAsync(CancellationToken cancellationToken = default);
}
