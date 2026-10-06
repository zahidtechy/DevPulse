using Microsoft.AspNetCore.DataProtection;

namespace DevPulse.Infrastructure.Services;

public sealed class UserEmailLookup : IUserEmailLookup
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserEmailLookup(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public Task<IReadOnlySet<string>> GetActiveUserEmailsAsync(CancellationToken cancellationToken = default)
    {
        var emails = _userManager.Users
            .Where(u => u.IsActive && u.Email != null)
            .Select(u => u.Email!.ToLower())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlySet<string>>(emails);
    }
}

public sealed class LeaveTelegramService : ILeaveTelegramService
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly ITelegramApiClient _telegramApiClient;
    private readonly IDataProtector _tokenProtector;
    private readonly LeaveTelegramOptions _defaultOptions;
    private readonly ILogger<LeaveTelegramService> _logger;

    public LeaveTelegramService(
        ILeaveRepository leaveRepository,
        ITelegramApiClient telegramApiClient,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<LeaveTelegramOptions> defaultOptions,
        ILogger<LeaveTelegramService> logger)
    {
        _leaveRepository = leaveRepository;
        _telegramApiClient = telegramApiClient;
        _tokenProtector = dataProtectionProvider.CreateProtector("DevPulse.Telegram.BotTokens.v1");
        _defaultOptions = defaultOptions.Value;
        _logger = logger;
    }

    public async Task<int> GetWeekendDaysBitmaskAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _leaveRepository.GetOrCreateSettingsAsync(cancellationToken);
        return settings.WeekendDaysBitmask;
    }

    public async Task NotifyAsync(string htmlMessage, CancellationToken cancellationToken = default)
    {
        var settings = await _leaveRepository.GetOrCreateSettingsAsync(cancellationToken);
        var config = ResolveTelegramConfig(settings);
        if (!config.IsConfigured)
        {
            _logger.LogInformation("Telegram leave group not configured; skipping notification.");
            return;
        }

        var (success, error) = await _telegramApiClient.SendMessageAsync(
            config.BotToken!,
            config.ChatId!,
            htmlMessage,
            config.MessageThreadId,
            cancellationToken);

        await RecordSendResultAsync(settings, success, error, cancellationToken);
    }

    public async Task<Result> SendTestAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _leaveRepository.GetOrCreateSettingsAsync(cancellationToken);
        var config = ResolveTelegramConfig(settings);
        if (!config.IsConfigured)
        {
            return Result.Failure(
                "Configure the leave management Telegram group in Leave Settings or appsettings (LeaveTelegram:BotToken and LeaveTelegram:ChatId).");
        }

        var (success, error) = await _telegramApiClient.SendMessageAsync(
            config.BotToken!,
            config.ChatId!,
            "<b>DevPulse Leave</b>\nTest message — your leave management group is connected.",
            config.MessageThreadId,
            cancellationToken);

        await RecordSendResultAsync(settings, success, error, cancellationToken);
        return success ? Result.Success() : Result.Failure(error ?? "Telegram test failed.");
    }

    public async Task<LeaveSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _leaveRepository.GetOrCreateSettingsAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<Result<LeaveSettingsDto>> UpdateSettingsAsync(UpdateLeaveSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.WeekendDaysBitmask <= 0)
        {
            return Result<LeaveSettingsDto>.Failure("Select at least one weekend day.");
        }

        var settings = await _leaveRepository.GetOrCreateSettingsAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TelegramBotToken))
        {
            settings.EncryptedTelegramBotToken = _tokenProtector.Protect(request.TelegramBotToken.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.TelegramChatId))
        {
            settings.TelegramChatId = request.TelegramChatId.Trim();
        }

        settings.WeekendDaysBitmask = request.WeekendDaysBitmask;
        await _leaveRepository.UpdateSettingsAsync(settings, cancellationToken);

        return Result<LeaveSettingsDto>.Success(MapSettings(settings));
    }

    private async Task RecordSendResultAsync(
        Domain.Entities.LeaveSettings settings,
        bool success,
        string? error,
        CancellationToken cancellationToken)
    {
        if (success)
        {
            settings.LastTelegramError = null;
            settings.LastTelegramSuccessAtUtc = DateTime.UtcNow;
        }
        else
        {
            settings.LastTelegramError = error;
            _logger.LogWarning("Leave Telegram notification failed: {Error}", error);
        }

        await _leaveRepository.UpdateSettingsAsync(settings, cancellationToken);
    }

    private TelegramConfig ResolveTelegramConfig(Domain.Entities.LeaveSettings settings)
    {
        var dbToken = UnprotectToken(settings.EncryptedTelegramBotToken);
        var dbChatId = string.IsNullOrWhiteSpace(settings.TelegramChatId) ? null : settings.TelegramChatId.Trim();

        var botToken = dbToken ?? NormalizeOption(_defaultOptions.BotToken);
        var chatId = dbChatId ?? NormalizeOption(_defaultOptions.ChatId);
        var messageThreadId = _defaultOptions.MessageThreadId;

        return new TelegramConfig(botToken, chatId, messageThreadId);
    }

    private static string? NormalizeOption(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string? UnprotectToken(string? encryptedToken)
    {
        if (string.IsNullOrWhiteSpace(encryptedToken))
        {
            return null;
        }

        try
        {
            return _tokenProtector.Unprotect(encryptedToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Telegram bot token.");
            return null;
        }
    }

    private LeaveSettingsDto MapSettings(Domain.Entities.LeaveSettings settings)
    {
        var config = ResolveTelegramConfig(settings);
        return new LeaveSettingsDto(
            config.IsConfigured,
            config.ChatId ?? settings.TelegramChatId,
            settings.WeekendDaysBitmask,
            settings.LastTelegramError,
            settings.LastTelegramSuccessAtUtc,
            UsesAppsettingsFallback(settings));
    }

    private bool UsesAppsettingsFallback(Domain.Entities.LeaveSettings settings) =>
        (UnprotectToken(settings.EncryptedTelegramBotToken) is null && NormalizeOption(_defaultOptions.BotToken) is not null)
        || (string.IsNullOrWhiteSpace(settings.TelegramChatId) && NormalizeOption(_defaultOptions.ChatId) is not null);

    private sealed record TelegramConfig(string? BotToken, string? ChatId, int? MessageThreadId)
    {
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
    }
}
