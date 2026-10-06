namespace DevPulse.Shared.Contracts.Hubstaff;

public record HubstaffOrganizationDto(
    Guid Id,
    string Name,
    int OrganizationId,
    string? HubstaffOrganizationName,
    bool IsActive,
    bool HasPatConfigured,
    DateTime? PatExpiresAtUtc,
    DateOnly? LastSyncedToDate,
    DateTime CreatedAtUtc,
    DateTime? LastValidatedAtUtc,
    string? LastValidationMessage);

public record CreateHubstaffOrganizationRequest(
    string Name,
    string PersonalAccessToken,
    int? OrganizationId);

public record UpdateHubstaffOrganizationRequest(
    string Name,
    int OrganizationId,
    string? PersonalAccessToken);

public record UpdateHubstaffOrganizationStatusRequest(bool IsActive);

public record DiscoverHubstaffOrganizationsRequest(string PersonalAccessToken);

public record HubstaffConnectionTestDto(
    Guid OrganizationRecordId,
    string Name,
    bool IsConnected,
    string Status,
    string? HubstaffOrganizationName,
    string Message);

public record HubstaffOrganizationDiscoveryDto(
    int OrganizationId,
    string Name);

public record HubstaffMemberDto(
    int HubstaffUserId,
    string Name,
    string? Email);

public record AddDeveloperHubstaffMappingRequest(
    Guid HubstaffOrganizationId,
    int HubstaffUserId);

public record AddDeveloperHubstaffMappingByEmailRequest(
    Guid HubstaffOrganizationId,
    string Email);

public record DeveloperHubstaffMappingDto(
    Guid Id,
    Guid HubstaffOrganizationId,
    string OrganizationName,
    int HubstaffUserId);

public record SyncFromHubstaffRequest(Guid? HubstaffOrganizationId);

public record SyncFromHubstaffResult(
    int DevelopersCreated,
    int DevelopersUpdated,
    int MappingsAdded);
