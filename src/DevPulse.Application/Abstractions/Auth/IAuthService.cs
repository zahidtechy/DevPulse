namespace DevPulse.Application.Abstractions.Auth;

public interface IAuthService
{
    Task<Result<AuthenticatedLogin>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result> ChangeOwnPasswordAsync(Guid userId, SelfChangePasswordRequest request, CancellationToken cancellationToken = default);
}
