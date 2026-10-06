namespace DevPulse.Shared.Contracts.Leave;

public enum LeaveCountingModeDto
{
    WorkingDays = 0,
    CalendarDays = 1
}

public enum LeaveApplicationStatusDto
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public record LeaveTypeDto(
    Guid Id,
    string Name,
    int DaysPerYear,
    LeaveCountingModeDto CountingMode,
    string? PolicyNotes,
    bool IsActive,
    DateTime CreatedAtUtc);

public record CreateLeaveTypeRequest(
    string Name,
    int DaysPerYear,
    LeaveCountingModeDto CountingMode,
    string? PolicyNotes,
    bool IsActive = true);

public record UpdateLeaveTypeRequest(
    string Name,
    int DaysPerYear,
    LeaveCountingModeDto CountingMode,
    string? PolicyNotes,
    bool IsActive);

public record LeaveBalanceDto(
    Guid LeaveTypeId,
    string LeaveTypeName,
    int DaysPerYear,
    LeaveCountingModeDto CountingMode,
    decimal UsedDays,
    decimal PendingDays,
    decimal RemainingDays,
    string? PolicyNotes);

public record LeaveColleagueDto(
    Guid Id,
    string Name,
    string? Email,
    bool HasLogin);

public record CreateLeaveApplicationRequest(
    Guid LeaveTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    string Reason,
    Guid ApproverDeveloperId);

public record ReviewLeaveApplicationRequest(string? Comment);

public record LeaveApplicationDto(
    Guid Id,
    Guid ApplicantDeveloperId,
    string ApplicantName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal RequestedDays,
    string Reason,
    Guid ApproverDeveloperId,
    string ApproverName,
    LeaveApplicationStatusDto Status,
    string? ReviewerComment,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc);

public record LeaveDayCountRequest(
    Guid LeaveTypeId,
    DateOnly FromDate,
    DateOnly ToDate);

public record LeaveDayCountDto(decimal Days);

public record LeaveSettingsDto(
    bool HasTelegramBotToken,
    string? TelegramChatId,
    int WeekendDaysBitmask,
    string? LastTelegramError,
    DateTime? LastTelegramSuccessAtUtc,
    bool UsesAppsettingsFallback = false);

public record UpdateLeaveSettingsRequest(
    string? TelegramBotToken,
    string? TelegramChatId,
    int WeekendDaysBitmask);

public record LeaveMeDto(
    Guid? DeveloperId,
    string? DeveloperName,
    bool CanApply,
    Guid? ReportingManagerDeveloperId = null,
    string? ReportingManagerName = null);

public record DeveloperLeaveBalanceDto(
    Guid DeveloperId,
    string DeveloperName,
    Guid LeaveTypeId,
    string LeaveTypeName,
    int DaysPerYear,
    decimal UsedDays,
    decimal PendingDays,
    decimal RemainingDays);

public record LeaveAnalyticsSummaryDto(
    int Year,
    decimal TotalUsedDays,
    decimal TotalPendingDays,
    decimal TotalRemainingDays,
    int ActiveDevelopers,
    IReadOnlyList<DeveloperLeaveBalanceDto> Balances);
