namespace DevPulse.Client.Services;

public interface IReportApiClient
{
    Task<DeveloperReportResponse?> GenerateDeveloperReportAsync(
        DeveloperReportRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportDeveloperReportToExcelAsync(
        DeveloperReportResponse report,
        CancellationToken cancellationToken = default);
}

public sealed class ReportApiClient : IReportApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReportApiClient(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    public async Task<DeveloperReportResponse?> GenerateDeveloperReportAsync(
        DeveloperReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/reports/developer-tasks",
            request,
            _jsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to generate report"));
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<DeveloperReportResponse>(_jsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Report response JSON did not match the expected format. Ensure DevPulse.Server is running and up to date.",
                ex);
        }
    }

    public async Task<byte[]> ExportDeveloperReportToExcelAsync(
        DeveloperReportResponse report,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/reports/developer-tasks/export",
            report,
            _jsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to export report"));
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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

            if (document.RootElement.TryGetProperty("title", out var titleProperty))
            {
                return titleProperty.GetString() ?? fallback;
            }

            if (document.RootElement.TryGetProperty("errors", out var errorsProperty)
                && errorsProperty.TryGetProperty("request", out var requestErrors)
                && requestErrors.GetArrayLength() > 0)
            {
                return requestErrors[0].GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
            // Use raw body below.
        }

        return string.IsNullOrWhiteSpace(rawError) ? fallback : rawError;
    }
}
