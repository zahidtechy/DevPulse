namespace DevPulse.Shared.Contracts.Developers;

using DevPulse.Shared.Contracts.Hubstaff;

public enum WorkRole
{
    Developer = 0,
    QA = 1
}

public record DeveloperDto(
    Guid Id,
    string Name,
    string? Email,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<DeveloperClickUpMappingDto> Mappings,
    WorkRole WorkRole = WorkRole.Developer,
    Guid? ReportingManagerDeveloperId = null,
    string? ReportingManagerName = null,
    IReadOnlyList<DeveloperHubstaffMappingDto>? HubstaffMappings = null);

public record DeveloperClickUpMappingDto(
    Guid Id,
    Guid ClickUpAccountId,
    string AccountName,
    int ClickUpUserId);

public record CreateDeveloperRequest(
    string Name,
    string? Email,
    WorkRole WorkRole = WorkRole.Developer,
    Guid? ReportingManagerDeveloperId = null);

public record UpdateDeveloperRequest(
    string Name,
    string? Email,
    bool IsActive,
    WorkRole WorkRole = WorkRole.Developer,
    Guid? ReportingManagerDeveloperId = null);

public record AddDeveloperMappingRequest(
    Guid ClickUpAccountId,
    int ClickUpUserId);

public record AddDeveloperMappingByEmailRequest(
    string WorkspaceId,
    string? Email = null);

public record SyncDevelopersResult(
    int DevelopersCreated,
    int DevelopersUpdated,
    int MappingsAdded);

public record SyncFromClickUpRequest(Guid? ClickUpAccountId = null);

public record DeveloperRegistryQuery(
    Guid? ClickUpAccountId = null,
    string? Status = null,
    WorkRole? WorkRole = null,
    string? Search = null);
