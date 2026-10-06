namespace DevPulse.Client.Services;

public interface IHolidayApiClient
{
    Task<IReadOnlyList<HolidayDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);

    Task<HolidayDto?> CreateAsync(CreateHolidayRequest request, CancellationToken cancellationToken = default);

    Task<HolidayDto?> UpdateAsync(Guid id, UpdateHolidayRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class HolidayApiClient : IHolidayApiClient
{
    private readonly HttpClient _httpClient;

    public HolidayApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<HolidayDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var holidays = await _httpClient.GetFromJsonAsync<List<HolidayDto>>($"api/holidays?year={year}", cancellationToken);
        return holidays ?? [];
    }

    public async Task<HolidayDto?> CreateAsync(CreateHolidayRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/holidays", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to create holiday: {error}");
        }

        return await response.Content.ReadFromJsonAsync<HolidayDto>(cancellationToken);
    }

    public async Task<HolidayDto?> UpdateAsync(Guid id, UpdateHolidayRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/holidays/{id}", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to update holiday: {error}");
        }

        return await response.Content.ReadFromJsonAsync<HolidayDto>(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/holidays/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to delete holiday: {error}");
        }
    }
}
