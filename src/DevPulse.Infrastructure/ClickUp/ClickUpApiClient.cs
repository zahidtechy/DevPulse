using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevPulse.Infrastructure.ClickUp.Models;

namespace DevPulse.Infrastructure.ClickUp;

/// <summary>
/// Typed HTTP client for ClickUp API v2.
/// Uses token-per-request pattern to support multiple accounts with one client instance.
/// </summary>
public sealed class ClickUpApiClient : IClickUpApiClient
{
    private const int MemberPageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ClickUpApiRateLimiter _rateLimiter;
    private readonly IOptionsMonitor<ClickUpApiOptions> _options;
    private readonly ILogger<ClickUpApiClient> _logger;

    public ClickUpApiClient(
        HttpClient httpClient,
        ClickUpApiRateLimiter rateLimiter,
        IOptionsMonitor<ClickUpApiOptions> options,
        ILogger<ClickUpApiClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClickUpWorkspaceDto>> GetAuthorizedWorkspacesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ClickUpTeamsResponse>(HttpMethod.Get, "team", accessToken, cancellationToken);
        return response.Teams
            .Select(t => new ClickUpWorkspaceDto(t.Id, t.Name, t.Color))
            .ToList();
    }

    public async Task<IReadOnlyList<ClickUpMemberDto>> GetWorkspaceMembersAsync(
        string accessToken,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        // Official ClickUp API: GET /team returns workspaces with embedded members.
        // GET /team/{id}/user is POST-only (invite) and returns 405 on GET.
        // GET /team/{id}/member is not available on all plans/API versions.
        var members = await TryGetMembersFromTeamsListAsync(accessToken, workspaceId, cancellationToken);
        if (members.Count > 0)
        {
            return members;
        }

        members = await TryGetMembersFromPaginatedEndpointAsync(
            accessToken,
            workspaceId,
            "member",
            cancellationToken);

        if (members.Count > 0)
        {
            return members;
        }

        var accessibleWorkspaces = await GetAuthorizedWorkspacesAsync(accessToken, cancellationToken);
        var matchedWorkspace = accessibleWorkspaces.FirstOrDefault(w => w.Id == workspaceId);
        if (matchedWorkspace is null)
        {
            var workspaceIds = string.Join(", ", accessibleWorkspaces.Select(w => w.Id));
            throw new HttpRequestException(
                $"Workspace '{workspaceId}' was not found for this token. Accessible workspace IDs: {workspaceIds}.",
                null,
                HttpStatusCode.NotFound);
        }

        throw new HttpRequestException(
            $"ClickUp did not return members for workspace '{workspaceId}' ({matchedWorkspace.Name}). " +
            "Large workspaces may omit members from GET /team; verify your token has workspace admin access.",
            null,
            HttpStatusCode.NotFound);
    }

    public async Task<ClickUpMemberDto?> FindWorkspaceMemberByEmailAsync(
        string accessToken,
        string workspaceId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var fromTeamsList = await TryFindMemberInTeamsListAsync(accessToken, workspaceId, normalizedEmail, cancellationToken);
        if (fromTeamsList is not null)
        {
            return fromTeamsList;
        }

        return await TryFindMemberInPaginatedEndpointAsync(
            accessToken,
            workspaceId,
            "member",
            normalizedEmail,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ClickUpCustomTaskTypeDto>> GetCustomTaskTypesAsync(
        string accessToken,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var response = await TrySendAsync<ClickUpCustomItemsResponse>(
            HttpMethod.Get,
            $"team/{workspaceId}/custom_item",
            accessToken,
            cancellationToken);

        if (response?.CustomItems is null || response.CustomItems.Count == 0)
        {
            _logger.LogDebug(
                "ClickUp custom task types unavailable for workspace {WorkspaceId}",
                workspaceId);
            return [];
        }

        return response.CustomItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new ClickUpCustomTaskTypeDto(item.Id, item.Name.Trim()))
            .ToList();
    }

    public async Task<ClickUpTaskQueryResponse> GetFilteredTasksAsync(
        string accessToken,
        string workspaceId,
        string accountName,
        Guid accountId,
        ClickUpTaskQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var queryString = BuildTaskQueryString(query);
        var response = await SendAsync<ClickUpTasksResponse>(
            HttpMethod.Get,
            $"team/{workspaceId}/task?{queryString}",
            accessToken,
            cancellationToken);

        var tasks = response.Tasks.Select(t =>
            {
                var isSubtask = !string.IsNullOrWhiteSpace(t.Parent);
                var assignees = t.Assignees ?? [];
                return new ClickUpTaskDto(
                    t.Id,
                    t.Name,
                    t.Status?.Status,
                    accountName,
                    t.Project?.Name,
                    ResolveFolderName(t.Folder?.Name),
                    t.List?.Name,
                    t.Url,
                    t.DateCreated,
                    t.DateDone ?? t.DateClosed,
                    t.DueDate,
                    t.Priority?.Priority,
                    assignees.Select(a => a.Email ?? a.Username).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList(),
                    t.Parent,
                    t.CustomItemId,
                    ResolveBuiltInTaskTypeName(t.CustomItemId),
                    isSubtask,
                    assignees.Where(a => a.Id > 0).Select(a => a.Id).Distinct().ToList());
            })
            .ToList();

        return new ClickUpTaskQueryResponse(accountId, accountName, query.Page, tasks.Count, tasks);
    }

    private static string ResolveBuiltInTaskTypeName(int? customItemId) =>
        customItemId switch
        {
            null or 0 => "Task",
            1 => "Milestone",
            _ => $"Type {customItemId.Value}"
        };

    private static string? ResolveFolderName(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return null;
        }

        return folderName.Equals("hidden", StringComparison.OrdinalIgnoreCase) ? null : folderName.Trim();
    }

    private async Task<IReadOnlyList<ClickUpMemberDto>> TryGetMembersFromPaginatedEndpointAsync(
        string accessToken,
        string workspaceId,
        string resource,
        CancellationToken cancellationToken)
    {
        var allMembers = new List<ClickUpMemberDto>();
        var page = 0;

        while (true)
        {
            var response = await TrySendAsync<ClickUpMembersResponse>(
                HttpMethod.Get,
                $"team/{workspaceId}/{resource}?page={page}",
                accessToken,
                cancellationToken);

            if (response?.Members is null || response.Members.Count == 0)
            {
                break;
            }

            allMembers.AddRange(MapMembers(response.Members));

            if (response.Members.Count < MemberPageSize)
            {
                break;
            }

            page++;
        }

        if (allMembers.Count == 0)
        {
            _logger.LogDebug(
                "ClickUp workspace member endpoint team/{WorkspaceId}/{Resource} returned no members",
                workspaceId,
                resource);
        }

        return allMembers;
    }

    private async Task<IReadOnlyList<ClickUpMemberDto>> TryGetMembersFromTeamsListAsync(
        string accessToken,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<ClickUpTeamsResponse>(HttpMethod.Get, "team", accessToken, cancellationToken);
        var team = response.Teams.FirstOrDefault(t => t.Id == workspaceId);
        if (team?.Members is null || team.Members.Count == 0)
        {
            _logger.LogDebug(
                "ClickUp GET /team did not include members for workspace {WorkspaceId}",
                workspaceId);
            return [];
        }

        return MapMembers(team.Members);
    }

    private async Task<ClickUpMemberDto?> TryFindMemberInTeamsListAsync(
        string accessToken,
        string workspaceId,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync<ClickUpTeamsResponse>(HttpMethod.Get, "team", accessToken, cancellationToken);
        var team = response.Teams.FirstOrDefault(t => t.Id == workspaceId);
        if (team?.Members is null || team.Members.Count == 0)
        {
            _logger.LogDebug(
                "ClickUp GET /team did not include members for workspace {WorkspaceId}",
                workspaceId);
            return null;
        }

        return FindMemberByEmail(MapMembers(team.Members), normalizedEmail);
    }

    private async Task<ClickUpMemberDto?> TryFindMemberInPaginatedEndpointAsync(
        string accessToken,
        string workspaceId,
        string resource,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var page = 0;

        while (true)
        {
            var response = await TrySendAsync<ClickUpMembersResponse>(
                HttpMethod.Get,
                $"team/{workspaceId}/{resource}?page={page}",
                accessToken,
                cancellationToken);

            if (response?.Members is null || response.Members.Count == 0)
            {
                break;
            }

            var members = MapMembers(response.Members);
            var match = FindMemberByEmail(members, normalizedEmail);
            if (match is not null)
            {
                return match;
            }

            if (response.Members.Count < MemberPageSize)
            {
                break;
            }

            page++;
        }

        return null;
    }

    private static ClickUpMemberDto? FindMemberByEmail(
        IEnumerable<ClickUpMemberDto> members,
        string normalizedEmail) =>
        members.FirstOrDefault(m =>
            !string.IsNullOrWhiteSpace(m.Email) &&
            string.Equals(m.Email.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ClickUpMemberDto> MapMembers(IEnumerable<ClickUpMemberItem> members) =>
        members
            .Where(m => m.User.Id > 0)
            .Select(m => new ClickUpMemberDto(
                m.User.Id,
                m.User.Username,
                m.User.Email,
                m.User.ProfilePicture))
            .ToList();

    private static string BuildTaskQueryString(ClickUpTaskQueryRequest query)
    {
        var parts = new List<string>
        {
            $"page={query.Page}",
            $"include_closed={(query.IncludeClosed ? "true" : "false")}",
            // ClickUp excludes child tasks unless subtasks=true.
            $"subtasks={(query.IncludeSubtasks ? "true" : "false")}"
        };

        if (query.DateFilter != ClickUpDateFilterMode.None
            && TryGetDateRangeBounds(query, out var startMs, out var endMs))
        {
            var (gtParam, ltParam) = query.DateFilter switch
            {
                ClickUpDateFilterMode.DateCreated => ("date_created_gt", "date_created_lt"),
                ClickUpDateFilterMode.DateUpdated => ("date_updated_gt", "date_updated_lt"),
                _ => ("date_done_gt", "date_done_lt")
            };

            parts.Add($"{gtParam}={startMs}");
            parts.Add($"{ltParam}={endMs}");
        }

        if (query.AssigneeIds is not null)
        {
            parts.AddRange(query.AssigneeIds.Select(id => $"assignees[]={id}"));
        }

        // Include standard tasks (0), milestones (1), and workspace custom types such as Bug.
        if (query.CustomItemIds is { Count: > 0 })
        {
            parts.AddRange(query.CustomItemIds.Distinct().Select(id => $"custom_items[]={id}"));
        }

        return string.Join('&', parts);
    }

    private static bool TryGetDateRangeBounds(ClickUpTaskQueryRequest query, out long startMs, out long endMs)
    {
        if (query.FromDate.HasValue && query.ToDate.HasValue)
        {
            startMs = new DateTimeOffset(query.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            endMs = new DateTimeOffset(query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            return true;
        }

        if (query.Month.HasValue)
        {
            startMs = new DateTimeOffset(query.Month.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            endMs = new DateTimeOffset(query.Month.Value.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            return true;
        }

        startMs = 0;
        endMs = 0;
        return false;
    }

    private async Task<T?> TrySendAsync<T>(
        HttpMethod method,
        string relativeUrl,
        string accessToken,
        CancellationToken cancellationToken)
        where T : class
    {
        using var response = await SendWithRateLimitAsync(method, relativeUrl, accessToken, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            _logger.LogDebug(
                "ClickUp endpoint unavailable ({StatusCode}): {Url}",
                (int)response.StatusCode,
                relativeUrl);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("ClickUp API error {StatusCode} for {Url}: {Body}", (int)response.StatusCode, relativeUrl, body);
            throw new HttpRequestException($"ClickUp API returned {(int)response.StatusCode}.", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string relativeUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRateLimitAsync(method, relativeUrl, accessToken, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("ClickUp API error {StatusCode} for {Url}: {Body}", (int)response.StatusCode, relativeUrl, body);
            throw new HttpRequestException($"ClickUp API returned {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("ClickUp API returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendWithRateLimitAsync(
        HttpMethod method,
        string relativeUrl,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var maxRetries = Math.Clamp(options.MaxRetriesOnRateLimit, 0, 20);
        var defaultRetrySeconds = Math.Clamp(options.DefaultRetryAfterSeconds, 1, 300);

        for (var attempt = 0; ; attempt++)
        {
            await _rateLimiter.WaitTurnAsync(cancellationToken);

            using var request = CreateRequest(method, relativeUrl, accessToken);
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                return response;
            }

            var retryAfter = GetRetryAfter(response) ??
                             TimeSpan.FromSeconds(defaultRetrySeconds * Math.Pow(1.5, attempt));
            response.Dispose();

            if (attempt >= maxRetries)
            {
                _logger.LogWarning(
                    "ClickUp rate limit still exceeded for {Url} after {Attempts} retries",
                    relativeUrl,
                    attempt + 1);
                throw new HttpRequestException(
                    "ClickUp API rate limit exceeded. Wait a few minutes and sync again (requests are now throttled/retried automatically).",
                    null,
                    HttpStatusCode.TooManyRequests);
            }

            // Cap individual waits so a bad header cannot stall forever.
            if (retryAfter > TimeSpan.FromMinutes(5))
            {
                retryAfter = TimeSpan.FromMinutes(5);
            }

            _logger.LogWarning(
                "ClickUp rate limited on {Url}; retry {Attempt}/{MaxRetries} after {RetryAfter}",
                relativeUrl,
                attempt + 1,
                maxRetries,
                retryAfter);

            await _rateLimiter.CoolDownAsync(retryAfter, cancellationToken);
        }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date.UtcDateTime - DateTime.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1);
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values))
        {
            var raw = values.FirstOrDefault();
            if (long.TryParse(raw, out var unixSeconds))
            {
                // ClickUp may send unix seconds or remaining seconds; treat large values as unix.
                if (unixSeconds > 1_000_000_000)
                {
                    var wait = DateTimeOffset.FromUnixTimeSeconds(unixSeconds) - DateTimeOffset.UtcNow;
                    return wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1);
                }

                if (unixSeconds > 0)
                {
                    return TimeSpan.FromSeconds(unixSeconds);
                }
            }
        }

        return null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string accessToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        // ClickUp expects the raw token in Authorization (no Bearer prefix).
        request.Headers.TryAddWithoutValidation("Authorization", accessToken);
        return request;
    }
}
