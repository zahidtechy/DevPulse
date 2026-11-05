namespace DevPulse.Infrastructure.Security;

internal sealed class JwtSigningKeyProviderStartupValidator : IHostedService
{
    private readonly IJwtSigningKeyProvider _signingKeyProvider;

    public JwtSigningKeyProviderStartupValidator(IJwtSigningKeyProvider signingKeyProvider)
    {
        _signingKeyProvider = signingKeyProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Resolving the provider at startup makes invalid keys fail fast instead of
        // on the first login request.
        _ = _signingKeyProvider.SigningCredentials;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
