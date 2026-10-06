namespace DevPulse.Application.Abstractions.Auth;

public interface IUserDeveloperResolver
{
    /// <summary>
    /// Resolves the developer for a logged-in user.
    /// Uses the explicit user-to-developer link when present, otherwise falls back to email matching.
    /// </summary>
    Task<Developer?> ResolveAsync(string userEmail, CancellationToken cancellationToken = default);
}
