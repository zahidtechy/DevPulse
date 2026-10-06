using DevPulse.Infrastructure.Security;

namespace DevPulse.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;

    public AuthService(UserManager<ApplicationUser> userManager, JwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<LoginResponse>.Failure("Email and password are required.");
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return Result<LoginResponse>.Failure("Invalid email or password.");
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Result<LoginResponse>.Failure("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user, roles.ToList());
        var role = roles.FirstOrDefault() ?? AppRoles.User;
        var userDto = MapToDto(user, role);

        return Result<LoginResponse>.Success(new LoginResponse(token, expiresAtUtc, userDto));
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
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Description));
    }

    internal static UserDto MapToDto(ApplicationUser user, string role, string? developerName = null) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            role,
            user.IsActive,
            user.CreatedAtUtc,
            user.DeveloperId,
            developerName);
}
