using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

public sealed class JwtSigningKeyProvider : IDisposable
{
    private readonly IDisposable? _ownedKey;

    public JwtSigningKeyProvider(
        IOptions<JwtSettings> settings,
        IHostEnvironment environment,
        ILogger<JwtSigningKeyProvider> logger)
    {
        var thumbprint = settings.Value.SigningCertificateThumbprint?.Replace(" ", string.Empty);
        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            var certificate = LoadCertificate(settings.Value, thumbprint);
            SigningKey = new X509SecurityKey(certificate) { KeyId = certificate.Thumbprint };
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);
            _ownedKey = certificate;
            return;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "JWT signing certificate is not configured. Set Jwt:SigningCertificateThumbprint.");
        }

        var rsa = RSA.Create(2048);
        SigningKey = new RsaSecurityKey(rsa) { KeyId = "development-ephemeral" };
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);
        _ownedKey = rsa;

        logger.LogWarning(
            "Using an ephemeral JWT signing key in Development. Tokens become invalid when the server restarts.");
    }

    public SecurityKey SigningKey { get; }

    public SigningCredentials SigningCredentials { get; }

    public void Dispose() => _ownedKey?.Dispose();

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
