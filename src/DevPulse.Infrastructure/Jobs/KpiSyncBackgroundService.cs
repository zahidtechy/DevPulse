namespace DevPulse.Infrastructure.Jobs;

/// <summary>
/// Runs the KPI task sync once per day at the configured UTC time.
/// </summary>
public sealed class KpiSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<KpiSyncOptions> _options;
    private readonly ILogger<KpiSyncBackgroundService> _logger;

    public KpiSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<KpiSyncOptions> options,
        ILogger<KpiSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KPI sync background service started");

        var options = _options.CurrentValue;
        if (options.RunOnStartup && options.Enabled)
        {
            // Brief delay so the host finishes startup / migrations.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                await RunSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup KPI sync failed");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            options = _options.CurrentValue;
            if (!options.Enabled)
            {
                _logger.LogDebug("KPI sync is disabled; checking again in one hour");
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
            _logger.LogInformation("Next KPI sync scheduled in {Delay}", delay);

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
                _logger.LogError(ex, "Scheduled KPI sync failed");
            }
        }

        _logger.LogInformation("KPI sync background service stopped");
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IKpiSyncService>();
        var result = await syncService.SyncAsync(triggeredManually: false, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Scheduled KPI sync succeeded: {Message}", result.Value?.Message);
        }
        else
        {
            _logger.LogWarning("Scheduled KPI sync failed: {Error}", result.Error);
        }
    }

    private static TimeSpan GetDelayUntilNextRun(KpiSyncOptions options)
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
