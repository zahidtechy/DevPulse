namespace DevPulse.Infrastructure.Identity;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "RefreshTokens";

    public int StandardSessionHours { get; set; } = 8;

    public int RememberMeDays { get; set; } = 7;
}
