namespace DevPulse.Application.Services.Attendance;

public sealed class AttendanceStatusCalculator
{
    public AttendanceDayStatusDto ComputeStatus(
        AttendanceRecord? record,
        AttendanceSettings settings,
        TimeZoneInfo timeZone)
    {
        if (record is null || (!record.PunchInUtc.HasValue && !record.PunchOutUtc.HasValue))
        {
            return AttendanceDayStatusDto.Absent;
        }

        if (!record.PunchInUtc.HasValue || !record.PunchOutUtc.HasValue)
        {
            return AttendanceDayStatusDto.Incomplete;
        }

        var punchInLocal = TimeZoneInfo.ConvertTimeFromUtc(record.PunchInUtc.Value, timeZone);
        var punchOutLocal = TimeZoneInfo.ConvertTimeFromUtc(record.PunchOutUtc.Value, timeZone);
        var punchInTime = TimeOnly.FromDateTime(punchInLocal);
        var punchOutTime = TimeOnly.FromDateTime(punchOutLocal);

        var isLate = punchInTime > settings.BufferStartTime;
        var isEarlyLeave = punchOutTime < settings.BufferEndTime;

        if (isLate && isEarlyLeave)
        {
            return AttendanceDayStatusDto.EarlyLeave;
        }

        if (isLate)
        {
            return AttendanceDayStatusDto.Late;
        }

        if (isEarlyLeave)
        {
            return AttendanceDayStatusDto.EarlyLeave;
        }

        return AttendanceDayStatusDto.OnTime;
    }

    public decimal? ComputeWorkHours(
        AttendanceRecord record,
        AttendanceSettings settings,
        TimeZoneInfo timeZone)
    {
        if (!record.PunchInUtc.HasValue || !record.PunchOutUtc.HasValue)
        {
            return null;
        }

        var punchInLocal = TimeZoneInfo.ConvertTimeFromUtc(record.PunchInUtc.Value, timeZone);
        var punchOutLocal = TimeZoneInfo.ConvertTimeFromUtc(record.PunchOutUtc.Value, timeZone);
        var punchInTime = TimeOnly.FromDateTime(punchInLocal);
        var punchOutTime = TimeOnly.FromDateTime(punchOutLocal);

        var effectiveIn = punchInTime <= settings.BufferStartTime
            ? settings.WorkStartTime
            : punchInTime;

        var effectiveOut = punchOutTime >= settings.BufferEndTime
            ? settings.WorkEndTime
            : punchOutTime;

        if (effectiveOut <= effectiveIn)
        {
            return null;
        }

        var duration = effectiveOut.ToTimeSpan() - effectiveIn.ToTimeSpan();
        return Math.Round((decimal)duration.TotalHours, 2);
    }

    public AttendanceDayStatusDto MapStatus(AttendanceDayStatus status) => status switch
    {
        AttendanceDayStatus.OnTime => AttendanceDayStatusDto.OnTime,
        AttendanceDayStatus.Late => AttendanceDayStatusDto.Late,
        AttendanceDayStatus.EarlyLeave => AttendanceDayStatusDto.EarlyLeave,
        AttendanceDayStatus.Incomplete => AttendanceDayStatusDto.Incomplete,
        AttendanceDayStatus.Absent => AttendanceDayStatusDto.Absent,
        _ => AttendanceDayStatusDto.Absent
    };

    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public DateOnly GetOfficeToday(TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        return DateOnly.FromDateTime(localNow);
    }

    public TimeOnly GetPunchInEarliestTime(AttendanceSettings settings) =>
        SubtractMinutes(settings.WorkStartTime, settings.PunchInAllowMinutesBeforeWorkStart);

    public TimeOnly GetPunchOutEarliestTime(AttendanceSettings settings) =>
        settings.WorkEndTime;

    public TimeOnly GetPunchOutLatestTime(AttendanceSettings settings) =>
        AddMinutes(settings.WorkEndTime, settings.PunchOutAllowMinutesAfterWorkEnd);

    public bool CanPunchInNow(AttendanceSettings settings, TimeZoneInfo timeZone, DateTime utcNow)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        var currentTime = TimeOnly.FromDateTime(localNow);
        var earliest = GetPunchInEarliestTime(settings);
        return currentTime >= earliest && currentTime <= settings.WorkEndTime;
    }

    public bool CanPunchOutNow(AttendanceSettings settings, TimeZoneInfo timeZone, DateTime utcNow)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        var currentTime = TimeOnly.FromDateTime(localNow);
        return currentTime >= GetPunchOutEarliestTime(settings)
            && currentTime <= GetPunchOutLatestTime(settings);
    }

    public static bool IsWeekend(DateOnly date, int weekendDaysBitmask)
    {
        var dayBit = 1 << (int)date.DayOfWeek;
        return (weekendDaysBitmask & dayBit) != 0;
    }

    private static TimeOnly AddMinutes(TimeOnly time, int minutes)
    {
        var totalMinutes = time.Hour * 60 + time.Minute + minutes;
        if (totalMinutes >= 24 * 60)
        {
            return new TimeOnly(23, 59);
        }

        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(totalMinutes));
    }

    private static TimeOnly SubtractMinutes(TimeOnly time, int minutes)
    {
        var totalMinutes = time.Hour * 60 + time.Minute - minutes;
        if (totalMinutes <= 0)
        {
            return TimeOnly.MinValue;
        }

        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(totalMinutes));
    }
}
