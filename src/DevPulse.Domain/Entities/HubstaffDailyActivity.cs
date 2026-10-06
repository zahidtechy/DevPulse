namespace DevPulse.Domain.Entities;

/// <summary>
/// Cached Hubstaff daily activity aggregate for analytics.
/// </summary>
public class HubstaffDailyActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HubstaffOrganizationId { get; set; }

    public HubstaffOrganization HubstaffOrganization { get; set; } = null!;

    public long HubstaffDailyActivityId { get; set; }

    public DateOnly WorkDate { get; set; }

    public int HubstaffUserId { get; set; }

    public Guid? DeveloperId { get; set; }

    public Developer? Developer { get; set; }

    public int ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public int TaskId { get; set; }

    public string? HubstaffUserEmail { get; set; }

    public int TrackedSeconds { get; set; }

    public int BillableSeconds { get; set; }

    public int IdleSeconds { get; set; }

    public int ManualSeconds { get; set; }

    public int InputTrackedSeconds { get; set; }

    public int OverallActiveSeconds { get; set; }

    public DateTime? HubstaffUpdatedAtUtc { get; set; }

    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}
