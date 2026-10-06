namespace DevPulse.Domain.Entities;

/// <summary>
/// Singleton company settings for attendance working hours and buffer times.
/// </summary>
public class AttendanceSettings
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public Guid Id { get; set; } = SingletonId;

    public TimeOnly WorkStartTime { get; set; } = new(9, 0);

    public TimeOnly WorkEndTime { get; set; } = new(18, 0);

    /// <summary>
    /// Latest arrival time still counted as on-time.
    /// </summary>
    public TimeOnly BufferStartTime { get; set; } = new(9, 15);

    /// <summary>
    /// Earliest departure time still counted as a full day.
    /// </summary>
    public TimeOnly BufferEndTime { get; set; } = new(17, 45);

    /// <summary>
    /// How many minutes before work start punch in becomes available.
    /// </summary>
    public int PunchInAllowMinutesBeforeWorkStart { get; set; } = 60;

    /// <summary>
    /// How many minutes after work end punch out remains available.
    /// Punch out opens at work end and closes at work end plus this allowance.
    /// </summary>
    public int PunchOutAllowMinutesAfterWorkEnd { get; set; } = 120;

    public string OfficeTimeZoneId { get; set; } = "Asia/Dhaka";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
