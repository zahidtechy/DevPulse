namespace DevPulse.Application.Abstractions.Auth;

public interface IUserEmailLookup
{
    Task<IReadOnlySet<string>> GetActiveUserEmailsAsync(CancellationToken cancellationToken = default);
}
