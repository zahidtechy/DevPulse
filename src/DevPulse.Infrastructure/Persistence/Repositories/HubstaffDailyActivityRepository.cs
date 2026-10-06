namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class HubstaffDailyActivityRepository : IHubstaffDailyActivityRepository
{
    private readonly DevPulseDbContext _dbContext;

    public HubstaffDailyActivityRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertRangeAsync(IReadOnlyList<HubstaffDailyActivity> activities, CancellationToken cancellationToken = default)
    {
        if (activities.Count == 0)
        {
            return;
        }

        var orgIds = activities.Select(a => a.HubstaffOrganizationId).Distinct().ToList();
        var hubstaffIds = activities.Select(a => a.HubstaffDailyActivityId).Distinct().ToList();

        var existing = await _dbContext.HubstaffDailyActivities
            .Where(a => orgIds.Contains(a.HubstaffOrganizationId) && hubstaffIds.Contains(a.HubstaffDailyActivityId))
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(a => (a.HubstaffOrganizationId, a.HubstaffDailyActivityId));

        foreach (var incoming in activities)
        {
            var key = (incoming.HubstaffOrganizationId, incoming.HubstaffDailyActivityId);
            if (existingByKey.TryGetValue(key, out var current))
            {
                current.WorkDate = incoming.WorkDate;
                current.HubstaffUserId = incoming.HubstaffUserId;
                current.DeveloperId = incoming.DeveloperId;
                current.ProjectId = incoming.ProjectId;
                current.ProjectName = incoming.ProjectName;
                current.TaskId = incoming.TaskId;
                current.HubstaffUserEmail = incoming.HubstaffUserEmail;
                current.TrackedSeconds = incoming.TrackedSeconds;
                current.BillableSeconds = incoming.BillableSeconds;
                current.IdleSeconds = incoming.IdleSeconds;
                current.ManualSeconds = incoming.ManualSeconds;
                current.InputTrackedSeconds = incoming.InputTrackedSeconds;
                current.OverallActiveSeconds = incoming.OverallActiveSeconds;
                current.HubstaffUpdatedAtUtc = incoming.HubstaffUpdatedAtUtc;
                current.SyncedAtUtc = incoming.SyncedAtUtc;
            }
            else
            {
                _dbContext.HubstaffDailyActivities.Add(incoming);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestSyncedAtAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffDailyActivities
            .OrderByDescending(a => a.SyncedAtUtc)
            .Select(a => (DateTime?)a.SyncedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HubstaffDailyActivity>> GetForAnalyticsAsync(
        Guid hubstaffOrganizationId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<Guid>? developerIds,
        bool includeUnmapped,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.HubstaffDailyActivities
            .AsNoTracking()
            .Where(a => a.HubstaffOrganizationId == hubstaffOrganizationId)
            .Where(a => a.WorkDate >= fromDate && a.WorkDate <= toDate);

        if (developerIds is { Count: > 0 })
        {
            query = includeUnmapped
                ? query.Where(a => a.DeveloperId == null || developerIds.Contains(a.DeveloperId.Value))
                : query.Where(a => a.DeveloperId != null && developerIds.Contains(a.DeveloperId.Value));
        }
        else if (!includeUnmapped)
        {
            query = query.Where(a => a.DeveloperId != null);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<(int Total, int Mapped)> GetCountsForPeriodAsync(
        Guid hubstaffOrganizationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbContext.HubstaffDailyActivities
            .AsNoTracking()
            .Where(a => a.HubstaffOrganizationId == hubstaffOrganizationId)
            .Where(a => a.WorkDate >= fromDate && a.WorkDate <= toDate);

        var total = await baseQuery.CountAsync(cancellationToken);
        var mapped = await baseQuery.Where(a => a.DeveloperId != null).CountAsync(cancellationToken);
        return (total, mapped);
    }

    public async Task<int> BackfillDeveloperIdsAsync(
        Guid hubstaffOrganizationId,
        IReadOnlyDictionary<int, Guid> userIdToDeveloper,
        CancellationToken cancellationToken = default)
    {
        if (userIdToDeveloper.Count == 0)
        {
            return 0;
        }

        var hubstaffUserIds = userIdToDeveloper.Keys.ToList();
        var activities = await _dbContext.HubstaffDailyActivities
            .Where(a => a.HubstaffOrganizationId == hubstaffOrganizationId)
            .Where(a => a.DeveloperId == null && hubstaffUserIds.Contains(a.HubstaffUserId))
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var activity in activities)
        {
            if (userIdToDeveloper.TryGetValue(activity.HubstaffUserId, out var developerId))
            {
                activity.DeveloperId = developerId;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }
}
