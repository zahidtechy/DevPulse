namespace DevPulse.Domain.Entities;

/// <summary>
/// Audit record for a KPI data sync job execution.
/// </summary>
public class KpiSyncRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public KpiSyncRunStatus Status { get; set; } = KpiSyncRunStatus.Running;

    public int TasksUpserted { get; set; }

    public int DeveloperCount { get; set; }

    public int AccountCount { get; set; }

    public string? ErrorMessage { get; set; }

    public bool TriggeredManually { get; set; }
}

public enum KpiSyncRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2
}
