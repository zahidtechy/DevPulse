namespace DevPulse.Client.Services;

public interface ILeaveApiClient
{
    Task<LeaveMeDto> GetMeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<LeaveTypeDto?> CreateLeaveTypeAsync(CreateLeaveTypeRequest request, CancellationToken cancellationToken = default);

    Task<LeaveTypeDto?> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeRequest request, CancellationToken cancellationToken = default);

    Task DeleteLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveColleagueDto>> GetColleaguesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(int year, CancellationToken cancellationToken = default);

    Task<LeaveAnalyticsSummaryDto> GetTeamAnalyticsAsync(int year, CancellationToken cancellationToken = default);

    Task<LeaveDayCountDto?> CalculateDaysAsync(LeaveDayCountRequest request, CancellationToken cancellationToken = default);

    Task<LeaveApplicationDto?> SubmitApplicationAsync(CreateLeaveApplicationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplicationDto>> GetMyApplicationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplicationDto>> GetPendingForApprovalAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplicationDto>> GetAllApplicationsAsync(CancellationToken cancellationToken = default);

    Task<LeaveApplicationDto?> ApproveAsync(Guid id, ReviewLeaveApplicationRequest request, CancellationToken cancellationToken = default);

    Task<LeaveApplicationDto?> RejectAsync(Guid id, ReviewLeaveApplicationRequest request, CancellationToken cancellationToken = default);

    Task<LeaveApplicationDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LeaveSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<LeaveSettingsDto?> UpdateSettingsAsync(UpdateLeaveSettingsRequest request, CancellationToken cancellationToken = default);

    Task SendTestTelegramAsync(CancellationToken cancellationToken = default);
}

public sealed class LeaveApiClient : ILeaveApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public LeaveApiClient(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    public async Task<LeaveMeDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var me = await _httpClient.GetFromJsonAsync<LeaveMeDto>("api/leave/me", _jsonOptions, cancellationToken);
        return me ?? new LeaveMeDto(null, null, false);
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var types = await _httpClient.GetFromJsonAsync<List<LeaveTypeDto>>($"api/leave/types?activeOnly={activeOnly}", _jsonOptions, cancellationToken);
        return types ?? [];
    }

    public async Task<LeaveTypeDto?> CreateLeaveTypeAsync(CreateLeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leave/types", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to create leave type.");
        return await response.Content.ReadFromJsonAsync<LeaveTypeDto>(_jsonOptions, cancellationToken);
    }

    public async Task<LeaveTypeDto?> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leave/types/{id}", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to update leave type.");
        return await response.Content.ReadFromJsonAsync<LeaveTypeDto>(_jsonOptions, cancellationToken);
    }

    public async Task DeleteLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leave/types/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to delete leave type.");
    }

    public async Task<IReadOnlyList<LeaveColleagueDto>> GetColleaguesAsync(CancellationToken cancellationToken = default)
    {
        var colleagues = await _httpClient.GetFromJsonAsync<List<LeaveColleagueDto>>("api/leave/colleagues", _jsonOptions, cancellationToken);
        return colleagues ?? [];
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(int year, CancellationToken cancellationToken = default)
    {
        var balances = await _httpClient.GetFromJsonAsync<List<LeaveBalanceDto>>($"api/leave/balances?year={year}", _jsonOptions, cancellationToken);
        return balances ?? [];
    }

    public async Task<LeaveAnalyticsSummaryDto> GetTeamAnalyticsAsync(int year, CancellationToken cancellationToken = default)
    {
        var analytics = await _httpClient.GetFromJsonAsync<LeaveAnalyticsSummaryDto>($"api/leave/analytics?year={year}", _jsonOptions, cancellationToken);
        return analytics ?? new LeaveAnalyticsSummaryDto(year, 0, 0, 0, 0, []);
    }

    public async Task<LeaveDayCountDto?> CalculateDaysAsync(LeaveDayCountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leave/calculate-days", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to calculate leave days.");
        return await response.Content.ReadFromJsonAsync<LeaveDayCountDto>(_jsonOptions, cancellationToken);
    }

    public async Task<LeaveApplicationDto?> SubmitApplicationAsync(CreateLeaveApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leave/applications", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to submit leave application.");
        return await response.Content.ReadFromJsonAsync<LeaveApplicationDto>(_jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveApplicationDto>> GetMyApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _httpClient.GetFromJsonAsync<List<LeaveApplicationDto>>("api/leave/applications/mine", _jsonOptions, cancellationToken);
        return apps ?? [];
    }

    public async Task<IReadOnlyList<LeaveApplicationDto>> GetPendingForApprovalAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _httpClient.GetFromJsonAsync<List<LeaveApplicationDto>>("api/leave/applications/pending-approval", _jsonOptions, cancellationToken);
        return apps ?? [];
    }

    public async Task<IReadOnlyList<LeaveApplicationDto>> GetAllApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var apps = await _httpClient.GetFromJsonAsync<List<LeaveApplicationDto>>("api/leave/applications", _jsonOptions, cancellationToken);
        return apps ?? [];
    }

    public async Task<LeaveApplicationDto?> ApproveAsync(Guid id, ReviewLeaveApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leave/applications/{id}/approve", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to approve leave application.");
        return await response.Content.ReadFromJsonAsync<LeaveApplicationDto>(_jsonOptions, cancellationToken);
    }

    public async Task<LeaveApplicationDto?> RejectAsync(Guid id, ReviewLeaveApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leave/applications/{id}/reject", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to reject leave application.");
        return await response.Content.ReadFromJsonAsync<LeaveApplicationDto>(_jsonOptions, cancellationToken);
    }

    public async Task<LeaveApplicationDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/leave/applications/{id}/cancel", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to cancel leave application.");
        return await response.Content.ReadFromJsonAsync<LeaveApplicationDto>(_jsonOptions, cancellationToken);
    }

    public async Task<LeaveSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _httpClient.GetFromJsonAsync<LeaveSettingsDto>("api/leave/settings", _jsonOptions, cancellationToken);
        return settings ?? new LeaveSettingsDto(false, null, 65, null, null, false);
    }

    public async Task<LeaveSettingsDto?> UpdateSettingsAsync(UpdateLeaveSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/leave/settings", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to update leave settings.");
        return await response.Content.ReadFromJsonAsync<LeaveSettingsDto>(_jsonOptions, cancellationToken);
    }

    public async Task SendTestTelegramAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/leave/settings/test-telegram", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to send Telegram test message.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken, string fallback)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ParseApiError(error, fallback);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("You do not have permission to perform this action.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Your session has expired. Please sign in again.");
        }

        throw new InvalidOperationException(message);
    }

    private static string ParseApiError(string rawError, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(rawError);
            if (document.RootElement.TryGetProperty("errors", out var errorsProperty)
                && errorsProperty.ValueKind == JsonValueKind.Array
                && errorsProperty.GetArrayLength() > 0)
            {
                var messages = errorsProperty.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (messages.Count > 0)
                {
                    return string.Join(" ", messages);
                }
            }

            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
            // Use raw body below.
        }

        return string.IsNullOrWhiteSpace(rawError) ? fallback : rawError;
    }
}
