namespace DevPulse.Infrastructure.Services;

public sealed class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDeveloperRepository _developerRepository;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        IDeveloperRepository developerRepository)
    {
        _userManager = userManager;
        _developerRepository = developerRepository;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = _userManager.Users.OrderBy(u => u.Email).ToList();
        var developerNames = await LoadDeveloperNamesAsync(users, cancellationToken);
        var result = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var developerName = user.DeveloperId is Guid developerId
                ? developerNames.GetValueOrDefault(developerId)
                : null;
            result.Add(AuthService.MapToDto(user, roles.FirstOrDefault() ?? AppRoles.User, developerName));
        }

        return result;
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var developerName = await GetDeveloperNameAsync(user.DeveloperId, cancellationToken);
        return Result<UserDto>.Success(AuthService.MapToDto(user, roles.FirstOrDefault() ?? AppRoles.User, developerName));
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateUserInput(request.Email, request.Password, request.DisplayName, request.Role);
        if (validationError is not null)
        {
            return Result<UserDto>.Failure(validationError);
        }

        if (request.DeveloperId.HasValue && request.CreateDeveloper)
        {
            return Result<UserDto>.Failure("Choose either an existing developer or creating a new one, not both.");
        }

        var existing = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existing is not null)
        {
            return Result<UserDto>.Failure("A user with this email already exists.");
        }

        var developerLink = await ResolveDeveloperLinkForCreateAsync(request, cancellationToken);
        if (developerLink.IsFailure)
        {
            return Result<UserDto>.Failure(developerLink.Errors);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            DeveloperId = developerLink.Value
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Result<UserDto>.Failure(createResult.Errors.Select(e => e.Description));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return Result<UserDto>.Failure(roleResult.Errors.Select(e => e.Description));
        }

        var developerName = await GetDeveloperNameAsync(user.DeveloperId, cancellationToken);
        return Result<UserDto>.Success(AuthService.MapToDto(user, request.Role, developerName));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<UserDto>.Failure("Display name is required.");
        }

        if (!AppRoles.All.Contains(request.Role))
        {
            return Result<UserDto>.Failure("Invalid role.");
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        if (request.DeveloperId.HasValue)
        {
            var linkValidation = await ValidateDeveloperLinkAsync(request.DeveloperId.Value, user.Id, cancellationToken);
            if (linkValidation.IsFailure)
            {
                return Result<UserDto>.Failure(linkValidation.Error!);
            }
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.DeveloperId = request.DeveloperId;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result<UserDto>.Failure(updateResult.Errors.Select(e => e.Description));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(request.Role))
        {
            if (currentRoles.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded)
                {
                    return Result<UserDto>.Failure(removeResult.Errors.Select(e => e.Description));
                }
            }

            var addResult = await _userManager.AddToRoleAsync(user, request.Role);
            if (!addResult.Succeeded)
            {
                return Result<UserDto>.Failure(addResult.Errors.Select(e => e.Description));
            }
        }

        var developerName = await GetDeveloperNameAsync(user.DeveloperId, cancellationToken);
        return Result<UserDto>.Success(AuthService.MapToDto(user, request.Role, developerName));
    }

    public async Task<Result> ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Result.Failure("New password is required.");
        }

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin))
        {
            var adminCount = 0;
            foreach (var admin in _userManager.Users)
            {
                if (await _userManager.IsInRoleAsync(admin, AppRoles.Admin))
                {
                    adminCount++;
                }
            }

            if (adminCount <= 1)
            {
                return Result.Failure("Cannot delete the last administrator.");
            }
        }

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(result.Errors.Select(e => e.Description));
    }

    private async Task<Result<Guid?>> ResolveDeveloperLinkForCreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CreateDeveloper)
        {
            var normalizedEmail = request.Email.Trim();
            var existingDeveloper = await _developerRepository.GetByEmailIgnoreCaseAsync(normalizedEmail, cancellationToken);
            if (existingDeveloper is not null)
            {
                return Result<Guid?>.Failure("A developer with this email already exists. Link that developer instead.");
            }

            var developer = new Developer
            {
                Name = request.DisplayName.Trim(),
                Email = normalizedEmail
            };

            await _developerRepository.AddAsync(developer, cancellationToken);
            return Result<Guid?>.Success(developer.Id);
        }

        if (request.DeveloperId is Guid developerId)
        {
            var validation = await ValidateDeveloperLinkAsync(developerId, excludeUserId: null, cancellationToken);
            return validation.IsFailure
                ? Result<Guid?>.Failure(validation.Error!)
                : Result<Guid?>.Success(developerId);
        }

        return Result<Guid?>.Success(null);
    }

    private async Task<Result> ValidateDeveloperLinkAsync(
        Guid developerId,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var developer = await _developerRepository.GetByIdAsync(developerId, cancellationToken);
        if (developer is null)
        {
            return Result.Failure("Selected developer was not found.");
        }

        var alreadyLinked = _userManager.Users
            .Any(u => u.DeveloperId == developerId && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));

        return alreadyLinked
            ? Result.Failure("That developer is already linked to another user.")
            : Result.Success();
    }

    private async Task<string?> GetDeveloperNameAsync(Guid? developerId, CancellationToken cancellationToken)
    {
        if (!developerId.HasValue)
        {
            return null;
        }

        var developer = await _developerRepository.GetByIdAsync(developerId.Value, cancellationToken);
        return developer?.Name;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadDeveloperNamesAsync(
        IReadOnlyList<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var developerIds = users
            .Where(u => u.DeveloperId.HasValue)
            .Select(u => u.DeveloperId!.Value)
            .Distinct()
            .ToList();

        if (developerIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var names = new Dictionary<Guid, string>(developerIds.Count);
        foreach (var developerId in developerIds)
        {
            var developer = await _developerRepository.GetByIdAsync(developerId, cancellationToken);
            if (developer is not null)
            {
                names[developerId] = developer.Name;
            }
        }

        return names;
    }

    private static string? ValidateUserInput(string email, string password, string displayName, string role)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email is required.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Display name is required.";
        }

        if (!AppRoles.All.Contains(role))
        {
            return "Invalid role.";
        }

        return null;
    }
}
