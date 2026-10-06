namespace DevPulse.Application.Abstractions.ClickUp;

public interface IClickUpAccountService
{
    Task<IReadOnlyList<ClickUpAccountDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<ClickUpAccountDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ClickUpAccountDto>> CreateAsync(CreateClickUpAccountRequest request, CancellationToken cancellationToken = default);

    Task<Result<ClickUpAccountDto>> UpdateAsync(Guid id, UpdateClickUpAccountRequest request, CancellationToken cancellationToken = default);

    Task<Result<ClickUpAccountDto>> UpdateStatusAsync(Guid id, UpdateClickUpAccountStatusRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ClickUpConnectionTestDto>> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClickUpMemberDto>>> GetMembersAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ClickUpUserLookupDto>> GetMemberByEmailAsync(
        string workspaceId,
        string email,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClickUpWorkspaceDto>>> GetAuthorizedWorkspacesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ClickUpTaskQueryResponse>> GetTasksAsync(Guid id, ClickUpTaskQueryRequest query, CancellationToken cancellationToken = default);
}
