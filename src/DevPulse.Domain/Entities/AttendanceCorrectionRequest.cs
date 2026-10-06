namespace DevPulse.Domain.Entities;

public class AttendanceCorrectionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeveloperId { get; set; }

    public Developer Developer { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public DateTime? RequestedPunchInUtc { get; set; }

    public DateTime? RequestedPunchOutUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public AttendanceCorrectionStatus Status { get; set; } = AttendanceCorrectionStatus.Pending;

    public string? ReviewerComment { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
