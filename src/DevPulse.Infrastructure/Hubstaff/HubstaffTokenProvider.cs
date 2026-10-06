using System.Collections.Concurrent;

namespace DevPulse.Infrastructure.Hubstaff;

public sealed class HubstaffTokenProvider : IHubstaffTokenProvider
{
    private sealed record CachedAccessToken(string AccessToken, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<Guid, CachedAccessToken> _cache = new();
    private readonly SemaphoreSlim _refreshMutex = new(1, 1);
    private readonly IHubstaffOrganizationRepository _organizationRepository;
    private readonly IHubstaffAuthClient _authClient;
    private readonly IHubstaffTokenProtector _tokenProtector;
    private readonly IOptionsMonitor<HubstaffApiOptions> _options;
    private readonly ILogger<HubstaffTokenProvider> _logger;

    public HubstaffTokenProvider(
        IHubstaffOrganizationRepository organizationRepository,
        IHubstaffAuthClient authClient,
        IHubstaffTokenProtector tokenProtector,
        IOptionsMonitor<HubstaffApiOptions> options,
        ILogger<HubstaffTokenProvider> logger)
    {
        _organizationRepository = organizationRepository;
        _authClient = authClient;
        _tokenProtector = tokenProtector;
        _options = options;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(
        Guid hubstaffOrganizationRecordId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(hubstaffOrganizationRecordId, out var cached)
            && cached.ExpiresAtUtc > DateTime.UtcNow.AddSeconds(_options.CurrentValue.AccessTokenRefreshBufferSeconds))
        {
            return cached.AccessToken;
        }

        await _refreshMutex.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(hubstaffOrganizationRecordId, out cached)
                && cached.ExpiresAtUtc > DateTime.UtcNow.AddSeconds(_options.CurrentValue.AccessTokenRefreshBufferSeconds))
            {
                return cached.AccessToken;
            }

            var organization = await _organizationRepository.GetByIdAsync(hubstaffOrganizationRecordId, cancellationToken)
                ?? throw new InvalidOperationException("Hubstaff organization was not found.");

            if (organization.PatExpiresAtUtc.HasValue && organization.PatExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Hubstaff PAT has expired. Update the PAT in Hubstaff settings.");
            }

            var pat = _tokenProtector.Unprotect(organization.EncryptedPersonalAccessToken);
            var exchange = await _authClient.ExchangeRefreshTokenAsync(pat, cancellationToken);

            organization.EncryptedPersonalAccessToken = _tokenProtector.Protect(exchange.RefreshToken);
            organization.PatExpiresAtUtc = DateTime.UtcNow.AddDays(90);
            await _organizationRepository.UpdateAsync(organization, cancellationToken);

            var expiresAt = DateTime.UtcNow.AddSeconds(
                Math.Max(exchange.ExpiresInSeconds - _options.CurrentValue.AccessTokenRefreshBufferSeconds, 60));

            _cache[hubstaffOrganizationRecordId] = new CachedAccessToken(exchange.AccessToken, expiresAt);
            return exchange.AccessToken;
        }
        finally
        {
            _refreshMutex.Release();
        }
    }

    public Task<HubstaffTokenExchangeResult> ExchangePatAsync(
        string personalAccessToken,
        CancellationToken cancellationToken = default) =>
        _authClient.ExchangeRefreshTokenAsync(personalAccessToken, cancellationToken);

    public void InvalidateCache(Guid hubstaffOrganizationRecordId) =>
        _cache.TryRemove(hubstaffOrganizationRecordId, out _);
}
