namespace DevPulse.Shared.Contracts.Attendance;

public enum AttendanceDayStatusDto
{
    OnTime = 0,
    Late = 1,
    EarlyLeave = 2,
    Incomplete = 3,
    Absent = 4
}

public enum AttendanceCorrectionStatusDto
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum AttendanceNextActionDto
{
    PunchIn = 0,
    PunchOut = 1,
    Complete = 2
}

public record AttendanceMeDto(
    Guid? DeveloperId,
    string? DeveloperName,
    bool CanPunch,
    AttendanceNextActionDto NextAction,
    AttendanceRecordDto? TodayRecord,
    string OfficeTimeZoneId,
    bool CanPunchIn = true,
    bool CanPunchOut = true,
    TimeOnly? PunchInEarliestTime = null,
    TimeOnly? PunchOutEarliestTime = null,
    TimeOnly? PunchOutLatestTime = null);

public record AttendanceSettingsDto(
    TimeOnly WorkStartTime,
    TimeOnly WorkEndTime,
    TimeOnly BufferStartTime,
    TimeOnly BufferEndTime,
    int PunchInAllowMinutesBeforeWorkStart,
    int PunchOutAllowMinutesAfterWorkEnd,
    string OfficeTimeZoneId,
    DateTime UpdatedAtUtc);

public record UpdateAttendanceSettingsRequest(
    TimeOnly WorkStartTime,
    TimeOnly WorkEndTime,
    TimeOnly BufferStartTime,
    TimeOnly BufferEndTime,
    int PunchInAllowMinutesBeforeWorkStart,
    int PunchOutAllowMinutesAfterWorkEnd,
    string OfficeTimeZoneId);

public record AttendanceRecordDto(
    Guid Id,
    Guid DeveloperId,
    string DeveloperName,
    DateOnly WorkDate,
    DateTime? PunchInUtc,
    DateTime? PunchOutUtc,
    bool PunchInIsCorrected,
    bool PunchOutIsCorrected,
    AttendanceDayStatusDto Status,
    decimal? WorkHours);

public record AttendancePunchResultDto(
    AttendanceRecordDto Record,
    AttendanceNextActionDto NextAction);

public record AttendanceAnalyticsSummaryDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int WorkingDays,
    int OnTimeCount,
    int LateCount,
    int EarlyLeaveCount,
    int IncompleteCount,
    int AbsentCount,
    decimal OnTimePercent);

public record CreateAttendanceCorrectionRequest(
    DateOnly WorkDate,
    DateTime? RequestedPunchInUtc,
    DateTime? RequestedPunchOutUtc,
    string Reason);

public record AttendanceCorrectionRequestDto(
    Guid Id,
    Guid DeveloperId,
    string DeveloperName,
    DateOnly WorkDate,
    DateTime? RequestedPunchInUtc,
    DateTime? RequestedPunchOutUtc,
    string Reason,
    AttendanceCorrectionStatusDto Status,
    string? ReviewerComment,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc);

public record ReviewAttendanceCorrectionRequest(
    DateTime? ApprovedPunchInUtc,
    DateTime? ApprovedPunchOutUtc,
    string? ReviewerComment);

public record RejectAttendanceCorrectionRequest(string? ReviewerComment);

public record AdminUpsertAttendanceRecordRequest(
    DateTime? PunchInUtc,
    DateTime? PunchOutUtc);
