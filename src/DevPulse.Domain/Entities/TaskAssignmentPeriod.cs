namespace DevPulse.Domain.Entities;

/// <summary>
/// Durable assignment history for a ClickUp task. Rows are never deleted:
/// handoff only sets <see cref="UnassignedAtUtc"/>.
/// Periods are half-open: [AssignedAtUtc, UnassignedAtUtc).
/// </summary>
public class TaskAssignmentPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }

    public ClickUpAccount Account { get; set; } = null!;

    public string TaskId { get; set; } = string.Empty;

    public Guid DeveloperId { get; set; }

    public Developer Developer { get; set; } = null!;

    public DateTime AssignedAtUtc { get; set; }

    /// <summary>
    /// Null means the person is still assigned.
    /// </summary>
    public DateTime? UnassignedAtUtc { get; set; }
}
