using System.Text.Json.Serialization;

namespace DevPulse.Infrastructure.Hubstaff.Models;

internal sealed class HubstaffTokenResponse
{
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}

internal sealed class HubstaffOrganizationsResponse
{
    [JsonPropertyName("organizations")]
    public List<HubstaffOrganizationJson> Organizations { get; set; } = [];
}

internal sealed class HubstaffOrganizationJson
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class HubstaffMembersResponse
{
    [JsonPropertyName("members")]
    public List<HubstaffMemberJson> Members { get; set; } = [];
}

internal sealed class HubstaffMemberJson
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

internal class HubstaffDailyActivitiesResponse
{
    [JsonPropertyName("daily_activities")]
    public List<HubstaffDailyActivityJson> DailyActivities { get; set; } = [];

    [JsonPropertyName("pagination")]
    public HubstaffPaginationJson? Pagination { get; set; }
}

internal sealed class HubstaffDailyActivityJson
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    [JsonPropertyName("tracked")]
    public int Tracked { get; set; }

    [JsonPropertyName("billable")]
    public int Billable { get; set; }

    [JsonPropertyName("idle")]
    public int Idle { get; set; }

    [JsonPropertyName("manual")]
    public int Manual { get; set; }

    [JsonPropertyName("input_tracked")]
    public int InputTracked { get; set; }

    [JsonPropertyName("overall")]
    public int Overall { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

internal sealed class HubstaffPaginationJson
{
    [JsonPropertyName("next_page_start_id")]
    public int? NextPageStartId { get; set; }
}

internal sealed class HubstaffUsersSideLoad
{
    [JsonPropertyName("users")]
    public List<HubstaffUserSideLoadJson> Users { get; set; } = [];
}

internal sealed class HubstaffUserSideLoadJson
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

internal sealed class HubstaffProjectsSideLoad
{
    [JsonPropertyName("projects")]
    public List<HubstaffProjectSideLoadJson> Projects { get; set; } = [];
}

internal sealed class HubstaffProjectSideLoadJson
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class HubstaffDailyActivitiesWithIncludesResponse : HubstaffDailyActivitiesResponse
{
    [JsonPropertyName("users")]
    public List<HubstaffUserSideLoadJson>? Users { get; set; }

    [JsonPropertyName("projects")]
    public List<HubstaffProjectSideLoadJson>? Projects { get; set; }
}
