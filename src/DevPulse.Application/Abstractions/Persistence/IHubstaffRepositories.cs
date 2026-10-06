namespace DevPulse.Application.Abstractions.Persistence;

public interface IHubstaffOrganizationRepository
{
    Task<IReadOnlyList<HubstaffOrganization>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HubstaffOrganization>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<HubstaffOrganization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByOrganizationIdAsync(int organizationId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> HasDailyActivitiesAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(HubstaffOrganization organization, CancellationToken cancellationToken = default);

    Task UpdateAsync(HubstaffOrganization organization, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IHubstaffDailyActivityRepository
{
    Task UpsertRangeAsync(IReadOnlyList<HubstaffDailyActivity> activities, CancellationToken cancellationToken = default);

    Task<DateTime?> GetLatestSyncedAtAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HubstaffDailyActivity>> GetForAnalyticsAsync(
        Guid hubstaffOrganizationId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<Guid>? developerIds,
        bool includeUnmapped,
        CancellationToken cancellationToken = default);

    Task<(int Total, int Mapped)> GetCountsForPeriodAsync(
        Guid hubstaffOrganizationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<int> BackfillDeveloperIdsAsync(
        Guid hubstaffOrganizationId,
        IReadOnlyDictionary<int, Guid> userIdToDeveloper,
        CancellationToken cancellationToken = default);
}

public interface IHubstaffSyncRunRepository
{
    Task AddAsync(HubstaffSyncRun run, CancellationToken cancellationToken = default);

    Task UpdateAsync(HubstaffSyncRun run, CancellationToken cancellationToken = default);

    Task<HubstaffSyncRun?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<HubstaffSyncRun?> GetRunningAsync(CancellationToken cancellationToken = default);
}
