namespace DevPulse.Domain.Entities;

public class LeaveApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicantDeveloperId { get; set; }

    public Developer ApplicantDeveloper { get; set; } = null!;

    public Guid LeaveTypeId { get; set; }

    public LeaveType LeaveType { get; set; } = null!;

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public decimal RequestedDays { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ApproverDeveloperId { get; set; }

    public Developer ApproverDeveloper { get; set; } = null!;

    public LeaveApplicationStatus Status { get; set; } = LeaveApplicationStatus.Pending;

    public string? ReviewerComment { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
