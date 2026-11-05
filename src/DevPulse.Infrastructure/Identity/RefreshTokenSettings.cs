namespace DevPulse.Infrastructure.Identity;

public sealed class RefreshTokenSettings
{
    public const string SectionName = "RefreshTokens";

    public int StandardSessionHours { get; set; } = 8;

    public int RememberMeDays { get; set; } = 7;

    /// <summary>How often the cleanup job runs. Defaults to weekly.</summary>
    public int CleanupIntervalDays { get; set; } = 7;

    /// <summary>How long expired or revoked tokens are retained for auditing.</summary>
    public int CleanupRetentionDays { get; set; } = 7;
}
