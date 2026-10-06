namespace DevPulse.Domain.Entities;

/// <summary>
/// Persisted ClickUp task snapshot used for daily KPI sync and DB-backed reports.
/// One row per (AccountId, TaskId). Person attribution comes from <see cref="TaskAssignmentPeriod"/>.
/// </summary>
public class SyncedTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }

    public ClickUpAccount Account { get; set; } = null!;

    public string AccountName { get; set; } = string.Empty;

    public string? ProjectName { get; set; }

    public string? FolderName { get; set; }

    public string TaskId { get; set; } = string.Empty;

    public string TaskName { get; set; } = string.Empty;

    public string? Status { get; set; }

    public string? Priority { get; set; }

    public string? ListName { get; set; }

    public string? Url { get; set; }

    public long? DateCreated { get; set; }

    public long? DateDone { get; set; }

    public long? DueDate { get; set; }

    public double? CompletionDays { get; set; }

    public bool IsSubtask { get; set; }

    public string? ParentTaskId { get; set; }

    public string? ParentTaskName { get; set; }

    public string TaskType { get; set; } = "Task";

    public bool IsCompleted { get; set; }

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}
