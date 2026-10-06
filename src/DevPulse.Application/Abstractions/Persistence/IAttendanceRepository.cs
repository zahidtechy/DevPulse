namespace DevPulse.Application.Abstractions.Persistence;

public interface IAttendanceRepository
{
    Task<AttendanceSettings?> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task EnsureSettingsAsync(CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(AttendanceSettings settings, CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetRecordAsync(Guid developerId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetRecordByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecord>> GetRecordsByDeveloperAsync(
        Guid developerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecord>> GetRecordsAsync(
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

    Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsByDeveloperAsync(
        Guid developerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceCorrectionRequest>> GetPendingCorrectionRequestsAsync(
        CancellationToken cancellationToken = default);

    Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task DeleteCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);
}
