using System.Security.Cryptography;
using DevPulse.Infrastructure.Persistence;
using DevPulse.Infrastructure.Security;
using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly DevPulseDbContext _dbContext;
    private readonly RefreshTokenSettings _refreshTokenSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        JwtTokenGenerator tokenGenerator,
        DevPulseDbContext dbContext,
        IOptions<RefreshTokenSettings> refreshTokenSettings)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
        _dbContext = dbContext;
        _refreshTokenSettings = refreshTokenSettings.Value;
    }

    public async Task<Result<AuthenticatedLogin>> LoginAsync(
        LoginRequest request,
        string clientType,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<AuthenticatedLogin>.Failure("Email and password are required.");
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
        {
            return Result<AuthenticatedLogin>.Failure("Invalid email or password.");
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthenticatedLogin>.Failure("Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var issuedRefreshToken = CreateRefreshToken(
            user,
            Guid.NewGuid(),
            request.RememberMe,
            clientType,
            ipAddress,
            userAgent);
        _dbContext.RefreshTokens.Add(issuedRefreshToken.Entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(
            user,
            roles.ToList(),
            issuedRefreshToken.Entity.FamilyId,
            clientType);
        var role = roles.FirstOrDefault() ?? AppRoles.User;
        var userDto = MapToDto(user, role);
        var response = new LoginResponse(token, expiresAtUtc, userDto);

        return Result<AuthenticatedLogin>.Success(new AuthenticatedLogin(
            response,
            issuedRefreshToken.RawToken,
            issuedRefreshToken.Entity.ExpiresAtUtc,
            issuedRefreshToken.Entity.IsPersistent));
    }

    public async Task<Result<AuthenticatedLogin>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result<AuthenticatedLogin>.Failure("Refresh session is invalid or expired.");
        }

        var tokenHash = HashToken(refreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return Result<AuthenticatedLogin>.Failure("Refresh session is invalid or expired.");
        }

        if (storedToken.ConsumedAtUtc.HasValue || storedToken.RevokedAtUtc.HasValue)
        {
            await RevokeFamilyAsync(storedToken.FamilyId, "Refresh token reuse detected.", cancellationToken);
            return Result<AuthenticatedLogin>.Failure("Refresh session is invalid or expired.");
        }

        var now = DateTime.UtcNow;
        var currentSecurityStamp = await _userManager.GetSecurityStampAsync(storedToken.User);
        if (storedToken.ExpiresAtUtc <= now
            || !storedToken.User.IsActive
            || !string.Equals(storedToken.SecurityStamp, currentSecurityStamp, StringComparison.Ordinal))
        {
            await RevokeFamilyAsync(storedToken.FamilyId, "Refresh session expired or user security changed.", cancellationToken);
            return Result<AuthenticatedLogin>.Failure("Refresh session is invalid or expired.");
        }

        var replacement = CreateRefreshToken(
            storedToken.User,
            storedToken.FamilyId,
            storedToken.IsPersistent,
            storedToken.ClientType,
            ipAddress,
            userAgent,
            storedToken.ExpiresAtUtc);

        storedToken.ConsumedAtUtc = now;
        storedToken.ReplacedByTokenId = replacement.Entity.Id;
        _dbContext.RefreshTokens.Add(replacement.Entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AuthenticatedLogin>.Failure("Refresh session is invalid or expired.");
        }

        var roles = await _userManager.GetRolesAsync(storedToken.User);
        var (accessToken, accessTokenExpiresAtUtc) = _tokenGenerator.GenerateToken(
            storedToken.User,
            roles.ToList(),
            storedToken.FamilyId,
            storedToken.ClientType);
        var userDto = MapToDto(storedToken.User, roles.FirstOrDefault() ?? AppRoles.User);
        var response = new LoginResponse(accessToken, accessTokenExpiresAtUtc, userDto);

        return Result<AuthenticatedLogin>.Success(new AuthenticatedLogin(
            response,
            replacement.RawToken,
            replacement.Entity.ExpiresAtUtc,
            replacement.Entity.IsPersistent));
    }

    public async Task RevokeSessionAsync(
        string refreshToken,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashToken(refreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null)
        {
            await RevokeFamilyAsync(storedToken.FamilyId, reason, cancellationToken);
        }
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevocationReason, reason),
                cancellationToken);
    }

    public async Task<Result> ChangeOwnPasswordAsync(Guid userId, SelfChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return Result.Failure("Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Result.Failure("New password is required.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return Result.Failure("User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Select(e => e.Description));
        }

        await RevokeAllSessionsAsync(user.Id, "Password changed.", cancellationToken);
        return Result.Success();
    }

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(
        ApplicationUser user,
        Guid familyId,
        bool isPersistent,
        string clientType,
        string? ipAddress,
        string? userAgent,
        DateTime? absoluteExpiryUtc = null)
    {
        var now = DateTime.UtcNow;
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = absoluteExpiryUtc ?? (isPersistent
            ? now.AddDays(_refreshTokenSettings.RememberMeDays)
            : now.AddHours(_refreshTokenSettings.StandardSessionHours));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            ClientType = Truncate(clientType, 32) ?? "browser",
            IsPersistent = isPersistent,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512)
        };

        return (entity, rawToken);
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await _dbContext.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevocationReason, Truncate(reason, 200)),
                cancellationToken);
    }

    private static byte[] HashToken(string token) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];

    internal static UserDto MapToDto(ApplicationUser user, string role, string? developerName = null) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            role,
            user.IsActive,
            user.CreatedAtUtc,
            user.DeveloperId,
            developerName,
            user.TelegramId);
}
