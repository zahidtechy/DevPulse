using DevPulse.Server.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController : ControllerBase
{
    private const string RefreshCookieName = "DevPulse.Refresh";
    private const string BrowserClientType = "devpulse-blazor";
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _environment = environment;
        _logger = logger;
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
        try
        {
            if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken))
            {
                await _authService.RevokeSessionAsync(refreshToken, "User signed out.", cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The request was cancelled or the client disconnected. The browser cookie is
            // still cleared below so a stale session cannot silently sign the user back in.
        }
        catch (Exception ex)
        {
            // A failed server-side revocation must not leave the refresh cookie in place.
            // Reuse detection will invalidate the family if the token is presented again.
            _logger.LogWarning(ex, "Failed to revoke the refresh session during logout.");
        }
        finally
        {
            DeleteRefreshCookie();
        }

        return NoContent();
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

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth"
            });
    }
}
