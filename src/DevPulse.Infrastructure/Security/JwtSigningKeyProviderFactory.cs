namespace DevPulse.Infrastructure.Security;

public static class JwtSigningKeyProviderFactory
{
    public static IJwtSigningKeyProvider Create(
        JwtSettings settings,
        IHostEnvironment environment,
        ILoggerFactory loggerFactory)
    {
        var mode = JwtSigningModeResolver.Resolve(settings, environment);
        var logger = loggerFactory.CreateLogger("DevPulse.Infrastructure.Security.JwtSigningKeyProvider");

        return mode switch
        {
            JwtSigningMode.Certificate => new CertificateJwtSigningKeyProvider(settings, logger),
            JwtSigningMode.Symmetric => new SymmetricJwtSigningKeyProvider(settings, logger),
            JwtSigningMode.DevelopmentEphemeral => new DevelopmentEphemeralJwtSigningKeyProvider(logger),
            _ => throw new InvalidOperationException($"Unsupported JWT signing mode '{mode}'.")
        };
    }
}
