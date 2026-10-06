namespace DevPulse.Client.Services;

public interface IDeveloperApiClient
{
    Task<IReadOnlyList<DeveloperDto>> GetDevelopersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperDto>> GetRegistryDevelopersAsync(
        DeveloperRegistryQuery query,
        CancellationToken cancellationToken = default);

    Task<DeveloperDto?> CreateDeveloperAsync(CreateDeveloperRequest request, CancellationToken cancellationToken = default);

    Task<DeveloperDto?> UpdateDeveloperAsync(Guid id, UpdateDeveloperRequest request, CancellationToken cancellationToken = default);

    Task DeleteDeveloperAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DeveloperDto?> AddMappingByEmailAsync(
        Guid developerId,
        AddDeveloperMappingByEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<SyncDevelopersResult?> SyncFromClickUpAsync(
        SyncFromClickUpRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<DeveloperDto?> AddHubstaffMappingByEmailAsync(
        Guid developerId,
        AddDeveloperHubstaffMappingByEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<SyncFromHubstaffResult?> SyncFromHubstaffAsync(
        SyncFromHubstaffRequest? request = null,
        CancellationToken cancellationToken = default);
}

public sealed class DeveloperApiClient : IDeveloperApiClient
{
    private readonly HttpClient _httpClient;

    public DeveloperApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<DeveloperDto>> GetDevelopersAsync(CancellationToken cancellationToken = default)
    {
        var developers = await _httpClient.GetFromJsonAsync<List<DeveloperDto>>("api/developers", cancellationToken);
        return developers ?? [];
    }

    public async Task<IReadOnlyList<DeveloperDto>> GetRegistryDevelopersAsync(
        DeveloperRegistryQuery query,
        CancellationToken cancellationToken = default)
    {
        var queryString = BuildRegistryQueryString(query);
        var developers = await _httpClient.GetFromJsonAsync<List<DeveloperDto>>(
            $"api/developers/registry{queryString}",
            cancellationToken);
        return developers ?? [];
    }

    public async Task<DeveloperDto?> CreateDeveloperAsync(CreateDeveloperRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/developers", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to create developer: {error}");
        }

        return await response.Content.ReadFromJsonAsync<DeveloperDto>(cancellationToken);
    }

    public async Task<DeveloperDto?> UpdateDeveloperAsync(Guid id, UpdateDeveloperRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/developers/{id}", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to update developer: {error}");
        }

        return await response.Content.ReadFromJsonAsync<DeveloperDto>(cancellationToken);
    }

    public async Task DeleteDeveloperAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/developers/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DeveloperDto?> AddMappingByEmailAsync(
        Guid developerId,
        AddDeveloperMappingByEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/developers/{developerId}/mappings/by-email",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to map developer"));
        }

        return await response.Content.ReadFromJsonAsync<DeveloperDto>(cancellationToken);
    }

    public async Task<SyncDevelopersResult?> SyncFromClickUpAsync(
        SyncFromClickUpRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/developers/sync", request ?? new SyncFromClickUpRequest(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to sync developers"));
        }

        return await response.Content.ReadFromJsonAsync<SyncDevelopersResult>(cancellationToken);
    }

    public async Task<DeveloperDto?> AddHubstaffMappingByEmailAsync(
        Guid developerId,
        AddDeveloperHubstaffMappingByEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/developers/{developerId}/hubstaff-mappings/by-email",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to map developer to Hubstaff"));
        }

        return await response.Content.ReadFromJsonAsync<DeveloperDto>(cancellationToken);
    }

    public async Task<SyncFromHubstaffResult?> SyncFromHubstaffAsync(
        SyncFromHubstaffRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/developers/sync/hubstaff", request ?? new SyncFromHubstaffRequest(null), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to sync developers from Hubstaff"));
        }

        return await response.Content.ReadFromJsonAsync<SyncFromHubstaffResult>(cancellationToken);
    }

    private static string ParseApiError(string rawError, string fallback)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawError);
            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? fallback;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Use raw body below.
        }

        return string.IsNullOrWhiteSpace(rawError) ? fallback : rawError;
    }

    private static string BuildRegistryQueryString(DeveloperRegistryQuery query)
    {
        var parameters = new List<string>();

        if (query.ClickUpAccountId.HasValue)
        {
            parameters.Add($"clickUpAccountId={query.ClickUpAccountId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && !string.Equals(query.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            parameters.Add($"status={Uri.EscapeDataString(query.Status)}");
        }

        if (query.WorkRole.HasValue)
        {
            parameters.Add($"workRole={(int)query.WorkRole.Value}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(query.Search)}");
        }

        return parameters.Count == 0 ? string.Empty : "?" + string.Join("&", parameters);
    }
}
