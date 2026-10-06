namespace DevPulse.Domain.Entities;

/// <summary>
/// Audit record for a Hubstaff daily activity sync job.
/// </summary>
public class HubstaffSyncRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public HubstaffSyncRunStatus Status { get; set; } = HubstaffSyncRunStatus.Running;

    public int ActivitiesFetched { get; set; }

    public int ActivitiesUpserted { get; set; }

    public int UnmappedUsersSkipped { get; set; }

    public int OrganizationCount { get; set; }

    public string? ErrorMessage { get; set; }

    public bool TriggeredManually { get; set; }
}
