using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

public sealed class JwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IJwtSigningKeyProvider _signingKeyProvider;

    public JwtTokenGenerator(
        IOptions<JwtSettings> settings,
        IJwtSigningKeyProvider signingKeyProvider)
    {
        _settings = settings.Value;
        _signingKeyProvider = signingKeyProvider;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        Guid sessionId,
        string clientType)
    {
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(DevPulseClaimTypes.Subject, user.Id.ToString()),
            new(DevPulseClaimTypes.Email, user.Email ?? string.Empty),
            new(DevPulseClaimTypes.Name, user.DisplayName),
            new(DevPulseClaimTypes.DisplayName, user.DisplayName),
            new(DevPulseClaimTypes.SessionId, sessionId.ToString()),
            new(DevPulseClaimTypes.ClientId, clientType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAtUtc).ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(DevPulseClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: _signingKeyProvider.SigningCredentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
