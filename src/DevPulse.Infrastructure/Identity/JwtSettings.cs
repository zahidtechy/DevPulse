namespace DevPulse.Infrastructure.Identity;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "DevPulse";

    public string Audience { get; set; } = "DevPulse.Client";

    public int ExpiryMinutes { get; set; } = 480;
}
