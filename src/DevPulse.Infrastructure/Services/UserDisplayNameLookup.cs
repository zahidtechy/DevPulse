namespace DevPulse.Infrastructure.Services;

public sealed class UserDisplayNameLookup : IUserDisplayNameLookup
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserDisplayNameLookup(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _userManager.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToDictionaryAsync(
                entry => entry.Id,
                entry => entry.DisplayName,
                cancellationToken);
    }
}
