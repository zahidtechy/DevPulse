namespace DevPulse.Application.Abstractions.Leave;

/// <summary>
/// Queues leave Telegram messages for asynchronous delivery by a background worker.
/// </summary>
public interface ILeaveTelegramNotificationQueue
{
    ValueTask EnqueueAsync(string htmlMessage, CancellationToken cancellationToken = default);
}
