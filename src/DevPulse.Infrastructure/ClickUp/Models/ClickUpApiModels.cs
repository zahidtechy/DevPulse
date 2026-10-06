using System.Text.Json.Serialization;
using DevPulse.Infrastructure.ClickUp.Serialization;

namespace DevPulse.Infrastructure.ClickUp.Models;

internal sealed class ClickUpTeamsResponse
{
    [JsonPropertyName("teams")]
    public List<ClickUpTeam> Teams { get; set; } = [];
}

internal sealed class ClickUpTeam
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("members")]
    public List<ClickUpMemberItem>? Members { get; set; }
}

internal sealed class ClickUpMembersResponse
{
    [JsonPropertyName("members")]
    public List<ClickUpMemberItem>? Members { get; set; }
}

internal sealed class ClickUpMemberItem
{
    [JsonPropertyName("user")]
    public ClickUpUser User { get; set; } = new();
}

internal sealed class ClickUpUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("profilePicture")]
    public string? ProfilePicture { get; set; }
}

internal sealed class ClickUpTasksResponse
{
    [JsonPropertyName("tasks")]
    public List<ClickUpTaskItem> Tasks { get; set; } = [];
}

internal sealed class ClickUpTaskItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("custom_item_id")]
    public int? CustomItemId { get; set; }

    [JsonPropertyName("date_created")]
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public long? DateCreated { get; set; }

    [JsonPropertyName("date_closed")]
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public long? DateClosed { get; set; }

    [JsonPropertyName("date_done")]
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public long? DateDone { get; set; }

    [JsonPropertyName("due_date")]
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public long? DueDate { get; set; }

    [JsonPropertyName("status")]
    public ClickUpStatus? Status { get; set; }

    [JsonPropertyName("assignees")]
    public List<ClickUpUser>? Assignees { get; set; }

    [JsonPropertyName("priority")]
    public ClickUpPriority? Priority { get; set; }

    [JsonPropertyName("project")]
    public ClickUpProject? Project { get; set; }

    [JsonPropertyName("folder")]
    public ClickUpFolder? Folder { get; set; }

    [JsonPropertyName("list")]
    public ClickUpList? List { get; set; }
}

internal sealed class ClickUpCustomItemsResponse
{
    [JsonPropertyName("custom_items")]
    public List<ClickUpCustomItem> CustomItems { get; set; } = [];
}

internal sealed class ClickUpCustomItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ClickUpStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

internal sealed class ClickUpPriority
{
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

internal sealed class ClickUpProject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ClickUpFolder
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ClickUpList
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
