using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

internal sealed class DevelopmentEphemeralJwtSigningKeyProvider : IJwtSigningKeyProvider
{
    public DevelopmentEphemeralJwtSigningKeyProvider(ILogger logger)
    {
        var securityKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32))
        {
            KeyId = "development-ephemeral"
        };

        SigningKey = securityKey;
        SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        Algorithm = SecurityAlgorithms.HmacSha256;
        KeyId = "development-ephemeral";

        logger.LogWarning(
            "Using an ephemeral symmetric JWT signing key in Development because Jwt:SigningKey is not configured. " +
            "Tokens become invalid when the server restarts.");
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
