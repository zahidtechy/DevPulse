namespace DevPulse.Infrastructure.Jobs;

public sealed class HubstaffSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<HubstaffSyncOptions> _options;
    private readonly ILogger<HubstaffSyncBackgroundService> _logger;

    public HubstaffSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<HubstaffSyncOptions> options,
        ILogger<HubstaffSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hubstaff sync background service started");

        var options = _options.CurrentValue;
        if (options.RunOnStartup && options.Enabled)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup Hubstaff sync failed");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            options = _options.CurrentValue;
            if (!options.Enabled)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            var delay = GetDelayUntilNextRun(options);
            _logger.LogInformation("Next Hubstaff sync scheduled in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!_options.CurrentValue.Enabled)
            {
                continue;
            }

            try
            {
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled Hubstaff sync failed");
            }
        }

        _logger.LogInformation("Hubstaff sync background service stopped");
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IHubstaffSyncService>();
        var result = await syncService.SyncAsync(triggeredManually: false, cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Scheduled Hubstaff sync succeeded: {Message}", result.Value?.Message);
        }
        else
        {
            _logger.LogWarning("Scheduled Hubstaff sync failed: {Error}", result.Error);
        }
    }

    private static TimeSpan GetDelayUntilNextRun(HubstaffSyncOptions options)
    {
        var now = DateTime.UtcNow;
        var hour = Math.Clamp(options.RunHourUtc, 0, 23);
        var minute = Math.Clamp(options.RunMinuteUtc, 0, 59);
        var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        var delay = next - now;
        return delay < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : delay;
    }
}
