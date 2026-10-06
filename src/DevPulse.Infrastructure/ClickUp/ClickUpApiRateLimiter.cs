namespace DevPulse.Infrastructure.ClickUp;

/// <summary>
/// Serializes ClickUp HTTP calls and spaces them out to avoid 429 rate limits.
/// </summary>
public sealed class ClickUpApiRateLimiter
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly IOptionsMonitor<ClickUpApiOptions> _options;
    private readonly ILogger<ClickUpApiRateLimiter> _logger;
    private DateTime _nextAllowedUtc = DateTime.MinValue;

    public ClickUpApiRateLimiter(
        IOptionsMonitor<ClickUpApiOptions> options,
        ILogger<ClickUpApiRateLimiter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            if (_nextAllowedUtc > now)
            {
                var delay = _nextAllowedUtc - now;
                _logger.LogDebug("ClickUp throttle waiting {DelayMs}ms", (int)delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }

            var intervalMs = Math.Clamp(_options.CurrentValue.MinRequestIntervalMs, 0, 60_000);
            _nextAllowedUtc = DateTime.UtcNow.AddMilliseconds(intervalMs);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task CoolDownAsync(TimeSpan retryAfter, CancellationToken cancellationToken)
    {
        var waitUntil = DateTime.UtcNow.Add(retryAfter);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (waitUntil > _nextAllowedUtc)
            {
                _nextAllowedUtc = waitUntil;
            }
        }
        finally
        {
            _mutex.Release();
        }

        _logger.LogWarning("ClickUp rate limit cooldown for {RetryAfter}", retryAfter);
        await Task.Delay(retryAfter, cancellationToken);
    }
}
