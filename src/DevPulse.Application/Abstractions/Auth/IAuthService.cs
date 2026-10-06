namespace DevPulse.Application.Abstractions.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result> ChangeOwnPasswordAsync(Guid userId, SelfChangePasswordRequest request, CancellationToken cancellationToken = default);
}
