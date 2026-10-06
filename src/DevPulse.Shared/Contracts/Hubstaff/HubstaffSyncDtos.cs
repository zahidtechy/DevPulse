namespace DevPulse.Shared.Contracts.Hubstaff;

public record HubstaffSyncStatusDto(
    HubstaffSyncRunDto? LatestRun,
    DateTime? LastSyncedAtUtc,
    IReadOnlyList<HubstaffOrganizationSyncSummaryDto> Organizations);

public record HubstaffOrganizationSyncSummaryDto(
    Guid Id,
    string Name,
    DateOnly? LastSyncedToDate,
    DateTime? PatExpiresAtUtc,
    bool PatExpiringSoon);

public record HubstaffSyncRunDto(
    Guid Id,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateOnly FromDate,
    DateOnly ToDate,
    string Status,
    int ActivitiesFetched,
    int ActivitiesUpserted,
    int UnmappedUsersSkipped,
    int OrganizationCount,
    string? ErrorMessage,
    bool TriggeredManually);

public record HubstaffSyncTriggerRequest(
    Guid? OrganizationId,
    DateOnly? FromDate,
    DateOnly? ToDate);

public record HubstaffSyncResultDto(string Message);

public record HubstaffAnalyticsRequest(
    Guid HubstaffOrganizationId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<Guid>? DeveloperIds,
    bool IncludeUnmapped = false);

public record HubstaffAnalyticsResponse(
    HubstaffAnalyticsSummaryDto Summary,
    IReadOnlyList<HubstaffDeveloperHoursDto> ByDeveloper,
    IReadOnlyList<HubstaffProjectHoursDto> ByProject,
    IReadOnlyList<HubstaffDailyTrendDto> DailyTrend,
    DateTime? LastSyncedAtUtc,
    DateOnly? LastSyncedToDate,
    HubstaffSyncRunDto? LatestSyncRun,
    bool IsStale);

public record HubstaffAnalyticsSummaryDto(
    decimal TotalTrackedHours,
    decimal TotalBillableHours,
    decimal TotalIdleHours,
    decimal TotalManualHours,
    decimal AverageHoursPerDeveloperPerDay,
    decimal BillableRatio,
    decimal IdleRatio);

public record HubstaffDeveloperHoursDto(
    Guid? DeveloperId,
    string Name,
    decimal TrackedHours,
    decimal BillableHours,
    decimal IdleHours,
    decimal ManualHours,
    int ActiveDays,
    decimal AverageHoursPerDay);

public record HubstaffProjectHoursDto(
    int ProjectId,
    string? ProjectName,
    decimal TrackedHours,
    int DeveloperCount);

public record HubstaffDailyTrendDto(
    DateOnly Date,
    decimal TrackedHours,
    decimal BillableHours,
    decimal IdleHours);
