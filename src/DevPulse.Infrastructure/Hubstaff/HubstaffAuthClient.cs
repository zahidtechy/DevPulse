using System.Net.Http.Json;
using DevPulse.Infrastructure.Hubstaff.Models;

namespace DevPulse.Infrastructure.Hubstaff;

public sealed class HubstaffAuthClient : IHubstaffAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HubstaffAuthClient> _logger;

    public HubstaffAuthClient(HttpClient httpClient, ILogger<HubstaffAuthClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HubstaffTokenExchangeResult> ExchangeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken.Trim()
        });

        using var response = await _httpClient.PostAsync("access_tokens", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Hubstaff token exchange failed with status {StatusCode}", (int)response.StatusCode);
            throw new HttpRequestException(
                $"Hubstaff token exchange failed ({(int)response.StatusCode}). Verify the PAT is valid and not expired.");
        }

        var token = System.Text.Json.JsonSerializer.Deserialize<HubstaffTokenResponse>(body);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new InvalidOperationException("Hubstaff token exchange returned an invalid response.");
        }

        return new HubstaffTokenExchangeResult(
            token.AccessToken,
            token.RefreshToken,
            token.ExpiresIn > 0 ? token.ExpiresIn : 86_400);
    }
}
