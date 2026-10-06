namespace DevPulse.Infrastructure.Services;

public sealed class UserDeveloperResolver : IUserDeveloperResolver
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDeveloperRepository _developerRepository;

    public UserDeveloperResolver(
        UserManager<ApplicationUser> userManager,
        IDeveloperRepository developerRepository)
    {
        _userManager = userManager;
        _developerRepository = developerRepository;
    }

    public async Task<Developer?> ResolveAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(userEmail.Trim());
        if (user?.DeveloperId is Guid developerId)
        {
            var linked = await _developerRepository.GetByIdAsync(developerId, cancellationToken);
            if (linked is not null)
            {
                return linked;
            }
        }

        return await _developerRepository.GetByEmailIgnoreCaseAsync(userEmail, cancellationToken);
    }
}
