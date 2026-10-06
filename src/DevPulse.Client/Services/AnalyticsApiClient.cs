namespace DevPulse.Client.Services;

public interface IAnalyticsApiClient
{
    Task<CachedAnalyticsResponse?> GetFromDatabaseAsync(
        CachedAnalyticsRequest request,
        CancellationToken cancellationToken = default);

    Task<KpiSyncStatusResponse?> GetSyncStatusAsync(CancellationToken cancellationToken = default);

    Task<KpiSyncResultDto?> RunSyncAsync(CancellationToken cancellationToken = default);
}

public sealed class AnalyticsApiClient : IAnalyticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnalyticsApiClient(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    public async Task<CachedAnalyticsResponse?> GetFromDatabaseAsync(
        CachedAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/analytics/from-database",
            request,
            _jsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to load analytics from database"));
        }

        return await response.Content.ReadFromJsonAsync<CachedAnalyticsResponse>(_jsonOptions, cancellationToken);
    }

    public async Task<KpiSyncStatusResponse?> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/analytics/sync/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<KpiSyncStatusResponse>(_jsonOptions, cancellationToken);
    }

    public async Task<KpiSyncResultDto?> RunSyncAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/analytics/sync", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to run KPI sync"));
        }

        return await response.Content.ReadFromJsonAsync<KpiSyncResultDto>(_jsonOptions, cancellationToken);
    }

    private static string ParseApiError(string rawError, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(rawError);
            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(rawError) ? fallback : rawError;
    }
}
