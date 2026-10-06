namespace DevPulse.Application.Abstractions.Analytics;

public interface IKpiSyncService
{
    Task<Result<KpiSyncResultDto>> SyncAsync(bool triggeredManually = false, CancellationToken cancellationToken = default);

    Task<KpiSyncStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);
}

public interface ICachedAnalyticsService
{
    Task<Result<CachedAnalyticsResponse>> GetAnalyticsFromDatabaseAsync(
        CachedAnalyticsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<DeveloperReportResponse>> GenerateReportFromDatabaseAsync(
        DeveloperReportRequest request,
        CancellationToken cancellationToken = default);
}
