namespace DevPulse.Domain.Entities;

public class LeaveType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public int DaysPerYear { get; set; }

    public LeaveCountingMode CountingMode { get; set; } = LeaveCountingMode.WorkingDays;

    public string? PolicyNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
