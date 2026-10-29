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
    /// Latest arrival time that does not count as late.
    /// Also closes punch in when <see cref="StrictPunchIn"/> is enabled.
    /// </summary>
    public TimeOnly BufferStartTime { get; set; } = new(9, 15);

    /// <summary>
    /// Earliest departure time that does not count as an early leave.
    /// Also opens punch out when <see cref="StrictPunchOut"/> is enabled.
    /// </summary>
    public TimeOnly BufferEndTime { get; set; } = new(17, 45);

    /// <summary>
    /// How many minutes before work start punch in becomes available.
    /// </summary>
    public int PunchInAllowMinutesBeforeWorkStart { get; set; } = 60;

    /// <summary>
    /// How many minutes after work end punch out remains available.
    /// Punch out closes at work end plus this allowance.
    /// </summary>
    public int PunchOutAllowMinutesAfterWorkEnd { get; set; } = 120;

    /// <summary>
    /// When true, punch in closes at the grace buffer start instead of work end.
    /// </summary>
    public bool StrictPunchIn { get; set; }

    /// <summary>
    /// When true, punch out opens at the grace buffer end instead of after punch in.
    /// </summary>
    public bool StrictPunchOut { get; set; }

    public string OfficeTimeZoneId { get; set; } = "Asia/Dhaka";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
