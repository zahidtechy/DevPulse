namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class SyncedTaskRepository : ISyncedTaskRepository
{
    private readonly DevPulseDbContext _dbContext;

    public SyncedTaskRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertRangeAsync(IReadOnlyList<SyncedTask> tasks, CancellationToken cancellationToken = default)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        var uniqueIncoming = tasks
            .GroupBy(t => (t.AccountId, t.TaskId), t => t)
            .Select(g => g.OrderByDescending(t => t.IsCompleted).ThenByDescending(t => t.SyncedAtUtc).First())
            .ToList();

        var accountIds = uniqueIncoming.Select(t => t.AccountId).Distinct().ToList();
        var taskIds = uniqueIncoming.Select(t => t.TaskId).Distinct().ToList();

        var existing = await _dbContext.SyncedTasks
            .Where(t => accountIds.Contains(t.AccountId) && taskIds.Contains(t.TaskId))
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(
            t => (t.AccountId, t.TaskId),
            t => t);

        foreach (var incoming in uniqueIncoming)
        {
            var key = (incoming.AccountId, incoming.TaskId);
            if (existingByKey.TryGetValue(key, out var current))
            {
                current.AccountName = incoming.AccountName;
                current.ProjectName = incoming.ProjectName;
                current.FolderName = incoming.FolderName;
                current.TaskName = incoming.TaskName;
                current.Status = incoming.Status;
                current.Priority = incoming.Priority;
                current.ListName = incoming.ListName;
                current.Url = incoming.Url;
                current.DateCreated = incoming.DateCreated;
                current.DateDone = incoming.DateDone;
                current.DueDate = incoming.DueDate;
                current.CompletionDays = incoming.CompletionDays;
                current.IsSubtask = incoming.IsSubtask;
                current.ParentTaskId = incoming.ParentTaskId;
                current.ParentTaskName = incoming.ParentTaskName;
                current.TaskType = incoming.TaskType;
                current.IsCompleted = incoming.IsCompleted;
                current.SyncedAtUtc = incoming.SyncedAtUtc;
            }
            else
            {
                _dbContext.SyncedTasks.Add(incoming);
                existingByKey[key] = incoming;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncedTask>> GetByAccountAndTaskIdsAsync(
        IReadOnlyList<Guid> accountIds,
        IReadOnlyList<string> taskIds,
        CancellationToken cancellationToken = default)
    {
        if (accountIds.Count == 0 || taskIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.SyncedTasks
            .AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId) && taskIds.Contains(t.TaskId))
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestSyncedAtAsync(CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.SyncedTasks.AsNoTracking().AnyAsync(cancellationToken))
        {
            return null;
        }

        return await _dbContext.SyncedTasks
            .AsNoTracking()
            .MaxAsync(t => t.SyncedAtUtc, cancellationToken);
    }
}
