namespace DevPulse.Application.Options;

/// <summary>
/// Default Telegram leave-management group. Used when DB settings are empty;
/// DB values from Leave Settings take precedence once saved.
/// </summary>
public sealed class LeaveTelegramOptions
{
    public const string SectionName = "LeaveTelegram";

    /// <summary>Bot token from @BotFather. Add the bot to your leave management group.</summary>
    public string? BotToken { get; set; }

    /// <summary>Group chat ID (supergroups usually start with -100).</summary>
    public string? ChatId { get; set; }

    /// <summary>Optional topic/thread ID for forum-style Telegram groups.</summary>
    public int? MessageThreadId { get; set; }
}
