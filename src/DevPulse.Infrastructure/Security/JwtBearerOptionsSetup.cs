using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevPulse.Infrastructure.Security;

public sealed class JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _settings;
    private readonly IJwtSigningKeyProvider _signingKeyProvider;

    public JwtBearerOptionsSetup(
        IOptions<JwtSettings> settings,
        IJwtSigningKeyProvider signingKeyProvider)
    {
        _settings = settings.Value;
        _signingKeyProvider = signingKeyProvider;
    }

    public void Configure(JwtBearerOptions options) =>
        Configure(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKeyProvider.SigningKey,
            ValidAlgorithms = new[] { _signingKeyProvider.Algorithm },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(_settings.ClockSkewSeconds),
            NameClaimType = DevPulseClaimTypes.Name,
            RoleClaimType = DevPulseClaimTypes.Role
        };
    }
}
