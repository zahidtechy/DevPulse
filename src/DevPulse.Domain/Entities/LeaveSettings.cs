namespace DevPulse.Domain.Entities;

/// <summary>
/// Singleton company settings for leave notifications and working-day calculation.
/// </summary>
public class LeaveSettings
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    public string? EncryptedTelegramBotToken { get; set; }

    public string? TelegramChatId { get; set; }

    /// <summary>
    /// Bitmask of weekend days (bit = 1 &lt;&lt; DayOfWeek). Default Sat+Sun = 65.
    /// </summary>
    public int WeekendDaysBitmask { get; set; } = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

    public string? LastTelegramError { get; set; }

    public DateTime? LastTelegramSuccessAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
