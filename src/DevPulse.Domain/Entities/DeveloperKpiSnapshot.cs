namespace DevPulse.Domain.Entities;

/// <summary>
/// Precomputed developer KPI metrics for a synced period (generated after each daily sync).
/// </summary>
public class DeveloperKpiSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SyncRunId { get; set; }

    public KpiSyncRun SyncRun { get; set; } = null!;

    public Guid DeveloperId { get; set; }

    public Developer Developer { get; set; } = null!;

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string DeveloperName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public int TotalTasks { get; set; }

    public int CompletedCount { get; set; }

    public int InProgressCount { get; set; }

    public int ChildTaskCount { get; set; }

    public int WorkspaceCount { get; set; }

    public int ProjectCount { get; set; }

    public int OverdueCount { get; set; }

    public int OnTimeCompletedCount { get; set; }

    public double? AverageCompletionDays { get; set; }

    public double CompletionRate { get; set; }

    public double? OnTimeRate { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
