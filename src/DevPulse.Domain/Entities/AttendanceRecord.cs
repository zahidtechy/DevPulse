namespace DevPulse.Domain.Entities;

public class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeveloperId { get; set; }

    public Developer Developer { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public DateTime? PunchInUtc { get; set; }

    public DateTime? PunchOutUtc { get; set; }

    public bool PunchInIsCorrected { get; set; }

    public bool PunchOutIsCorrected { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
