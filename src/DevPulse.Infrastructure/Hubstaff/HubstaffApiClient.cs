using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevPulse.Infrastructure.Hubstaff.Models;

namespace DevPulse.Infrastructure.Hubstaff;

public sealed class HubstaffApiClient : IHubstaffApiClient
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly HubstaffApiRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<HubstaffApiOptions> _options;
    private readonly ILogger<HubstaffApiClient> _logger;

    public HubstaffApiClient(
        HttpClient httpClient,
        HubstaffApiRateLimiter rateLimiter,
        IOptionsMonitor<HubstaffApiOptions> options,
        ILogger<HubstaffApiClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HubstaffOrganizationInfo>> GetOrganizationsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(HttpMethod.Get, "organizations", accessToken, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<HubstaffOrganizationsResponse>(JsonOptions, cancellationToken)
            ?? new HubstaffOrganizationsResponse();

        return payload.Organizations
            .Select(o => new HubstaffOrganizationInfo(o.Id, o.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<HubstaffMemberInfo>> GetMembersAsync(
        int organizationId,
        string accessToken,
        int? pageStartId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"organizations/{organizationId}/members{BuildPaginationQuery(pageStartId)}";
        using var response = await SendRequestAsync(HttpMethod.Get, url, accessToken, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<HubstaffMembersResponse>(JsonOptions, cancellationToken)
            ?? new HubstaffMembersResponse();

        return payload.Members
            .Select(m => new HubstaffMemberInfo(m.UserId, m.Name, m.Email))
            .ToList();
    }

    public async Task<HubstaffDailyActivitiesPage> GetDailyActivitiesAsync(
        int organizationId,
        DateOnly fromDate,
        DateOnly toDate,
        string accessToken,
        IReadOnlyList<int>? userIds = null,
        int? pageStartId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"date[start]={fromDate:yyyy-MM-dd}",
            $"date[stop]={toDate:yyyy-MM-dd}",
            "include[]=users",
            "include[]=projects",
            $"page_limit={Math.Clamp(_options.CurrentValue.DefaultPageLimit, 1, 500)}"
        };

        if (pageStartId.HasValue)
        {
            query.Add($"page_start_id={pageStartId.Value}");
        }

        if (userIds is { Count: > 0 })
        {
            foreach (var userId in userIds)
            {
                query.Add($"user_ids[]={userId}");
            }
        }

        var url = $"organizations/{organizationId}/activities/daily?{string.Join("&", query)}";
        using var response = await SendRequestAsync(HttpMethod.Get, url, accessToken, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<HubstaffDailyActivitiesWithIncludesResponse>(JsonOptions, cancellationToken)
            ?? new HubstaffDailyActivitiesWithIncludesResponse();

        var userEmails = payload.Users?.ToDictionary(u => u.Id, u => u.Email) ?? [];
        var projectNames = payload.Projects?.ToDictionary(p => p.Id, p => p.Name) ?? [];

        var activities = payload.DailyActivities
            .Select(a =>
            {
                DateOnly workDate = DateOnly.TryParse(a.Date, out var parsed) ? parsed : fromDate;
                projectNames.TryGetValue(a.ProjectId, out var projectName);
                userEmails.TryGetValue(a.UserId, out var email);

                DateTime? updatedAt = DateTime.TryParse(
                    a.UpdatedAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var updated)
                    ? updated
                    : null;

                return new HubstaffDailyActivityInfo(
                    a.Id,
                    workDate,
                    a.UserId,
                    a.ProjectId,
                    projectName,
                    a.TaskId,
                    email,
                    a.Tracked,
                    a.Billable,
                    a.Idle,
                    a.Manual,
                    a.InputTracked,
                    a.Overall,
                    updatedAt);
            })
            .ToList();

        return new HubstaffDailyActivitiesPage(activities, payload.Pagination?.NextPageStartId);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpMethod method,
        string relativeUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var retries = Math.Clamp(_options.CurrentValue.MaxRetriesOnRateLimit, 0, 10);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            await _rateLimiter.WaitTurnAsync(cancellationToken);

            using var request = new HttpRequestMessage(method, relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < retries)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(20);
                response.Dispose();
                await _rateLimiter.CoolDownAsync(retryAfter, cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                _logger.LogWarning(
                    "Hubstaff API {Method} {Url} failed with {StatusCode}: {Body}",
                    method,
                    relativeUrl,
                    (int)response.StatusCode,
                    Truncate(errorBody, 300));

                throw new HttpRequestException(
                    $"Hubstaff API request failed ({(int)response.StatusCode}) for {relativeUrl}.");
            }

            return response;
        }

        throw new InvalidOperationException("Hubstaff API request exhausted retries.");
    }

    private static string BuildPaginationQuery(int? pageStartId) =>
        pageStartId.HasValue ? $"?page_start_id={pageStartId.Value}" : string.Empty;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
