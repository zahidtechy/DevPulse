namespace DevPulse.Shared.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    Guid? DeveloperId = null,
    string? DeveloperName = null,
    string? TelegramId = null);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role,
    Guid? DeveloperId = null,
    bool CreateDeveloper = false,
    string? TelegramId = null);

public sealed record UpdateUserRequest(
    string DisplayName,
    string Role,
    bool IsActive,
    Guid? DeveloperId = null,
    string? TelegramId = null);

public sealed record ChangePasswordRequest(string NewPassword);

public sealed record SelfChangePasswordRequest(string CurrentPassword, string NewPassword);
