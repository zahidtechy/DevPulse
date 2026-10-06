using DevPulse.Infrastructure.Services;

namespace DevPulse.Infrastructure.Jobs;

/// <summary>
/// Sends queued leave Telegram notifications without blocking HTTP requests.
/// </summary>
public sealed class LeaveTelegramNotificationBackgroundService : BackgroundService
{
    private readonly LeaveTelegramNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaveTelegramNotificationBackgroundService> _logger;

    public LeaveTelegramNotificationBackgroundService(
        LeaveTelegramNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<LeaveTelegramNotificationBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Leave Telegram notification worker started.");

        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }

        _logger.LogInformation("Leave Telegram notification worker stopped.");
    }

    private async Task ProcessMessageAsync(string message, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var telegramService = scope.ServiceProvider.GetRequiredService<ILeaveTelegramService>();
            await telegramService.NotifyAsync(message, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send leave Telegram notification from background queue.");
        }
    }
}
