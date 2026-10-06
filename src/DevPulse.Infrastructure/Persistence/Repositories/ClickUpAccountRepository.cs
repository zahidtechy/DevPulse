namespace DevPulse.Infrastructure.Persistence.Repositories;

public sealed class ClickUpAccountRepository : IClickUpAccountRepository
{
    private readonly DevPulseDbContext _dbContext;

    public ClickUpAccountRepository(DevPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ClickUpAccount>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ClickUpAccounts
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClickUpAccount>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.ClickUpAccounts
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<ClickUpAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.ClickUpAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<ClickUpAccount?> GetByWorkspaceIdAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        await _dbContext.ClickUpAccounts
            .FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId, cancellationToken);

    public async Task<bool> ExistsByWorkspaceIdAsync(string workspaceId, Guid? excludeAccountId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ClickUpAccounts.Where(x => x.WorkspaceId == workspaceId);
        if (excludeAccountId.HasValue)
        {
            query = query.Where(x => x.Id != excludeAccountId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(ClickUpAccount account, CancellationToken cancellationToken = default)
    {
        _dbContext.ClickUpAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ClickUpAccount account, CancellationToken cancellationToken = default)
    {
        _dbContext.ClickUpAccounts.Update(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.ClickUpAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (account is null)
        {
            return;
        }

        _dbContext.ClickUpAccounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
