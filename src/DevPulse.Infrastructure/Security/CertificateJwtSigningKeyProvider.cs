using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

internal sealed class CertificateJwtSigningKeyProvider : IJwtSigningKeyProvider
{
    private readonly X509Certificate2 _certificate;

    public CertificateJwtSigningKeyProvider(JwtSettings settings, ILogger logger)
    {
        var thumbprint = JwtSigningModeResolver.NormalizeThumbprint(settings.SigningCertificateThumbprint)
            ?? throw new InvalidOperationException(
                "Jwt:SigningCertificateThumbprint is required when Jwt:SigningMode is Certificate.");

        _certificate = LoadCertificate(settings, thumbprint);

        SigningKey = new X509SecurityKey(_certificate) { KeyId = _certificate.Thumbprint };
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);
        Algorithm = SecurityAlgorithms.RsaSha256;
        KeyId = _certificate.Thumbprint;

        logger.LogInformation("JWT signing configured with certificate {KeyId} (RS256).", KeyId);
    }

    public string Algorithm { get; }

    public string KeyId { get; }

    public SecurityKey SigningKey { get; }

    public SigningCredentials SigningCredentials { get; }

    public void Dispose() => _certificate.Dispose();

    private static X509Certificate2 LoadCertificate(JwtSettings settings, string thumbprint)
    {
        if (!Enum.TryParse<StoreName>(settings.SigningCertificateStoreName, ignoreCase: true, out var storeName))
        {
            throw new InvalidOperationException(
                $"Invalid JWT certificate store name '{settings.SigningCertificateStoreName}'.");
        }

        if (!Enum.TryParse<StoreLocation>(settings.SigningCertificateStoreLocation, ignoreCase: true, out var storeLocation))
        {
            throw new InvalidOperationException(
                $"Invalid JWT certificate store location '{settings.SigningCertificateStoreLocation}'.");
        }

        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly);
        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: true);
        var certificate = matches.OfType<X509Certificate2>().FirstOrDefault();

        if (certificate is null)
        {
            throw new InvalidOperationException(
                $"JWT signing certificate '{thumbprint}' was not found in {storeLocation}/{storeName}.");
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException("The configured JWT signing certificate has no accessible private key.");
        }

        // Signing is hard-coded to RS256. Reject non-RSA certificates here so a
        // configuration mistake fails at startup instead of at the first token request.
        using (var rsa = certificate.GetRSAPrivateKey())
        {
            if (rsa is null)
            {
                certificate.Dispose();
                throw new InvalidOperationException(
                    $"The configured JWT signing certificate '{thumbprint}' does not expose an RSA private key. " +
                    "Only RSA certificates (RS256) are supported.");
            }
        }

        return certificate;
    }
}
