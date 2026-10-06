namespace DevPulse.Client.Services;

public interface IAttendanceApiClient
{
    Task<AttendanceMeDto> GetMeAsync(CancellationToken cancellationToken = default);

    Task<AttendancePunchResultDto?> PunchAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecordDto>> GetMyHistoryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<AttendanceSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AttendanceSettingsDto?> UpdateSettingsAsync(UpdateAttendanceSettingsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecordDto>> GetRecordsAsync(DateOnly from, DateOnly to, Guid? developerId, CancellationToken cancellationToken = default);

    Task<AttendanceAnalyticsSummaryDto> GetAnalyticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRequestDto?> SubmitCorrectionRequestAsync(CreateAttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceCorrectionRequestDto>> GetMyCorrectionRequestsAsync(CancellationToken cancellationToken = default);

    Task CancelCorrectionRequestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceCorrectionRequestDto>> GetPendingCorrectionRequestsAsync(CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRequestDto?> ApproveCorrectionRequestAsync(Guid id, ReviewAttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRequestDto?> RejectCorrectionRequestAsync(Guid id, RejectAttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task<AttendanceRecordDto?> AdminUpsertRecordAsync(Guid developerId, DateOnly workDate, AdminUpsertAttendanceRecordRequest request, CancellationToken cancellationToken = default);
}

public sealed class AttendanceApiClient : IAttendanceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AttendanceApiClient(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    public async Task<AttendanceMeDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var me = await _httpClient.GetFromJsonAsync<AttendanceMeDto>("api/attendance/me", _jsonOptions, cancellationToken);
        return me ?? new AttendanceMeDto(null, null, false, AttendanceNextActionDto.PunchIn, null, "Asia/Dhaka", true, true, null, null, null);
    }

    public async Task<AttendancePunchResultDto?> PunchAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/attendance/punch", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to record punch.");
        return await response.Content.ReadFromJsonAsync<AttendancePunchResultDto>(_jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> GetMyHistoryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var history = await _httpClient.GetFromJsonAsync<List<AttendanceRecordDto>>(
            $"api/attendance/my-history?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            _jsonOptions,
            cancellationToken);
        return history ?? [];
    }

    public async Task<AttendanceSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _httpClient.GetFromJsonAsync<AttendanceSettingsDto>("api/attendance/settings", _jsonOptions, cancellationToken);
        return settings ?? new AttendanceSettingsDto(new TimeOnly(9, 0), new TimeOnly(18, 0), new TimeOnly(9, 15), new TimeOnly(17, 45), 60, 120, "Asia/Dhaka", DateTime.UtcNow);
    }

    public async Task<AttendanceSettingsDto?> UpdateSettingsAsync(UpdateAttendanceSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/attendance/settings", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to update attendance settings.");
        return await response.Content.ReadFromJsonAsync<AttendanceSettingsDto>(_jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> GetRecordsAsync(DateOnly from, DateOnly to, Guid? developerId, CancellationToken cancellationToken = default)
    {
        var url = $"api/attendance/records?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (developerId.HasValue)
        {
            url += $"&developerId={developerId.Value}";
        }

        var records = await _httpClient.GetFromJsonAsync<List<AttendanceRecordDto>>(url, _jsonOptions, cancellationToken);
        return records ?? [];
    }

    public async Task<AttendanceAnalyticsSummaryDto> GetAnalyticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var analytics = await _httpClient.GetFromJsonAsync<AttendanceAnalyticsSummaryDto>(
            $"api/attendance/analytics?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            _jsonOptions,
            cancellationToken);
        return analytics ?? new AttendanceAnalyticsSummaryDto(from, to, 0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<AttendanceCorrectionRequestDto?> SubmitCorrectionRequestAsync(CreateAttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/attendance/correction-requests", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to submit correction request.");
        return await response.Content.ReadFromJsonAsync<AttendanceCorrectionRequestDto>(_jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequestDto>> GetMyCorrectionRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _httpClient.GetFromJsonAsync<List<AttendanceCorrectionRequestDto>>(
            "api/attendance/correction-requests/mine",
            _jsonOptions,
            cancellationToken);
        return requests ?? [];
    }

    public async Task CancelCorrectionRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/attendance/correction-requests/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to cancel correction request.");
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequestDto>> GetPendingCorrectionRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _httpClient.GetFromJsonAsync<List<AttendanceCorrectionRequestDto>>(
            "api/attendance/correction-requests/pending",
            _jsonOptions,
            cancellationToken);
        return requests ?? [];
    }

    public async Task<AttendanceCorrectionRequestDto?> ApproveCorrectionRequestAsync(Guid id, ReviewAttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/attendance/correction-requests/{id}/approve", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to approve correction request.");
        return await response.Content.ReadFromJsonAsync<AttendanceCorrectionRequestDto>(_jsonOptions, cancellationToken);
    }

    public async Task<AttendanceCorrectionRequestDto?> RejectCorrectionRequestAsync(Guid id, RejectAttendanceCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/attendance/correction-requests/{id}/reject", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to reject correction request.");
        return await response.Content.ReadFromJsonAsync<AttendanceCorrectionRequestDto>(_jsonOptions, cancellationToken);
    }

    public async Task<AttendanceRecordDto?> AdminUpsertRecordAsync(Guid developerId, DateOnly workDate, AdminUpsertAttendanceRecordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/attendance/records/{developerId}/{workDate:yyyy-MM-dd}",
            request,
            _jsonOptions,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to update attendance record.");
        return await response.Content.ReadFromJsonAsync<AttendanceRecordDto>(_jsonOptions, cancellationToken);
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
