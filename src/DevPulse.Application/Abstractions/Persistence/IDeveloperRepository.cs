namespace DevPulse.Application.Abstractions.Persistence;

public interface IDeveloperRepository
{
    Task<IReadOnlyList<Developer>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all developers for the admin registry, with optional filters (ClickUp account, status, role, search).
    /// </summary>
    Task<IReadOnlyList<Developer>> GetRegistryAsync(
        Guid? clickUpAccountId = null,
        bool? isActive = null,
        Domain.Enums.WorkRole? workRole = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns developers with at least one mapping to an active ClickUp account.
    /// </summary>
    Task<IReadOnlyList<Developer>> GetWithMappingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns matching developers that have at least one mapping to an active ClickUp account.
    /// </summary>
    Task<IReadOnlyList<Developer>> GetByIdsWithMappingsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<Developer?> GetByIdWithMappingsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Developer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Developer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Developer?> GetByEmailIgnoreCaseAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Developer>> GetActiveWithEmailAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Developer developer, CancellationToken cancellationToken = default);

    Task UpdateAsync(Developer developer, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> MappingExistsAsync(Guid developerId, Guid accountId, CancellationToken cancellationToken = default);

    Task AddMappingAsync(DeveloperClickUpMapping mapping, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Developer>> GetAllWithHubstaffMappingsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Developer>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<bool> HubstaffMappingExistsAsync(Guid developerId, Guid hubstaffOrganizationId, CancellationToken cancellationToken = default);

    Task AddHubstaffMappingAsync(DeveloperHubstaffMapping mapping, CancellationToken cancellationToken = default);

    Task RemoveHubstaffMappingAsync(Guid mappingId, CancellationToken cancellationToken = default);
}
