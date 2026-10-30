namespace DevPulse.Application.Abstractions.Persistence;

public interface IAttendanceRepository
{
    Task<AttendanceSettings?> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task EnsureSettingsAsync(CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(AttendanceSettings settings, CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetRecordAsync(Guid developerId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceDaySourceRow>> GetAttendanceDaySourceRowsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default);

    Task AddRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default);

    Task UpdateRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRequest?> GetCorrectionRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRequest?> GetPendingCorrectionRequestAsync(
        Guid developerId,
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default);

    Task<(int TotalCount, int DeveloperCount)> GetPendingCorrectionCountsAsync(
        Guid? developerId,
        bool includeTotal,
        CancellationToken cancellationToken = default);

    Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task DeleteCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);
}

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
