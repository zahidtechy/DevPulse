namespace DevPulse.Application.Abstractions.Auth;

public interface IAuthService
{
    Task<Result<AuthenticatedLogin>> LoginAsync(
        LoginRequest request,
        string clientType,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<AuthenticatedLogin>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(
        string refreshToken,
        string reason,
        CancellationToken cancellationToken = default);

    Task RevokeAllSessionsAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> ChangeOwnPasswordAsync(Guid userId, SelfChangePasswordRequest request, CancellationToken cancellationToken = default);
}
