using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

public interface IJwtSigningKeyProvider : IDisposable
{
    string Algorithm { get; }

    string KeyId { get; }

    SecurityKey SigningKey { get; }

    SigningCredentials SigningCredentials { get; }
}
