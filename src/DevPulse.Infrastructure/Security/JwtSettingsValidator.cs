using System.Security.Cryptography.X509Certificates;

namespace DevPulse.Infrastructure.Security;

public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
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

        if (!Enum.TryParse<StoreName>(settings.SigningCertificateStoreName, true, out _))
        {
            failures.Add("Jwt:SigningCertificateStoreName is invalid.");
        }

        if (!Enum.TryParse<StoreLocation>(settings.SigningCertificateStoreLocation, true, out _))
        {
            failures.Add("Jwt:SigningCertificateStoreLocation is invalid.");
        }

        var thumbprint = settings.SigningCertificateThumbprint?.Replace(" ", string.Empty);
        if (!_environment.IsDevelopment() && string.IsNullOrWhiteSpace(thumbprint))
        {
            failures.Add("Jwt:SigningCertificateThumbprint is required outside Development.");
        }
        else if (!string.IsNullOrWhiteSpace(thumbprint)
                 && (thumbprint.Length % 2 != 0 || thumbprint.Any(character => !Uri.IsHexDigit(character))))
        {
            failures.Add("Jwt:SigningCertificateThumbprint must contain hexadecimal characters.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
