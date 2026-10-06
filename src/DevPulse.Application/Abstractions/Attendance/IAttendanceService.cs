namespace DevPulse.Application.Abstractions.Attendance;

public interface IAttendanceService
{
    Task<AttendanceMeDto> GetMeAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<Result<AttendancePunchResultDto>> PunchAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecordDto>> GetMyHistoryAsync(
        string userEmail,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<AttendanceSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<AttendanceSettingsDto>> UpdateSettingsAsync(
        UpdateAttendanceSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecordDto>> GetRecordsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default);

    Task<AttendanceAnalyticsSummaryDto> GetAnalyticsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceCorrectionRequestDto>> SubmitCorrectionRequestAsync(
        string userEmail,
        CreateAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceCorrectionRequestDto>> GetMyCorrectionRequestsAsync(
        string userEmail,
        CancellationToken cancellationToken = default);

    Task<Result> CancelCorrectionRequestAsync(
        string userEmail,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceCorrectionRequestDto>> GetPendingCorrectionRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceCorrectionRequestDto>> ApproveCorrectionRequestAsync(
        Guid requestId,
        Guid adminUserId,
        ReviewAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceCorrectionRequestDto>> RejectCorrectionRequestAsync(
        Guid requestId,
        Guid adminUserId,
        RejectAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceRecordDto>> AdminUpsertRecordAsync(
        Guid developerId,
        DateOnly workDate,
        AdminUpsertAttendanceRecordRequest request,
        CancellationToken cancellationToken = default);
}
