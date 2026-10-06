namespace DevPulse.Application.Abstractions.Persistence;

public interface IClickUpAccountRepository
{
    Task<IReadOnlyList<ClickUpAccount>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClickUpAccount>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<ClickUpAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ClickUpAccount?> GetByWorkspaceIdAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByWorkspaceIdAsync(string workspaceId, Guid? excludeAccountId = null, CancellationToken cancellationToken = default);

    Task AddAsync(ClickUpAccount account, CancellationToken cancellationToken = default);

    Task UpdateAsync(ClickUpAccount account, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
