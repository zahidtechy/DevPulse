namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class TaskAssignmentPeriodRepository : ITaskAssignmentPeriodRepository
{
    private readonly DevPulseDbContext _dbContext;

    public TaskAssignmentPeriodRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ApplyCurrentAssigneesAsync(
        IReadOnlyList<TaskCurrentAssignee> currentAssignees,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (currentAssignees.Count == 0)
        {
            return;
        }

        var accountIds = currentAssignees.Select(a => a.AccountId).Distinct().ToList();
        var taskIds = currentAssignees.Select(a => a.TaskId).Distinct().ToList();

        var openPeriods = await _dbContext.TaskAssignmentPeriods
            .Where(p => p.UnassignedAtUtc == null
                && accountIds.Contains(p.AccountId)
                && taskIds.Contains(p.TaskId))
            .ToListAsync(cancellationToken);

        var allPeriodsForTasks = await _dbContext.TaskAssignmentPeriods
            .Where(p => accountIds.Contains(p.AccountId) && taskIds.Contains(p.TaskId))
            .Select(p => new { p.AccountId, p.TaskId, p.DeveloperId })
            .ToListAsync(cancellationToken);

        var hadAnyPeriod = allPeriodsForTasks
            .Select(p => (p.AccountId, p.TaskId, p.DeveloperId))
            .ToHashSet();

        var currentByTask = currentAssignees
            .GroupBy(a => (a.AccountId, a.TaskId))
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => a.DeveloperId).ToHashSet());

        var dateCreatedByTask = currentAssignees
            .GroupBy(a => (a.AccountId, a.TaskId))
            .ToDictionary(g => g.Key, g => g.First().DateCreatedMs);

        var openByTask = openPeriods
            .GroupBy(p => (p.AccountId, p.TaskId))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (taskKey, currentDeveloperIds) in currentByTask)
        {
            openByTask.TryGetValue(taskKey, out var opens);
            opens ??= [];

            foreach (var period in opens)
            {
                if (!currentDeveloperIds.Contains(period.DeveloperId))
                {
                    var closeAt = syncedAtUtc <= period.AssignedAtUtc
                        ? period.AssignedAtUtc.AddSeconds(1)
                        : syncedAtUtc;
                    period.UnassignedAtUtc = closeAt;
                }
            }

            var openDeveloperIds = opens
                .Where(p => p.UnassignedAtUtc == null)
                .Select(p => p.DeveloperId)
                .ToHashSet();

            foreach (var developerId in currentDeveloperIds)
            {
                if (openDeveloperIds.Contains(developerId))
                {
                    continue;
                }

                var assignedAt = hadAnyPeriod.Contains((taskKey.AccountId, taskKey.TaskId, developerId))
                    ? syncedAtUtc
                    : ToUtcOrDefault(dateCreatedByTask.GetValueOrDefault(taskKey), syncedAtUtc);

                if (assignedAt > syncedAtUtc)
                {
                    assignedAt = syncedAtUtc;
                }

                _dbContext.TaskAssignmentPeriods.Add(new TaskAssignmentPeriod
                {
                    AccountId = taskKey.AccountId,
                    TaskId = taskKey.TaskId,
                    DeveloperId = developerId,
                    AssignedAtUtc = assignedAt,
                    UnassignedAtUtc = null
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InsertIfMissingAsync(
        IReadOnlyList<TaskAssignmentPeriod> periods,
        CancellationToken cancellationToken = default)
    {
        if (periods.Count == 0)
        {
            return;
        }

        var accountIds = periods.Select(p => p.AccountId).Distinct().ToList();
        var taskIds = periods.Select(p => p.TaskId).Distinct().ToList();
        var developerIds = periods.Select(p => p.DeveloperId).Distinct().ToList();

        var existing = await _dbContext.TaskAssignmentPeriods
            .Where(p => accountIds.Contains(p.AccountId)
                && taskIds.Contains(p.TaskId)
                && developerIds.Contains(p.DeveloperId))
            .Select(p => new { p.AccountId, p.TaskId, p.DeveloperId, p.AssignedAtUtc, p.UnassignedAtUtc })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(p => (p.AccountId, p.TaskId, p.DeveloperId, p.AssignedAtUtc, p.UnassignedAtUtc))
            .ToHashSet();

        foreach (var period in periods)
        {
            var key = (period.AccountId, period.TaskId, period.DeveloperId, period.AssignedAtUtc, period.UnassignedAtUtc);
            if (existingKeys.Contains(key))
            {
                continue;
            }

            var alreadyHasMatchingWindow = existing.Any(p =>
                p.AccountId == period.AccountId
                && p.TaskId == period.TaskId
                && p.DeveloperId == period.DeveloperId
                && p.AssignedAtUtc == period.AssignedAtUtc
                && p.UnassignedAtUtc == period.UnassignedAtUtc);

            if (alreadyHasMatchingWindow)
            {
                continue;
            }

            var hasSameOpen = period.UnassignedAtUtc is null
                && existing.Any(p =>
                    p.AccountId == period.AccountId
                    && p.TaskId == period.TaskId
                    && p.DeveloperId == period.DeveloperId
                    && p.UnassignedAtUtc == null);

            if (hasSameOpen)
            {
                continue;
            }

            _dbContext.TaskAssignmentPeriods.Add(period);
            existingKeys.Add(key);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AdjustOpenPeriodStartAsync(
        Guid accountId,
        string taskId,
        Guid developerId,
        DateTime assignedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var open = await _dbContext.TaskAssignmentPeriods
            .FirstOrDefaultAsync(
                p => p.AccountId == accountId
                    && p.TaskId == taskId
                    && p.DeveloperId == developerId
                    && p.UnassignedAtUtc == null,
                cancellationToken);

        if (open is null || open.AssignedAtUtc == assignedAtUtc)
        {
            return;
        }

        open.AssignedAtUtc = assignedAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskAssignmentPeriod>> GetOverlappingAsync(
        IReadOnlyList<Guid> developerIds,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        IReadOnlyList<Guid>? accountIds = null,
        CancellationToken cancellationToken = default)
    {
        if (developerIds.Count == 0)
        {
            return [];
        }

        var query = _dbContext.TaskAssignmentPeriods
            .AsNoTracking()
            .Where(p => developerIds.Contains(p.DeveloperId)
                && p.AssignedAtUtc < rangeEndExclusiveUtc
                && (p.UnassignedAtUtc == null || p.UnassignedAtUtc > rangeStartUtc));

        if (accountIds is { Count: > 0 })
        {
            query = query.Where(p => accountIds.Contains(p.AccountId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    private static DateTime ToUtcOrDefault(long? unixMs, DateTime fallback)
    {
        if (!unixMs.HasValue || unixMs.Value <= 0)
        {
            return fallback;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs.Value).UtcDateTime;
    }
}
