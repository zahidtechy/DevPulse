namespace DevPulse.Application.Abstractions.Persistence;

public interface ILeaveRepository
{
    Task<IReadOnlyList<LeaveType>> GetAllLeaveTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveType>> GetActiveLeaveTypesAsync(CancellationToken cancellationToken = default);

    Task<LeaveType?> GetLeaveTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);

    Task UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken = default);

    Task DeleteLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LeaveApplication?> GetApplicationByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplication>> GetApplicationsByApplicantAsync(
        Guid developerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplication>> GetApplicationsByApproverAsync(
        Guid approverDeveloperId,
        LeaveApplicationStatus? status,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplication>> GetAllApplicationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplication>> GetApplicationsForBalanceAsync(
        Guid developerId,
        Guid leaveTypeId,
        int year,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplication>> GetApplicationsForTeamBalanceAsync(
        int year,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingLeaveAsync(
        Guid applicantDeveloperId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? excludeApplicationId = null,
        CancellationToken cancellationToken = default);

    Task AddApplicationAsync(LeaveApplication application, CancellationToken cancellationToken = default);

    Task UpdateApplicationAsync(LeaveApplication application, CancellationToken cancellationToken = default);

    Task<LeaveSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(LeaveSettings settings, CancellationToken cancellationToken = default);
}
