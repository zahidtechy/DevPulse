namespace DevPulse.Application.Abstractions.Leave;

public interface ILeaveService
{
    Task<LeaveMeDto> GetMeAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<Result<LeaveTypeDto>> GetLeaveTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<LeaveTypeDto>> CreateLeaveTypeAsync(CreateLeaveTypeRequest request, CancellationToken cancellationToken = default);

    Task<Result<LeaveTypeDto>> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveColleagueDto>> GetColleaguesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(string userEmail, int year, CancellationToken cancellationToken = default);

    Task<LeaveAnalyticsSummaryDto> GetTeamAnalyticsAsync(int year, CancellationToken cancellationToken = default);

    Task<Result<LeaveDayCountDto>> CalculateDaysAsync(LeaveDayCountRequest request, CancellationToken cancellationToken = default);

    Task<Result<LeaveApplicationDto>> SubmitApplicationAsync(
        string userEmail,
        CreateLeaveApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplicationDto>> GetMyApplicationsAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplicationDto>> GetPendingForApproverAsync(string userEmail, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveApplicationDto>> GetAllApplicationsAsync(CancellationToken cancellationToken = default);

    Task<Result<LeaveApplicationDto>> ApproveAsync(
        string userEmail,
        Guid applicationId,
        ReviewLeaveApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LeaveApplicationDto>> RejectAsync(
        string userEmail,
        Guid applicationId,
        ReviewLeaveApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LeaveApplicationDto>> CancelAsync(string userEmail, Guid applicationId, CancellationToken cancellationToken = default);

    Task<LeaveSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<LeaveSettingsDto>> UpdateSettingsAsync(UpdateLeaveSettingsRequest request, CancellationToken cancellationToken = default);

    Task<Result> SendTestTelegramAsync(CancellationToken cancellationToken = default);
}
