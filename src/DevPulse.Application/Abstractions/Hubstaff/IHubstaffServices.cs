namespace DevPulse.Application.Abstractions.Hubstaff;

public interface IHubstaffAuthClient
{
    Task<HubstaffTokenExchangeResult> ExchangeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

public record HubstaffTokenExchangeResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds);

public interface IHubstaffApiClient
{
    Task<IReadOnlyList<HubstaffOrganizationInfo>> GetOrganizationsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HubstaffMemberInfo>> GetMembersAsync(
        int organizationId,
        string accessToken,
        int? pageStartId = null,
        CancellationToken cancellationToken = default);

    Task<HubstaffDailyActivitiesPage> GetDailyActivitiesAsync(
        int organizationId,
        DateOnly fromDate,
        DateOnly toDate,
        string accessToken,
        IReadOnlyList<int>? userIds = null,
        int? pageStartId = null,
        CancellationToken cancellationToken = default);
}

public record HubstaffOrganizationInfo(int OrganizationId, string Name);

public record HubstaffMemberInfo(int UserId, string Name, string? Email);

public record HubstaffDailyActivityInfo(
    long Id,
    DateOnly Date,
    int UserId,
    int ProjectId,
    string? ProjectName,
    int TaskId,
    string? UserEmail,
    int TrackedSeconds,
    int BillableSeconds,
    int IdleSeconds,
    int ManualSeconds,
    int InputTrackedSeconds,
    int OverallActiveSeconds,
    DateTime? UpdatedAtUtc);

public record HubstaffDailyActivitiesPage(
    IReadOnlyList<HubstaffDailyActivityInfo> Activities,
    int? NextPageStartId);

public interface IHubstaffTokenProvider
{
    Task<string> GetAccessTokenAsync(Guid hubstaffOrganizationRecordId, CancellationToken cancellationToken = default);

    Task<HubstaffTokenExchangeResult> ExchangePatAsync(
        string personalAccessToken,
        CancellationToken cancellationToken = default);

    void InvalidateCache(Guid hubstaffOrganizationRecordId);
}

public interface IHubstaffOrganizationService
{
    Task<IReadOnlyList<HubstaffOrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<HubstaffOrganizationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<HubstaffOrganizationDto>> CreateAsync(
        CreateHubstaffOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HubstaffOrganizationDto>> UpdateAsync(
        Guid id,
        UpdateHubstaffOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HubstaffOrganizationDto>> UpdateStatusAsync(
        Guid id,
        UpdateHubstaffOrganizationStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<HubstaffConnectionTestDto>> TestConnectionAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HubstaffOrganizationDiscoveryDto>>> DiscoverOrganizationsAsync(
        DiscoverHubstaffOrganizationsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HubstaffMemberDto>>> GetMembersAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public interface IHubstaffSyncService
{
    Task<Result<HubstaffSyncResultDto>> SyncAsync(
        bool triggeredManually = false,
        HubstaffSyncTriggerRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<HubstaffSyncStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}

public interface IHubstaffAnalyticsService
{
    Task<Result<HubstaffAnalyticsResponse>> GetAnalyticsAsync(
        HubstaffAnalyticsRequest request,
        CancellationToken cancellationToken = default);
}
