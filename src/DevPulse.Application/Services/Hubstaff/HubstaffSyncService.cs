namespace DevPulse.Application.Services.Hubstaff;

public sealed class HubstaffSyncService : IHubstaffSyncService
{
    private readonly IHubstaffOrganizationRepository _organizationRepository;
    private readonly IHubstaffDailyActivityRepository _activityRepository;
    private readonly IHubstaffSyncRunRepository _syncRunRepository;
    private readonly IDeveloperRepository _developerRepository;
    private readonly IHubstaffTokenProvider _tokenProvider;
    private readonly IHubstaffApiClient _apiClient;
    private readonly HubstaffSyncOptions _options;
    private readonly ILogger<HubstaffSyncService> _logger;

    public HubstaffSyncService(
        IHubstaffOrganizationRepository organizationRepository,
        IHubstaffDailyActivityRepository activityRepository,
        IHubstaffSyncRunRepository syncRunRepository,
        IDeveloperRepository developerRepository,
        IHubstaffTokenProvider tokenProvider,
        IHubstaffApiClient apiClient,
        IOptions<HubstaffSyncOptions> options,
        ILogger<HubstaffSyncService> logger)
    {
        _organizationRepository = organizationRepository;
        _activityRepository = activityRepository;
        _syncRunRepository = syncRunRepository;
        _developerRepository = developerRepository;
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HubstaffSyncStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var latestRun = await _syncRunRepository.GetLatestAsync(cancellationToken);
        var lastSyncedAt = await _activityRepository.GetLatestSyncedAtAsync(cancellationToken);
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

        var summaries = organizations
            .Select(o => new HubstaffOrganizationSyncSummaryDto(
                o.Id,
                o.Name,
                o.LastSyncedToDate,
                o.PatExpiresAtUtc,
                o.PatExpiresAtUtc.HasValue && o.PatExpiresAtUtc <= DateTime.UtcNow.AddDays(14)))
            .ToList();

        return new HubstaffSyncStatusDto(
            latestRun is null ? null : ToDto(latestRun),
            lastSyncedAt,
            summaries);
    }

    public async Task<Result<HubstaffSyncResultDto>> SyncAsync(
        bool triggeredManually = false,
        HubstaffSyncTriggerRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (await _syncRunRepository.GetRunningAsync(cancellationToken) is not null)
        {
            return Result<HubstaffSyncResultDto>.Failure("A Hubstaff sync is already running.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var organizations = await _organizationRepository.GetActiveAsync(cancellationToken);
        if (request?.OrganizationId is Guid orgFilter)
        {
            organizations = organizations.Where(o => o.Id == orgFilter).ToList();
        }

        if (organizations.Count == 0)
        {
            return Result<HubstaffSyncResultDto>.Failure("No active Hubstaff organizations configured.");
        }

        var developers = await _developerRepository.GetAllWithHubstaffMappingsAsync(cancellationToken);
        var run = new HubstaffSyncRun
        {
            FromDate = request?.FromDate ?? today.AddDays(-(_options.LookbackDays - 1)),
            ToDate = request?.ToDate ?? today,
            Status = HubstaffSyncRunStatus.Running,
            TriggeredManually = triggeredManually,
            OrganizationCount = organizations.Count,
            StartedAtUtc = DateTime.UtcNow
        };

        await _syncRunRepository.AddAsync(run, cancellationToken);

        var totalFetched = 0;
        var totalUpserted = 0;
        var totalUnmapped = 0;

        try
        {
            foreach (var organization in organizations)
            {
                var (fromDate, toDate) = ResolveSyncWindow(organization, request, today);
                run.FromDate = DateOnly.FromDayNumber(Math.Min(run.FromDate.DayNumber, fromDate.DayNumber));
                run.ToDate = DateOnly.FromDayNumber(Math.Max(run.ToDate.DayNumber, toDate.DayNumber));

                var mappings = developers
                    .SelectMany(d => d.HubstaffMappings.Where(m => m.HubstaffOrganizationId == organization.Id))
                    .ToList();

                var userIdToDeveloper = mappings.ToDictionary(m => m.HubstaffUserId, m => m.DeveloperId);
                var userIds = userIdToDeveloper.Keys.ToList();

                var accessToken = await _tokenProvider.GetAccessTokenAsync(organization.Id, cancellationToken);
                var syncedAt = DateTime.UtcNow;

                foreach (var (chunkFrom, chunkTo) in ChunkDateRange(fromDate, toDate, _options.DateChunkDays))
                {
                    int? pageStartId = null;
                    do
                    {
                        var page = await _apiClient.GetDailyActivitiesAsync(
                            organization.OrganizationId,
                            chunkFrom,
                            chunkTo,
                            accessToken,
                            userIds.Count > 0 ? userIds : null,
                            pageStartId,
                            cancellationToken);

                        totalFetched += page.Activities.Count;

                        var entities = page.Activities
                            .Select(a =>
                            {
                                Guid? developerId = userIdToDeveloper.TryGetValue(a.UserId, out var devId)
                                    ? devId
                                    : null;

                                if (developerId is null)
                                {
                                    totalUnmapped++;
                                }

                                return new HubstaffDailyActivity
                                {
                                    HubstaffOrganizationId = organization.Id,
                                    HubstaffDailyActivityId = a.Id,
                                    WorkDate = a.Date,
                                    HubstaffUserId = a.UserId,
                                    DeveloperId = developerId,
                                    ProjectId = a.ProjectId,
                                    ProjectName = a.ProjectName,
                                    TaskId = a.TaskId,
                                    HubstaffUserEmail = a.UserEmail,
                                    TrackedSeconds = a.TrackedSeconds,
                                    BillableSeconds = a.BillableSeconds,
                                    IdleSeconds = a.IdleSeconds,
                                    ManualSeconds = a.ManualSeconds,
                                    InputTrackedSeconds = a.InputTrackedSeconds,
                                    OverallActiveSeconds = a.OverallActiveSeconds,
                                    HubstaffUpdatedAtUtc = a.UpdatedAtUtc,
                                    SyncedAtUtc = syncedAt
                                };
                            })
                            .ToList();

                        if (entities.Count > 0)
                        {
                            await _activityRepository.UpsertRangeAsync(entities, cancellationToken);
                            totalUpserted += entities.Count;
                        }

                        pageStartId = page.NextPageStartId;
                    }
                    while (pageStartId.HasValue);
                }

                organization.LastSyncedToDate = toDate;
                organization.LastValidatedAtUtc = DateTime.UtcNow;
                organization.LastValidationMessage = $"Last sync completed at {DateTime.UtcNow:u}.";
                await _organizationRepository.UpdateAsync(organization, cancellationToken);
            }

            run.ActivitiesFetched = totalFetched;
            run.ActivitiesUpserted = totalUpserted;
            run.UnmappedUsersSkipped = totalUnmapped;
            run.Status = HubstaffSyncRunStatus.Succeeded;
            run.CompletedAtUtc = DateTime.UtcNow;
            await _syncRunRepository.UpdateAsync(run, cancellationToken);

            return Result<HubstaffSyncResultDto>.Success(new HubstaffSyncResultDto(
                $"Sync completed: {totalUpserted} activities upserted from {totalFetched} fetched across {organizations.Count} organization(s)."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hubstaff sync failed");
            run.ActivitiesFetched = totalFetched;
            run.ActivitiesUpserted = totalUpserted;
            run.UnmappedUsersSkipped = totalUnmapped;
            run.Status = HubstaffSyncRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAtUtc = DateTime.UtcNow;
            await _syncRunRepository.UpdateAsync(run, cancellationToken);
            return Result<HubstaffSyncResultDto>.Failure(ex.Message);
        }
    }

    internal static HubstaffSyncRunDto ToDto(HubstaffSyncRun run) =>
        new(
            run.Id,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.FromDate,
            run.ToDate,
            run.Status.ToString(),
            run.ActivitiesFetched,
            run.ActivitiesUpserted,
            run.UnmappedUsersSkipped,
            run.OrganizationCount,
            run.ErrorMessage,
            run.TriggeredManually);

    private (DateOnly From, DateOnly To) ResolveSyncWindow(
        HubstaffOrganization organization,
        HubstaffSyncTriggerRequest? request,
        DateOnly today)
    {
        if (request?.FromDate is not null && request.ToDate is not null)
        {
            return (request.FromDate.Value, request.ToDate.Value);
        }

        if (organization.LastSyncedToDate is null)
        {
            var lookback = Math.Clamp(_options.LookbackDays, 1, 365);
            return (today.AddDays(-(lookback - 1)), today);
        }

        var overlap = Math.Clamp(_options.IncrementalOverlapDays, 0, 14);
        var from = organization.LastSyncedToDate.Value.AddDays(-overlap);
        return (from, today);
    }

    private static IEnumerable<(DateOnly From, DateOnly To)> ChunkDateRange(DateOnly from, DateOnly to, int chunkDays)
    {
        var size = Math.Clamp(chunkDays, 1, 90);
        var cursor = from;
        while (cursor <= to)
        {
            var chunkEnd = cursor.AddDays(size - 1);
            if (chunkEnd > to)
            {
                chunkEnd = to;
            }

            yield return (cursor, chunkEnd);
            cursor = chunkEnd.AddDays(1);
        }
    }
}
