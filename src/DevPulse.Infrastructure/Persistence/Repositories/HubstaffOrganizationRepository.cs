namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class HubstaffOrganizationRepository : IHubstaffOrganizationRepository
{
    private readonly DevPulseDbContext _dbContext;

    public HubstaffOrganizationRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<HubstaffOrganization>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffOrganizations
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HubstaffOrganization>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffOrganizations
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<HubstaffOrganization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffOrganizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ExistsByOrganizationIdAsync(int organizationId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.HubstaffOrganizations.Where(x => x.OrganizationId == organizationId);
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> HasDailyActivitiesAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.HubstaffDailyActivities.AnyAsync(x => x.HubstaffOrganizationId == id, cancellationToken);

    public async Task AddAsync(HubstaffOrganization organization, CancellationToken cancellationToken = default)
    {
        _dbContext.HubstaffOrganizations.Add(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(HubstaffOrganization organization, CancellationToken cancellationToken = default)
    {
        _dbContext.HubstaffOrganizations.Update(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _dbContext.HubstaffOrganizations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (organization is null)
        {
            return;
        }

        _dbContext.HubstaffOrganizations.Remove(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
