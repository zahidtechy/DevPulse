namespace DevPulse.Client.Services;

public interface IHubstaffOrganizationApiClient
{
    Task<IReadOnlyList<HubstaffOrganizationDto>> GetOrganizationsAsync(CancellationToken cancellationToken = default);

    Task<HubstaffOrganizationDto?> CreateOrganizationAsync(CreateHubstaffOrganizationRequest request, CancellationToken cancellationToken = default);

    Task<HubstaffOrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateHubstaffOrganizationRequest request, CancellationToken cancellationToken = default);

    Task<HubstaffOrganizationDto?> UpdateStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<HubstaffConnectionTestDto?> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HubstaffOrganizationDiscoveryDto>> DiscoverOrganizationsAsync(
        DiscoverHubstaffOrganizationsRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteOrganizationAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IHubstaffSyncApiClient
{
    Task<HubstaffSyncStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<HubstaffSyncResultDto?> TriggerSyncAsync(HubstaffSyncTriggerRequest? request = null, CancellationToken cancellationToken = default);
}

public interface IHubstaffAnalyticsApiClient
{
    Task<HubstaffAnalyticsResponse?> GetAnalyticsAsync(HubstaffAnalyticsRequest request, CancellationToken cancellationToken = default);
}

public sealed class HubstaffOrganizationApiClient : IHubstaffOrganizationApiClient
{
    private readonly HttpClient _httpClient;

    public HubstaffOrganizationApiClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<HubstaffOrganizationDto>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _httpClient.GetFromJsonAsync<List<HubstaffOrganizationDto>>("api/hubstaff/organizations", cancellationToken);
        return items ?? [];
    }

    public async Task<HubstaffOrganizationDto?> CreateOrganizationAsync(CreateHubstaffOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/hubstaff/organizations", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<HubstaffOrganizationDto>(cancellationToken);
    }

    public async Task<HubstaffOrganizationDto?> UpdateOrganizationAsync(Guid id, UpdateHubstaffOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/hubstaff/organizations/{id}", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<HubstaffOrganizationDto>(cancellationToken);
    }

    public async Task<HubstaffOrganizationDto?> UpdateStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync(
            $"api/hubstaff/organizations/{id}/status",
            new UpdateHubstaffOrganizationStatusRequest(isActive),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<HubstaffOrganizationDto>(cancellationToken);
    }

    public async Task<HubstaffConnectionTestDto?> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<HubstaffConnectionTestDto>($"api/hubstaff/organizations/{id}/test", cancellationToken);

    public async Task<IReadOnlyList<HubstaffOrganizationDiscoveryDto>> DiscoverOrganizationsAsync(
        DiscoverHubstaffOrganizationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/hubstaff/organizations/discover", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }

        var items = await response.Content.ReadFromJsonAsync<List<HubstaffOrganizationDiscoveryDto>>(cancellationToken);
        return items ?? [];
    }

    public async Task DeleteOrganizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/hubstaff/organizations/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }
    }

}

public sealed class HubstaffSyncApiClient : IHubstaffSyncApiClient
{
    private readonly HttpClient _httpClient;

    public HubstaffSyncApiClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<HubstaffSyncStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<HubstaffSyncStatusDto>("api/hubstaff/sync/status", cancellationToken);

    public async Task<HubstaffSyncResultDto?> TriggerSyncAsync(HubstaffSyncTriggerRequest? request = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/hubstaff/sync/trigger", request ?? new HubstaffSyncTriggerRequest(null, null, null), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<HubstaffSyncResultDto>(cancellationToken);
    }
}

public sealed class HubstaffAnalyticsApiClient : IHubstaffAnalyticsApiClient
{
    private readonly HttpClient _httpClient;

    public HubstaffAnalyticsApiClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<HubstaffAnalyticsResponse?> GetAnalyticsAsync(HubstaffAnalyticsRequest request, CancellationToken cancellationToken = default)
    {
        var developerIds = request.DeveloperIds is { Count: > 0 }
            ? string.Join("&", request.DeveloperIds.Select(id => $"developerIds={id}"))
            : string.Empty;

        var url =
            $"api/hubstaff/analytics?hubstaffOrganizationId={request.HubstaffOrganizationId}" +
            $"&fromDate={request.FromDate:yyyy-MM-dd}&toDate={request.ToDate:yyyy-MM-dd}" +
            $"&includeUnmapped={request.IncludeUnmapped.ToString().ToLowerInvariant()}";

        if (!string.IsNullOrEmpty(developerIds))
        {
            url += "&" + developerIds;
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await HubstaffApiClientHelpers.ReadHubstaffApiErrorAsync(response, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<HubstaffAnalyticsResponse>(cancellationToken);
    }
}

file static class HubstaffApiClientHelpers
{
    internal static async Task<string> ReadHubstaffApiErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseApiError(raw);
    }

    internal static string ParseApiError(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? raw;
            }
        }
        catch (JsonException)
        {
        }

        return raw;
    }
}
