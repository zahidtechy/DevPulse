using DevPulse.Server.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController : ControllerBase
{
    private const string RefreshCookieName = "DevPulse.Refresh";
    private const string LegacyAuthCookieName = "DevPulse.Auth";
    private const string BrowserClientType = "devpulse-blazor";
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IWebHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request,
            BrowserClientType,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            return Unauthorized(new { error = result.Error });
        }

        SetRefreshCookie(result.Value);
        DeleteCookie(LegacyAuthCookieName, "/");
        return Ok(result.Value.Response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            return Unauthorized(new { error = "Refresh session is invalid or expired." });
        }

        var result = await _authService.RefreshAsync(
            refreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            DeleteRefreshCookie();
            return Unauthorized(new { error = result.Error });
        }

        SetRefreshCookie(result.Value);
        return Ok(result.Value.Response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
        {
            await _authService.RevokeSessionAsync(refreshToken, "User signed out.", cancellationToken);
        }

        DeleteRefreshCookie();
        DeleteCookie(LegacyAuthCookieName, "/");
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirstValue(DevPulseClaimTypes.Subject);
        var email = User.FindFirstValue(DevPulseClaimTypes.Email) ?? string.Empty;
        var displayName = User.FindFirstValue(DevPulseClaimTypes.DisplayName)
            ?? User.FindFirstValue(DevPulseClaimTypes.Name)
            ?? email;
        var role = User.FindFirstValue(DevPulseClaimTypes.Role) ?? AppRoles.User;

        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        return Ok(new UserDto(id, email, displayName, role, true, DateTime.UtcNow));
    }

    [Authorize]
    [HttpPut("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] SelfChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(DevPulseClaimTypes.Subject);
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var result = await _authService.ChangeOwnPasswordAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return NoContent();
    }

    private void SetRefreshCookie(AuthenticatedLogin login)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            login.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                IsEssential = true,
                Expires = login.IsPersistent
                    ? new DateTimeOffset(login.RefreshTokenExpiresAtUtc, TimeSpan.Zero)
                    : null
            });
    }

    private void DeleteRefreshCookie() => DeleteCookie(RefreshCookieName, "/api/auth");

    private void DeleteCookie(string name, string path)
    {
        Response.Cookies.Delete(
            name,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = path
            });
    }
}
