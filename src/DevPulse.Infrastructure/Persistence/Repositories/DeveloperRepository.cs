namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class DeveloperRepository : IDeveloperRepository
{
    private readonly DevPulseDbContext _dbContext;

    public DeveloperRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Developer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await QueryWithMappings()
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Developer>> GetRegistryAsync(
        Guid? clickUpAccountId = null,
        bool? isActive = null,
        WorkRole? workRole = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithMappings();

        if (clickUpAccountId.HasValue)
        {
            query = query.Where(x => x.ClickUpMappings.Any(m =>
                m.ClickUpAccountId == clickUpAccountId.Value && m.ClickUpAccount.IsActive));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        if (workRole.HasValue)
        {
            query = query.Where(x => x.WorkRole == workRole.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Name.Contains(term)
                || (x.Email != null && x.Email.Contains(term)));
        }

        return await query
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Developer>> GetWithMappingsAsync(CancellationToken cancellationToken = default) =>
        await QueryWithMappings()
            .Where(x => x.ClickUpMappings.Any(m => m.ClickUpAccount.IsActive))
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Developer>> GetByIdsWithMappingsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        await QueryWithMappings()
            .Where(x => ids.Contains(x.Id))
            .Where(x => x.ClickUpMappings.Any(m => m.ClickUpAccount.IsActive))
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<Developer?> GetByIdWithMappingsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await QueryWithMappings()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Developer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Developers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Developer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        await _dbContext.Developers
            .FirstOrDefaultAsync(x => x.Email != null && x.Email == email, cancellationToken);

    public async Task<Developer?> GetByEmailIgnoreCaseAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _dbContext.Developers
            .Include(x => x.ReportingManager)
            .FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Developer>> GetActiveWithEmailAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Developers
            .Where(x => x.IsActive && x.Email != null && x.Email != "")
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Developer developer, CancellationToken cancellationToken = default)
    {
        _dbContext.Developers.Add(developer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Developer developer, CancellationToken cancellationToken = default)
    {
        _dbContext.Developers.Update(developer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var developer = await _dbContext.Developers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (developer is null)
        {
            return;
        }

        _dbContext.Developers.Remove(developer);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> MappingExistsAsync(Guid developerId, Guid accountId, CancellationToken cancellationToken = default) =>
        await _dbContext.DeveloperClickUpMappings
            .AnyAsync(x => x.DeveloperId == developerId && x.ClickUpAccountId == accountId, cancellationToken);

    public async Task AddMappingAsync(DeveloperClickUpMapping mapping, CancellationToken cancellationToken = default)
    {
        _dbContext.DeveloperClickUpMappings.Add(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Developer> QueryWithMappings() =>
        _dbContext.Developers
            .Include(x => x.ReportingManager)
            .Include(x => x.ClickUpMappings)
            .ThenInclude(x => x.ClickUpAccount)
            .Include(x => x.HubstaffMappings)
            .ThenInclude(x => x.HubstaffOrganization);

    public async Task<IReadOnlyList<Developer>> GetAllWithHubstaffMappingsAsync(CancellationToken cancellationToken = default) =>
        await QueryWithMappings()
            .Where(x => x.HubstaffMappings.Any(m => m.HubstaffOrganization.IsActive))
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Developer>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        await _dbContext.Developers
            .Where(x => ids.Contains(x.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<bool> HubstaffMappingExistsAsync(
        Guid developerId,
        Guid hubstaffOrganizationId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.DeveloperHubstaffMappings
            .AnyAsync(x => x.DeveloperId == developerId && x.HubstaffOrganizationId == hubstaffOrganizationId, cancellationToken);

    public async Task AddHubstaffMappingAsync(DeveloperHubstaffMapping mapping, CancellationToken cancellationToken = default)
    {
        _dbContext.DeveloperHubstaffMappings.Add(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveHubstaffMappingAsync(Guid mappingId, CancellationToken cancellationToken = default)
    {
        var mapping = await _dbContext.DeveloperHubstaffMappings.FirstOrDefaultAsync(x => x.Id == mappingId, cancellationToken);
        if (mapping is null)
        {
            return;
        }

        _dbContext.DeveloperHubstaffMappings.Remove(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
