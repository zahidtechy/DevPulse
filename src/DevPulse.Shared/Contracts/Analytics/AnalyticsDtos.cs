namespace DevPulse.Shared.Contracts.Analytics;

public record KpiSyncStatusResponse(
    [property: JsonPropertyName("lastRun")] KpiSyncRunDto? LastRun,
    [property: JsonPropertyName("isSyncEnabled")] bool IsSyncEnabled,
    [property: JsonPropertyName("lookbackDays")] int LookbackDays,
    [property: JsonPropertyName("nextScheduledRunUtc")] DateTime? NextScheduledRunUtc);

public record KpiSyncRunDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("startedAtUtc")] DateTime StartedAtUtc,
    [property: JsonPropertyName("completedAtUtc")] DateTime? CompletedAtUtc,
    [property: JsonPropertyName("fromDate")] DateOnly FromDate,
    [property: JsonPropertyName("toDate")] DateOnly ToDate,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("tasksUpserted")] int TasksUpserted,
    [property: JsonPropertyName("developerCount")] int DeveloperCount,
    [property: JsonPropertyName("accountCount")] int AccountCount,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("triggeredManually")] bool TriggeredManually);

public record KpiSyncResultDto(
    [property: JsonPropertyName("run")] KpiSyncRunDto Run,
    [property: JsonPropertyName("message")] string Message);

public record CachedAnalyticsRequest(
    [property: JsonPropertyName("developerIds")] IReadOnlyList<Guid> DeveloperIds,
    [property: JsonPropertyName("fromDate")] DateOnly FromDate,
    [property: JsonPropertyName("toDate")] DateOnly ToDate,
    [property: JsonPropertyName("accountIds")] IReadOnlyList<Guid>? AccountIds = null);

public record CachedAnalyticsResponse(
    [property: JsonPropertyName("report")] DeveloperReportResponse Report,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("syncedAtUtc")] DateTime? SyncedAtUtc,
    [property: JsonPropertyName("lastSyncRun")] KpiSyncRunDto? LastSyncRun);
