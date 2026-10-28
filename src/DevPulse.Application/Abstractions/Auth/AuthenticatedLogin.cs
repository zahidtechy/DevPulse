using System.Security.Claims;

namespace DevPulse.Application.Abstractions.Auth;

public sealed record AuthenticatedLogin(
    LoginResponse Response,
    ClaimsPrincipal Principal);
