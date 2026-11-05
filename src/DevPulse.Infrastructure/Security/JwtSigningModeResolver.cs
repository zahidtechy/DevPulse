namespace DevPulse.Infrastructure.Security;

public static class JwtSigningModeResolver
{
    public static JwtSigningMode Resolve(JwtSettings settings, IHostEnvironment environment)
    {
        if (TryResolve(settings, environment, out var mode, out var failure))
        {
            return mode;
        }

        throw new InvalidOperationException(failure);
    }

    public static bool TryResolve(
        JwtSettings settings,
        IHostEnvironment environment,
        out JwtSigningMode mode,
        out string? failure)
    {
        failure = null;
        mode = settings.SigningMode;

        var hasThumbprint = !string.IsNullOrWhiteSpace(NormalizeThumbprint(settings.SigningCertificateThumbprint));
        var hasSigningKey = !string.IsNullOrWhiteSpace(settings.SigningKey);

        if (mode == JwtSigningMode.Auto)
        {
            if (hasThumbprint)
            {
                mode = JwtSigningMode.Certificate;
            }
            else if (hasSigningKey)
            {
                mode = JwtSigningMode.Symmetric;
            }
            else if (environment.IsDevelopment())
            {
                mode = JwtSigningMode.DevelopmentEphemeral;
            }
            else
            {
                failure = "Jwt:SigningMode is Auto but no JWT signing key or certificate is configured. " +
                          "Set Jwt:SigningKey or Jwt:SigningCertificateThumbprint.";
                return false;
            }
        }

        if (mode == JwtSigningMode.Symmetric && !hasSigningKey)
        {
            if (environment.IsDevelopment())
            {
                // Keep local development zero-configuration. A random in-memory key is
                // generated for this process only; production always requires a key.
                mode = JwtSigningMode.DevelopmentEphemeral;
            }
            else
            {
                failure = "Jwt:SigningKey is required when Jwt:SigningMode is Symmetric.";
                return false;
            }
        }

        if (mode == JwtSigningMode.DevelopmentEphemeral && !environment.IsDevelopment())
        {
            failure = "Jwt:SigningMode DevelopmentEphemeral is only allowed in Development.";
            return false;
        }

        if (mode == JwtSigningMode.Certificate && !hasThumbprint)
        {
            failure = "Jwt:SigningCertificateThumbprint is required when Jwt:SigningMode is Certificate.";
            return false;
        }

        return true;
    }

    public static string? NormalizeThumbprint(string? thumbprint) =>
        string.IsNullOrWhiteSpace(thumbprint) ? null : thumbprint.Replace(" ", string.Empty);
}
