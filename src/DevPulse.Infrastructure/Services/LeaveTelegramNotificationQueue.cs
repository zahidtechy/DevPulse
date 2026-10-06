using System.Threading.Channels;

namespace DevPulse.Infrastructure.Services;

/// <summary>
/// In-memory channel queue for leave Telegram notifications.
/// </summary>
public sealed class LeaveTelegramNotificationQueue : ILeaveTelegramNotificationQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public ChannelReader<string> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(string htmlMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(htmlMessage))
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(htmlMessage, cancellationToken);
    }
}
