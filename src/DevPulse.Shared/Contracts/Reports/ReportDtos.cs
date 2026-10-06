namespace DevPulse.Shared.Contracts.Reports;

public record DeveloperReportRequest(
    [property: JsonPropertyName("developerIds")] IReadOnlyList<Guid> DeveloperIds,
    [property: JsonPropertyName("fromDate")] DateOnly FromDate,
    [property: JsonPropertyName("toDate")] DateOnly ToDate,
    [property: JsonPropertyName("includeClosed")] bool IncludeClosed = true,
    [property: JsonPropertyName("accountIds")] IReadOnlyList<Guid>? AccountIds = null);

public record DeveloperReportResponse(
    [property: JsonPropertyName("fromDate")] DateOnly FromDate,
    [property: JsonPropertyName("toDate")] DateOnly ToDate,
    [property: JsonPropertyName("totalTasksCompleted")] int TotalTasksCompleted,
    [property: JsonPropertyName("totalInProgress")] int TotalInProgress,
    [property: JsonPropertyName("workspaceCount")] int WorkspaceCount,
    [property: JsonPropertyName("developers")] IReadOnlyList<DeveloperReportSummaryDto> Developers,
    [property: JsonPropertyName("tasks")] IReadOnlyList<DeveloperReportTaskDto> Tasks);

public record DeveloperReportSummaryDto(
    [property: JsonPropertyName("developerId")] Guid DeveloperId,
    [property: JsonPropertyName("developerName")] string DeveloperName,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("totalTasks")] int TotalTasks,
    [property: JsonPropertyName("completedCount")] int CompletedCount,
    [property: JsonPropertyName("inProgressCount")] int InProgressCount,
    [property: JsonPropertyName("childTaskCount")] int ChildTaskCount,
    [property: JsonPropertyName("workspaceCount")] int WorkspaceCount,
    [property: JsonPropertyName("projectCount")] int ProjectCount,
    [property: JsonPropertyName("overdueCount")] int OverdueCount,
    [property: JsonPropertyName("onTimeCompletedCount")] int OnTimeCompletedCount,
    [property: JsonPropertyName("averageCompletionDays")] double? AverageCompletionDays,
    [property: JsonPropertyName("byWorkspace")] IReadOnlyList<DeveloperWorkspaceBreakdownDto> ByWorkspace,
    [property: JsonPropertyName("byProject")] IReadOnlyList<DeveloperProjectBreakdownDto> ByProject);

public record DeveloperWorkspaceBreakdownDto(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("accountName")] string AccountName,
    [property: JsonPropertyName("taskCount")] int TaskCount);

public record DeveloperProjectBreakdownDto(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("accountName")] string AccountName,
    [property: JsonPropertyName("projectName")] string ProjectName,
    [property: JsonPropertyName("taskCount")] int TaskCount);

public record DeveloperReportTaskDto(
    [property: JsonPropertyName("developerId")] Guid DeveloperId,
    [property: JsonPropertyName("developerName")] string DeveloperName,
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("accountName")] string AccountName,
    [property: JsonPropertyName("projectName")] string? ProjectName,
    [property: JsonPropertyName("folderName")] string? FolderName,
    [property: JsonPropertyName("taskId")] string TaskId,
    [property: JsonPropertyName("taskName")] string TaskName,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("priority")] string? Priority,
    [property: JsonPropertyName("listName")] string? ListName,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("dateCreated")] long? DateCreated,
    [property: JsonPropertyName("dateDone")] long? DateDone,
    [property: JsonPropertyName("dueDate")] long? DueDate,
    [property: JsonPropertyName("completionDays")] double? CompletionDays,
    [property: JsonPropertyName("isSubtask")] bool IsSubtask = false,
    [property: JsonPropertyName("parentTaskId")] string? ParentTaskId = null,
    [property: JsonPropertyName("parentTaskName")] string? ParentTaskName = null,
    [property: JsonPropertyName("taskType")] string TaskType = "Task",
    [property: JsonPropertyName("isCompleted")] bool IsCompleted = true,
    [property: JsonPropertyName("assigneeIds")] IReadOnlyList<int>? AssigneeIds = null);
