using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DevPulse.Infrastructure.Telegram;

public sealed class TelegramApiClient : ITelegramApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramApiClient> _logger;

    public TelegramApiClient(HttpClient httpClient, ILogger<TelegramApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> SendMessageAsync(
        string botToken,
        string chatId,
        string htmlMessage,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return (false, "Telegram bot token and chat ID are not configured.");
        }

        var url = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";
        var payload = new TelegramSendMessageRequest(
            chatId.Trim(),
            htmlMessage,
            "HTML",
            true,
            messageThreadId);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Telegram send failed: {StatusCode} {Body}", response.StatusCode, body);
            return (false, ParseTelegramError(body) ?? $"Telegram API returned {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram send failed.");
            return (false, ex.Message);
        }
    }

    private static string? ParseTelegramError(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("description", out var description))
            {
                return description.GetString();
            }
        }
        catch
        {
            // Ignore parse errors.
        }

        return null;
    }

    private sealed record TelegramSendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode")] string ParseMode,
        [property: JsonPropertyName("disable_web_page_preview")] bool DisableWebPagePreview,
        [property: JsonPropertyName("message_thread_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? MessageThreadId);
}
