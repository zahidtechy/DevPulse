using DevPulse.Shared.Contracts.Attendance;

namespace DevPulse.Client.Components;

/// <summary>
/// Shared rendering rules for attendance times, durations, and statuses.
/// </summary>
public static class AttendanceFormatter
{
    public const string TimeFormat = "h:mm tt";
    public const string DateTimeFormat = "ddd, MMM d, yyyy · h:mm tt";
    public const string CorrectedAtFormat = "MMM d, yyyy · h:mm tt";

    public static string FormatTime(DateTime? utc, string timeZoneId) =>
        Format(utc, timeZoneId, TimeFormat);

    public static string FormatDateTime(DateTime? utc, string timeZoneId) =>
        Format(utc, timeZoneId, DateTimeFormat);

    public static string FormatCorrectedAt(DateTime? utc, string timeZoneId) =>
        Format(utc, timeZoneId, CorrectedAtFormat);

    public static string FormatWorkHours(decimal? workHours)
    {
        if (workHours is null || workHours <= 0)
        {
            return "—";
        }

        var totalMinutes = (int)Math.Round(workHours.Value * 60m, MidpointRounding.AwayFromZero);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours > 0 ? $"{hours}h {minutes:00}m" : $"{minutes}m";
    }

    private static string Format(DateTime? utc, string timeZoneId, string format)
    {
        if (!utc.HasValue)
        {
            return "—";
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utc.Value, tz).ToString(format);
        }
        catch
        {
            return utc.Value.ToLocalTime().ToString(format);
        }
    }

    public static string CorrectionStatusLabel(AttendanceCorrectionStatusDto status) => status switch
    {
        AttendanceCorrectionStatusDto.Pending => "Pending",
        AttendanceCorrectionStatusDto.Approved => "Approved",
        AttendanceCorrectionStatusDto.Rejected => "Rejected",
        _ => status.ToString()
    };

    public static string CorrectionStatusClass(AttendanceCorrectionStatusDto status) => status switch
    {
        AttendanceCorrectionStatusDto.Pending => "pending",
        AttendanceCorrectionStatusDto.Approved => "on-time",
        AttendanceCorrectionStatusDto.Rejected => "incomplete",
        _ => string.Empty
    };

    public static string DayStatusLabel(AttendanceDayStatusDto status) => status switch
    {
        AttendanceDayStatusDto.OnTime => "On time",
        AttendanceDayStatusDto.Late => "Late",
        AttendanceDayStatusDto.EarlyLeave => "Early leave",
        AttendanceDayStatusDto.Incomplete => "Incomplete",
        AttendanceDayStatusDto.Absent => "Absent",
        _ => status.ToString()
    };

    public static string DayStatusClass(AttendanceDayStatusDto status) => status switch
    {
        AttendanceDayStatusDto.OnTime => "on-time",
        AttendanceDayStatusDto.Late => "late",
        AttendanceDayStatusDto.EarlyLeave => "early-leave",
        AttendanceDayStatusDto.Incomplete => "incomplete",
        AttendanceDayStatusDto.Absent => "absent",
        _ => string.Empty
    };
}
