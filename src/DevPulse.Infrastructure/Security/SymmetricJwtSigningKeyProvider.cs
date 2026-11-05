using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

internal sealed class SymmetricJwtSigningKeyProvider : IJwtSigningKeyProvider
{
    public SymmetricJwtSigningKeyProvider(JwtSettings settings, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is required when Jwt:SigningMode is Symmetric.");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(settings.SigningKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a valid Base64 string.",
                exception);
        }

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes (256 bits) long.");
        }

        var securityKey = new SymmetricSecurityKey(keyBytes)
        {
            KeyId = settings.SigningKeyId
        };

        SigningKey = securityKey;
        SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        Algorithm = SecurityAlgorithms.HmacSha256;
        KeyId = settings.SigningKeyId;

        logger.LogInformation("JWT signing configured with symmetric key {KeyId} (HS256).", KeyId);
    }

    public string Algorithm { get; }

    public string KeyId { get; }

    public SecurityKey SigningKey { get; }

    public SigningCredentials SigningCredentials { get; }

    public void Dispose()
    {
        // SymmetricSecurityKey does not own unmanaged resources.
    }
}
