namespace DevPulse.Application.Abstractions.Persistence;

public sealed record AttendanceDayPageQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? DeveloperId,
    AttendanceDayStatusFilter Status,
    int PageNumber,
    int PageSize,
    TimeOnly BufferStartTime,
    TimeOnly BufferEndTime,
    string OfficeTimeZoneId);

public enum AttendanceDayStatusFilter
{
    All = 0,
    Pending = 1,
    OnTime = 2,
    Late = 3,
    EarlyLeave = 4,
    Incomplete = 5,
    Absent = 6,
    NoRecord = 7
}

public sealed record AttendanceDaySourcePage(
    IReadOnlyList<AttendanceDaySourceRow> SourceRows,
    int PageNumber,
    int PageSize,
    int TotalCount);

/// <summary>
/// Lightweight source row used to build the combined attendance-day read model.
/// Record and correction rows are returned by one UNION ALL query.
/// </summary>
public sealed record AttendanceDaySourceRow(
    Guid DeveloperId,
    string DeveloperName,
    DateOnly WorkDate,
    bool HasRecord,
    DateTime? PunchInUtc,
    DateTime? PunchOutUtc,
    bool PunchInIsCorrected,
    bool PunchOutIsCorrected,
    DateTime? PunchInCorrectedAtUtc,
    DateTime? PunchOutCorrectedAtUtc,
    Guid? CorrectionRequestId,
    AttendanceCorrectionStatus? CorrectionStatus,
    DateTime? RequestedPunchInUtc,
    DateTime? RequestedPunchOutUtc,
    DateTime? CorrectionCreatedAtUtc);
