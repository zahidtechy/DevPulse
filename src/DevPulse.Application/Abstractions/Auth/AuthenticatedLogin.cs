namespace DevPulse.Application.Abstractions.Auth;

public sealed record AuthenticatedLogin(
    LoginResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    bool IsPersistent);
