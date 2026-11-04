namespace DevPulse.Application.Abstractions.Auth;

public interface IUserDisplayNameLookup
{
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
