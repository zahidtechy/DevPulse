namespace DevPulse.Application.Services.Hubstaff;

public sealed class HubstaffAnalyticsService : IHubstaffAnalyticsService
{
    private readonly IHubstaffDailyActivityRepository _activityRepository;
    private readonly IHubstaffOrganizationRepository _organizationRepository;
    private readonly IHubstaffSyncRunRepository _syncRunRepository;
    private readonly IDeveloperRepository _developerRepository;

    public HubstaffAnalyticsService(
        IHubstaffDailyActivityRepository activityRepository,
        IHubstaffOrganizationRepository organizationRepository,
        IHubstaffSyncRunRepository syncRunRepository,
        IDeveloperRepository developerRepository)
    {
        _activityRepository = activityRepository;
        _organizationRepository = organizationRepository;
        _syncRunRepository = syncRunRepository;
        _developerRepository = developerRepository;
    }

    public async Task<Result<HubstaffAnalyticsResponse>> GetAnalyticsAsync(
        HubstaffAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FromDate > request.ToDate)
        {
            return Result<HubstaffAnalyticsResponse>.Failure("From date must be on or before to date.");
        }

        var organization = await _organizationRepository.GetByIdAsync(request.HubstaffOrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<HubstaffAnalyticsResponse>.Failure("Hubstaff organization was not found.");
        }

        IReadOnlyList<Guid>? developerIds = request.DeveloperIds;
        if (developerIds is { Count: > 0 })
        {
            var developers = await _developerRepository.GetByIdsAsync(developerIds, cancellationToken);
            developerIds = developers.Select(d => d.Id).ToList();
        }

        var activities = await _activityRepository.GetForAnalyticsAsync(
            request.HubstaffOrganizationId,
            request.FromDate,
            request.ToDate,
            developerIds,
            request.IncludeUnmapped,
            cancellationToken);

        if (activities.Count == 0)
        {
            var message = await BuildNoDataMessageAsync(
                organization,
                request.FromDate,
                request.ToDate,
                cancellationToken);
            return Result<HubstaffAnalyticsResponse>.Failure(message);
        }

        var developerNames = await LoadDeveloperNamesAsync(activities, cancellationToken);
        var lastSyncedAt = activities.Max(a => a.SyncedAtUtc);
        var latestRun = await _syncRunRepository.GetLatestAsync(cancellationToken);
        var isStale = lastSyncedAt < DateTime.UtcNow.AddHours(-48);

        var mappedActivities = activities.Where(a => a.DeveloperId.HasValue || request.IncludeUnmapped).ToList();

        var totalTracked = mappedActivities.Sum(a => a.TrackedSeconds);
        var totalBillable = mappedActivities.Sum(a => a.BillableSeconds);
        var totalIdle = mappedActivities.Sum(a => a.IdleSeconds);
        var totalManual = mappedActivities.Sum(a => a.ManualSeconds);

        var developerGroups = mappedActivities
            .GroupBy(a => a.DeveloperId)
            .Select(g =>
            {
                var name = g.Key.HasValue && developerNames.TryGetValue(g.Key.Value, out var devName)
                    ? devName
                    : g.First().HubstaffUserEmail ?? $"Hubstaff user {g.First().HubstaffUserId}";

                var tracked = g.Sum(x => x.TrackedSeconds);
                var activeDays = g.Select(x => x.WorkDate).Distinct().Count();
                return new HubstaffDeveloperHoursDto(
                    g.Key,
                    name,
                    ToHours(tracked),
                    ToHours(g.Sum(x => x.BillableSeconds)),
                    ToHours(g.Sum(x => x.IdleSeconds)),
                    ToHours(g.Sum(x => x.ManualSeconds)),
                    activeDays,
                    activeDays == 0 ? 0 : Math.Round(ToHours(tracked) / activeDays, 2));
            })
            .OrderByDescending(d => d.TrackedHours)
            .ToList();

        var activeDeveloperCount = developerGroups.Count(d => d.DeveloperId.HasValue);
        var workingDays = mappedActivities.Select(a => a.WorkDate).Distinct().Count();
        var avgHoursPerDevDay = activeDeveloperCount == 0 || workingDays == 0
            ? 0
            : Math.Round(ToHours(totalTracked) / (activeDeveloperCount * workingDays), 2);

        var summary = new HubstaffAnalyticsSummaryDto(
            ToHours(totalTracked),
            ToHours(totalBillable),
            ToHours(totalIdle),
            ToHours(totalManual),
            avgHoursPerDevDay,
            totalTracked == 0 ? 0 : Math.Round((decimal)totalBillable / totalTracked, 4),
            totalTracked == 0 ? 0 : Math.Round((decimal)totalIdle / totalTracked, 4));

        var byProject = mappedActivities
            .GroupBy(a => (a.ProjectId, a.ProjectName))
            .Select(g => new HubstaffProjectHoursDto(
                g.Key.ProjectId,
                g.Key.ProjectName,
                ToHours(g.Sum(x => x.TrackedSeconds)),
                g.Select(x => x.DeveloperId ?? Guid.Empty).Distinct().Count(id => id != Guid.Empty)))
            .OrderByDescending(p => p.TrackedHours)
            .ToList();

        var dailyTrend = mappedActivities
            .GroupBy(a => a.WorkDate)
            .OrderBy(g => g.Key)
            .Select(g => new HubstaffDailyTrendDto(
                g.Key,
                ToHours(g.Sum(x => x.TrackedSeconds)),
                ToHours(g.Sum(x => x.BillableSeconds)),
                ToHours(g.Sum(x => x.IdleSeconds))))
            .ToList();

        return Result<HubstaffAnalyticsResponse>.Success(new HubstaffAnalyticsResponse(
            summary,
            developerGroups,
            byProject,
            dailyTrend,
            lastSyncedAt,
            organization.LastSyncedToDate,
            latestRun is null ? null : HubstaffSyncService.ToDto(latestRun),
            isStale));
    }

    private async Task<Dictionary<Guid, string>> LoadDeveloperNamesAsync(
        IReadOnlyList<HubstaffDailyActivity> activities,
        CancellationToken cancellationToken)
    {
        var ids = activities
            .Where(a => a.DeveloperId.HasValue)
            .Select(a => a.DeveloperId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var developers = await _developerRepository.GetByIdsAsync(ids, cancellationToken);
        return developers.ToDictionary(d => d.Id, d => d.Name);
    }

    private static decimal ToHours(int seconds) => Math.Round(seconds / 3600m, 2);

    private async Task<string> BuildNoDataMessageAsync(
        HubstaffOrganization organization,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (organization.LastSyncedToDate is null)
        {
            return "Hubstaff is connected but activity has not been synced yet. Click Sync now on this page (Admin), then Generate again.";
        }

        var (total, mapped) = await _activityRepository.GetCountsForPeriodAsync(
            organization.Id,
            fromDate,
            toDate,
            cancellationToken);

        if (total > 0 && mapped == 0)
        {
            return "Synced Hubstaff activity exists for this period, but no developers are mapped. Open Developers, click Sync from Hubstaff to map by email, run Sync now here, then Generate again.";
        }

        if (total == 0)
        {
            return "No synced Hubstaff activity data found for the selected period. Try widening the date range or run Sync now to pull the latest tracked hours.";
        }

        return "No synced Hubstaff activity data found for the selected period.";
    }
}
