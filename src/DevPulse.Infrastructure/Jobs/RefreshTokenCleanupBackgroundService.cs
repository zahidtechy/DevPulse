using DevPulse.Infrastructure.Identity;
using DevPulse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevPulse.Infrastructure.Jobs;

/// <summary>
/// Removes refresh tokens that expired or were revoked more than the configured retention
/// period ago. Runs shortly after startup and then once per configured interval (weekly by
/// default) so frequent application restarts still trigger a cleanup.
/// </summary>
public sealed class RefreshTokenCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RefreshTokenSettings> _options;
    private readonly ILogger<RefreshTokenCleanupBackgroundService> _logger;

    public RefreshTokenCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RefreshTokenSettings> options,
        ILogger<RefreshTokenCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Refresh token cleanup background service started.");

        // Give the host time to finish startup and apply migrations.
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupAsync(stoppingToken);

            // Re-read the interval each cycle so a configuration change takes effect
            // without restarting the host.
            var intervalDays = Math.Max(1, _options.CurrentValue.CleanupIntervalDays);
            var interval = TimeSpan.FromDays(intervalDays);
            _logger.LogInformation("Next refresh token cleanup scheduled in {Interval}.", interval);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Refresh token cleanup background service stopped.");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retentionDays = Math.Max(1, _options.CurrentValue.CleanupRetentionDays);
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DevPulseDbContext>();

            // Expired tokens are deleted after the retention window. Revoked tokens are
            // deleted after the same window even if their original expiry is still in the
            // future (for example, a long-lived "remember me" session revoked by an admin).
            var deleted = await dbContext.RefreshTokens
                .Where(token => token.ExpiresAtUtc < cutoff
                    || (token.RevokedAtUtc != null && token.RevokedAtUtc < cutoff))
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Removed {Count} refresh token(s) expired or revoked before {Cutoff:O} (retention: {RetentionDays} day(s)).",
                deleted,
                cutoff,
                retentionDays);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed cleanup must not stop the worker; it runs again next cycle.
            _logger.LogError(ex, "Refresh token cleanup failed.");
        }
    }
}
