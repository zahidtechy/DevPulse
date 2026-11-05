using System.Security.Cryptography.X509Certificates;

namespace DevPulse.Infrastructure.Security;

public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    private static readonly string[] SupportedSigningModes =
    [
        nameof(JwtSigningMode.Auto),
        nameof(JwtSigningMode.Certificate),
        nameof(JwtSigningMode.Symmetric),
        nameof(JwtSigningMode.DevelopmentEphemeral)
    ];

    private readonly IHostEnvironment _environment;

    public JwtSettingsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, JwtSettings settings)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (settings.AccessTokenMinutes is < 1 or > 60)
        {
            failures.Add("Jwt:AccessTokenMinutes must be between 1 and 60.");
        }

        if (settings.ClockSkewSeconds is < 0 or > 300)
        {
            failures.Add("Jwt:ClockSkewSeconds must be between 0 and 300.");
        }

        if (!Enum.IsDefined(settings.SigningMode))
        {
            failures.Add(
                $"Jwt:SigningMode must be one of: {string.Join(", ", SupportedSigningModes)}.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (!JwtSigningModeResolver.TryResolve(settings, _environment, out var mode, out var resolutionFailure))
        {
            failures.Add(resolutionFailure!);
            return ValidateOptionsResult.Fail(failures);
        }

        switch (mode)
        {
            case JwtSigningMode.Certificate:
                ValidateCertificate(settings, failures);
                break;

            case JwtSigningMode.Symmetric:
                ValidateSymmetricKey(settings, failures);
                break;

            case JwtSigningMode.DevelopmentEphemeral:
                break;
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateCertificate(JwtSettings settings, List<string> failures)
    {
        var thumbprint = JwtSigningModeResolver.NormalizeThumbprint(settings.SigningCertificateThumbprint);

        if (!string.IsNullOrWhiteSpace(thumbprint)
            && (thumbprint.Length % 2 != 0 || thumbprint.Any(character => !Uri.IsHexDigit(character))))
        {
            failures.Add("Jwt:SigningCertificateThumbprint must contain hexadecimal characters.");
        }

        if (!Enum.TryParse<StoreName>(settings.SigningCertificateStoreName, true, out _))
        {
            failures.Add("Jwt:SigningCertificateStoreName is invalid.");
        }

        if (!Enum.TryParse<StoreLocation>(settings.SigningCertificateStoreLocation, true, out _))
        {
            failures.Add("Jwt:SigningCertificateStoreLocation is invalid.");
        }
    }

    private static void ValidateSymmetricKey(JwtSettings settings, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(settings.SigningKeyId))
        {
            failures.Add("Jwt:SigningKeyId is required when Jwt:SigningMode is Symmetric.");
        }

        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            failures.Add("Jwt:SigningKey is required when Jwt:SigningMode is Symmetric.");
            return;
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(settings.SigningKey);
        }
        catch (FormatException)
        {
            failures.Add("Jwt:SigningKey must be a valid Base64 string.");
            return;
        }

        if (keyBytes.Length < 32)
        {
            failures.Add("Jwt:SigningKey must be at least 32 bytes (256 bits) long.");
        }
    }
}
