namespace DevPulse.Shared.Contracts.ClickUp;

public record ClickUpAccountDto(
    Guid Id,
    string Name,
    string WorkspaceId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastValidatedAtUtc,
    string? LastValidationMessage);

public record CreateClickUpAccountRequest(
    string Name,
    string WorkspaceId,
    string AccessToken,
    IReadOnlyList<int>? OwnTeamClickUpUserIds = null);

public record PreviewClickUpWorkspaceRequest(
    string Name,
    string WorkspaceId,
    string AccessToken);

public record ClickUpWorkspacePreviewDto(
    string AccountName,
    string WorkspaceId,
    string WorkspaceName,
    string? Color,
    string Message,
    IReadOnlyList<ClickUpMemberDto> Members);

public record CreateClickUpAccountResultDto(
    ClickUpAccountDto Account,
    int DevelopersCreated,
    int DevelopersMarked,
    int MappingsAdded,
    string? ImportWarning = null);

public record UpdateClickUpAccountRequest(
    string Name,
    string WorkspaceId,
    string? AccessToken);

public record UpdateClickUpAccountStatusRequest(bool IsActive);

public record ClickUpConnectionTestDto(
    Guid AccountId,
    string AccountName,
    bool IsConnected,
    string Status,
    string? WorkspaceName,
    string Message);

public record ClickUpMemberDto(
    int ClickUpUserId,
    string Username,
    string? Email,
    string? ProfilePicture);

public record ClickUpUserLookupDto(
    int ClickUpUserId,
    string Username,
    string? Email,
    string? ProfilePicture,
    string WorkspaceId,
    Guid AccountId,
    string AccountName);

public record ClickUpWorkspaceDto(
    string Id,
    string Name,
    string? Color);

public record ClickUpCustomTaskTypeDto(
    int Id,
    string Name);

public record ClickUpTaskDto(
    string Id,
    string Name,
    string? Status,
    string? WorkspaceName,
    string? ProjectName,
    string? FolderName,
    string? ListName,
    string? Url,
    long? DateCreated,
    long? DateDone,
    long? DueDate,
    string? Priority,
    IReadOnlyList<string> AssigneeEmails,
    string? ParentTaskId = null,
    int? CustomItemId = null,
    string TaskTypeName = "Task",
    bool IsSubtask = false,
    IReadOnlyList<int>? AssigneeIds = null);

public record ClickUpTaskQueryRequest(
    Guid AccountId,
    IReadOnlyList<int>? AssigneeIds,
    DateOnly? Month,
    bool IncludeClosed = true,
    int Page = 0,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    bool IncludeSubtasks = true,
    IReadOnlyList<int>? CustomItemIds = null,
    ClickUpDateFilterMode DateFilter = ClickUpDateFilterMode.DateDone);

public enum ClickUpDateFilterMode
{
    None = 0,
    DateDone = 1,
    DateCreated = 2,
    DateUpdated = 3
}

public record ClickUpTaskQueryResponse(
    Guid AccountId,
    string AccountName,
    int Page,
    int TaskCount,
    IReadOnlyList<ClickUpTaskDto> Tasks);
