namespace DevPulse.Application.Abstractions.Leave;

public interface ITelegramApiClient
{
    Task<(bool Success, string? Error)> SendMessageAsync(
        string botToken,
        string chatId,
        string htmlMessage,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default);
}
