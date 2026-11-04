namespace DevPulse.Infrastructure.Identity;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "DevPulse";

    public string Audience { get; set; } = "DevPulse.Api";

    public int AccessTokenMinutes { get; set; } = 10;

    public int ClockSkewSeconds { get; set; } = 30;

    public string? SigningCertificateThumbprint { get; set; }

    public string SigningCertificateStoreName { get; set; } = "My";

    public string SigningCertificateStoreLocation { get; set; } = "LocalMachine";
}
