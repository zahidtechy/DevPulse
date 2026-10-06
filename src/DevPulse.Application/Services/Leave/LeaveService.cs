namespace DevPulse.Application.Services.Leave;

public sealed class LeaveService : ILeaveService
{
    private const int MaxNameLength = 200;
    private const int MaxPolicyNotesLength = 1000;
    private const int MaxReasonLength = 1000;
    private const int MaxCommentLength = 1000;

    private readonly ILeaveRepository _leaveRepository;
    private readonly IDeveloperRepository _developerRepository;
    private readonly LeaveDayCalculator _dayCalculator;
    private readonly ILeaveTelegramService _telegramService;
    private readonly ILeaveTelegramNotificationQueue _telegramNotificationQueue;
    private readonly IUserEmailLookup _userEmailLookup;
    private readonly IUserDeveloperResolver _userDeveloperResolver;

    public LeaveService(
        ILeaveRepository leaveRepository,
        IDeveloperRepository developerRepository,
        LeaveDayCalculator dayCalculator,
        ILeaveTelegramService telegramService,
        ILeaveTelegramNotificationQueue telegramNotificationQueue,
        IUserEmailLookup userEmailLookup,
        IUserDeveloperResolver userDeveloperResolver)
    {
        _leaveRepository = leaveRepository;
        _developerRepository = developerRepository;
        _dayCalculator = dayCalculator;
        _telegramService = telegramService;
        _telegramNotificationQueue = telegramNotificationQueue;
        _userEmailLookup = userEmailLookup;
        _userDeveloperResolver = userDeveloperResolver;
    }

    public async Task<LeaveMeDto> GetMeAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return new LeaveMeDto(null, null, false);
        }

        return new LeaveMeDto(
            developer.Id,
            developer.Name,
            developer.IsActive,
            developer.ReportingManagerDeveloperId,
            developer.ReportingManager?.Name);
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> GetLeaveTypesAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var types = activeOnly
            ? await _leaveRepository.GetActiveLeaveTypesAsync(cancellationToken)
            : await _leaveRepository.GetAllLeaveTypesAsync(cancellationToken);

        return types.Select(MapLeaveType).ToList();
    }

    public async Task<Result<LeaveTypeDto>> GetLeaveTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(id, cancellationToken);
        return leaveType is null
            ? Result<LeaveTypeDto>.Failure("Leave type was not found.")
            : Result<LeaveTypeDto>.Success(MapLeaveType(leaveType));
    }

    public async Task<Result<LeaveTypeDto>> CreateLeaveTypeAsync(CreateLeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateLeaveType(request.Name, request.DaysPerYear, request.PolicyNotes);
        if (validation.IsFailure)
        {
            return Result<LeaveTypeDto>.Failure(validation.Error!);
        }

        var leaveType = new LeaveType
        {
            Name = request.Name.Trim(),
            DaysPerYear = request.DaysPerYear,
            CountingMode = MapCountingMode(request.CountingMode),
            PolicyNotes = NormalizeOptionalText(request.PolicyNotes),
            IsActive = request.IsActive
        };

        await _leaveRepository.AddLeaveTypeAsync(leaveType, cancellationToken);
        return Result<LeaveTypeDto>.Success(MapLeaveType(leaveType));
    }

    public async Task<Result<LeaveTypeDto>> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeRequest request, CancellationToken cancellationToken = default)
    {
        var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(id, cancellationToken);
        if (leaveType is null)
        {
            return Result<LeaveTypeDto>.Failure("Leave type was not found.");
        }

        var validation = ValidateLeaveType(request.Name, request.DaysPerYear, request.PolicyNotes);
        if (validation.IsFailure)
        {
            return Result<LeaveTypeDto>.Failure(validation.Error!);
        }

        leaveType.Name = request.Name.Trim();
        leaveType.DaysPerYear = request.DaysPerYear;
        leaveType.CountingMode = MapCountingMode(request.CountingMode);
        leaveType.PolicyNotes = NormalizeOptionalText(request.PolicyNotes);
        leaveType.IsActive = request.IsActive;

        await _leaveRepository.UpdateLeaveTypeAsync(leaveType, cancellationToken);
        return Result<LeaveTypeDto>.Success(MapLeaveType(leaveType));
    }

    public async Task<Result> DeleteLeaveTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _leaveRepository.DeleteLeaveTypeAsync(id, cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<LeaveColleagueDto>> GetColleaguesAsync(CancellationToken cancellationToken = default)
    {
        var developers = await _developerRepository.GetActiveWithEmailAsync(cancellationToken);
        var userEmails = await _userEmailLookup.GetActiveUserEmailsAsync(cancellationToken);

        return developers
            .Select(d => new LeaveColleagueDto(
                d.Id,
                d.Name,
                d.Email,
                d.Email is not null && userEmails.Contains(d.Email.ToLower())))
            .ToList();
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(string userEmail, int year, CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return [];
        }

        var leaveTypes = await _leaveRepository.GetActiveLeaveTypesAsync(cancellationToken);
        var balances = new List<LeaveBalanceDto>(leaveTypes.Count);

        foreach (var leaveType in leaveTypes)
        {
            var applications = await _leaveRepository.GetApplicationsForBalanceAsync(
                developer.Id,
                leaveType.Id,
                year,
                cancellationToken);

            decimal usedDays = 0;
            decimal pendingDays = 0;
            foreach (var app in applications)
            {
                if (app.Status == LeaveApplicationStatus.Approved)
                {
                    usedDays += app.RequestedDays;
                }
                else if (app.Status == LeaveApplicationStatus.Pending)
                {
                    pendingDays += app.RequestedDays;
                }
            }

            balances.Add(new LeaveBalanceDto(
                leaveType.Id,
                leaveType.Name,
                leaveType.DaysPerYear,
                MapCountingModeDto(leaveType.CountingMode),
                usedDays,
                pendingDays,
                leaveType.DaysPerYear - usedDays - pendingDays,
                leaveType.PolicyNotes));
        }

        return balances;
    }

    public async Task<LeaveAnalyticsSummaryDto> GetTeamAnalyticsAsync(int year, CancellationToken cancellationToken = default)
    {
        var developers = await _developerRepository.GetActiveWithEmailAsync(cancellationToken);
        var leaveTypes = await _leaveRepository.GetActiveLeaveTypesAsync(cancellationToken);
        var applications = await _leaveRepository.GetApplicationsForTeamBalanceAsync(year, cancellationToken);

        var balances = new List<DeveloperLeaveBalanceDto>(developers.Count * Math.Max(leaveTypes.Count, 1));
        decimal totalUsed = 0;
        decimal totalPending = 0;
        decimal totalRemaining = 0;

        foreach (var developer in developers)
        {
            foreach (var leaveType in leaveTypes)
            {
                decimal usedDays = 0;
                decimal pendingDays = 0;

                foreach (var app in applications)
                {
                    if (app.ApplicantDeveloperId != developer.Id || app.LeaveTypeId != leaveType.Id)
                    {
                        continue;
                    }

                    if (app.Status == LeaveApplicationStatus.Approved)
                    {
                        usedDays += app.RequestedDays;
                    }
                    else if (app.Status == LeaveApplicationStatus.Pending)
                    {
                        pendingDays += app.RequestedDays;
                    }
                }

                var remainingDays = leaveType.DaysPerYear - usedDays - pendingDays;
                totalUsed += usedDays;
                totalPending += pendingDays;
                totalRemaining += remainingDays;

                balances.Add(new DeveloperLeaveBalanceDto(
                    developer.Id,
                    developer.Name,
                    leaveType.Id,
                    leaveType.Name,
                    leaveType.DaysPerYear,
                    usedDays,
                    pendingDays,
                    remainingDays));
            }
        }

        return new LeaveAnalyticsSummaryDto(
            year,
            totalUsed,
            totalPending,
            totalRemaining,
            developers.Count,
            balances);
    }

    public async Task<Result<LeaveDayCountDto>> CalculateDaysAsync(LeaveDayCountRequest request, CancellationToken cancellationToken = default)
    {
        var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(request.LeaveTypeId, cancellationToken);
        if (leaveType is null)
        {
            return Result<LeaveDayCountDto>.Failure("Leave type was not found.");
        }

        if (request.ToDate < request.FromDate)
        {
            return Result<LeaveDayCountDto>.Failure("To date must be on or after from date.");
        }

        var weekendBitmask = await _telegramService.GetWeekendDaysBitmaskAsync(cancellationToken);
        var days = await _dayCalculator.CalculateDaysAsync(
            MapCountingModeDto(leaveType.CountingMode),
            request.FromDate,
            request.ToDate,
            weekendBitmask,
            cancellationToken);

        return Result<LeaveDayCountDto>.Success(new LeaveDayCountDto(days));
    }

    public async Task<Result<LeaveApplicationDto>> SubmitApplicationAsync(
        string userEmail,
        CreateLeaveApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var applicant = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (applicant is null || !applicant.IsActive)
        {
            return Result<LeaveApplicationDto>.Failure("Your login email is not linked to an active developer record.");
        }

        var leaveType = await _leaveRepository.GetLeaveTypeByIdAsync(request.LeaveTypeId, cancellationToken);
        if (leaveType is null || !leaveType.IsActive)
        {
            return Result<LeaveApplicationDto>.Failure("Leave type was not found or is inactive.");
        }

        if (request.ApproverDeveloperId == applicant.Id)
        {
            return Result<LeaveApplicationDto>.Failure("You cannot select yourself as the approver.");
        }

        if (applicant.ReportingManagerDeveloperId is not Guid assignedManagerId)
        {
            return Result<LeaveApplicationDto>.Failure(
                "No reporting manager is assigned to your developer profile. Ask an administrator to assign one before applying for leave.");
        }

        if (request.ApproverDeveloperId != assignedManagerId)
        {
            return Result<LeaveApplicationDto>.Failure("Leave applications must be submitted to your assigned reporting manager.");
        }

        var approver = await _developerRepository.GetByIdAsync(assignedManagerId, cancellationToken);
        if (approver is null || !approver.IsActive)
        {
            return Result<LeaveApplicationDto>.Failure("Selected approver was not found or is inactive.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result<LeaveApplicationDto>.Failure("Reason is required.");
        }

        if (request.Reason.Trim().Length > MaxReasonLength)
        {
            return Result<LeaveApplicationDto>.Failure($"Reason cannot exceed {MaxReasonLength} characters.");
        }

        if (request.ToDate < request.FromDate)
        {
            return Result<LeaveApplicationDto>.Failure("To date must be on or after from date.");
        }

        var weekendBitmask = await _telegramService.GetWeekendDaysBitmaskAsync(cancellationToken);
        var requestedDays = await _dayCalculator.CalculateDaysAsync(
            MapCountingModeDto(leaveType.CountingMode),
            request.FromDate,
            request.ToDate,
            weekendBitmask,
            cancellationToken);

        if (requestedDays <= 0)
        {
            return Result<LeaveApplicationDto>.Failure("The selected date range contains no leave days.");
        }

        if (await _leaveRepository.HasOverlappingLeaveAsync(applicant.Id, request.FromDate, request.ToDate, null, cancellationToken))
        {
            return Result<LeaveApplicationDto>.Failure("You already have pending or approved leave overlapping these dates.");
        }

        var year = request.FromDate.Year;
        var balanceResult = await ValidateBalanceAsync(applicant.Id, leaveType, year, requestedDays, null, cancellationToken);
        if (balanceResult.IsFailure)
        {
            return Result<LeaveApplicationDto>.Failure(balanceResult.Error!);
        }

        var application = new LeaveApplication
        {
            ApplicantDeveloperId = applicant.Id,
            LeaveTypeId = leaveType.Id,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            RequestedDays = requestedDays,
            Reason = request.Reason.Trim(),
            ApproverDeveloperId = approver.Id,
            Status = LeaveApplicationStatus.Pending
        };

        await _leaveRepository.AddApplicationAsync(application, cancellationToken);
        application = (await _leaveRepository.GetApplicationByIdAsync(application.Id, cancellationToken))!;

        await _telegramNotificationQueue.EnqueueAsync(BuildSubmittedMessage(application), cancellationToken);

        return Result<LeaveApplicationDto>.Success(MapApplication(application));
    }

    public async Task<IReadOnlyList<LeaveApplicationDto>> GetMyApplicationsAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return [];
        }

        var applications = await _leaveRepository.GetApplicationsByApplicantAsync(developer.Id, cancellationToken);
        return applications.Select(MapApplication).ToList();
    }

    public async Task<IReadOnlyList<LeaveApplicationDto>> GetPendingForApproverAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return [];
        }

        var applications = await _leaveRepository.GetApplicationsByApproverAsync(
            developer.Id,
            LeaveApplicationStatus.Pending,
            cancellationToken);

        return applications.Select(MapApplication).ToList();
    }

    public async Task<IReadOnlyList<LeaveApplicationDto>> GetAllApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var applications = await _leaveRepository.GetAllApplicationsAsync(cancellationToken);
        return applications.Select(MapApplication).ToList();
    }

    public async Task<Result<LeaveApplicationDto>> ApproveAsync(
        string userEmail,
        Guid applicationId,
        ReviewLeaveApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ReviewAsync(userEmail, applicationId, request, approve: true, cancellationToken);
    }

    public async Task<Result<LeaveApplicationDto>> RejectAsync(
        string userEmail,
        Guid applicationId,
        ReviewLeaveApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ReviewAsync(userEmail, applicationId, request, approve: false, cancellationToken);
    }

    public async Task<Result<LeaveApplicationDto>> CancelAsync(string userEmail, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var applicant = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (applicant is null)
        {
            return Result<LeaveApplicationDto>.Failure("Your login email is not linked to a developer record.");
        }

        var application = await _leaveRepository.GetApplicationByIdAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return Result<LeaveApplicationDto>.Failure("Leave application was not found.");
        }

        if (application.ApplicantDeveloperId != applicant.Id)
        {
            return Result<LeaveApplicationDto>.Failure("You can only cancel your own applications.");
        }

        if (application.Status != LeaveApplicationStatus.Pending)
        {
            return Result<LeaveApplicationDto>.Failure("Only pending applications can be cancelled.");
        }

        application.Status = LeaveApplicationStatus.Cancelled;
        await _leaveRepository.UpdateApplicationAsync(application, cancellationToken);

        return Result<LeaveApplicationDto>.Success(MapApplication(application));
    }

    public Task<LeaveSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _telegramService.GetSettingsAsync(cancellationToken);

    public Task<Result<LeaveSettingsDto>> UpdateSettingsAsync(UpdateLeaveSettingsRequest request, CancellationToken cancellationToken = default) =>
        _telegramService.UpdateSettingsAsync(request, cancellationToken);

    public Task<Result> SendTestTelegramAsync(CancellationToken cancellationToken = default) =>
        _telegramService.SendTestAsync(cancellationToken);

    private async Task<Result<LeaveApplicationDto>> ReviewAsync(
        string userEmail,
        Guid applicationId,
        ReviewLeaveApplicationRequest request,
        bool approve,
        CancellationToken cancellationToken)
    {
        var approver = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (approver is null)
        {
            return Result<LeaveApplicationDto>.Failure("Your login email is not linked to a developer record.");
        }

        var application = await _leaveRepository.GetApplicationByIdAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return Result<LeaveApplicationDto>.Failure("Leave application was not found.");
        }

        if (application.ApproverDeveloperId != approver.Id)
        {
            return Result<LeaveApplicationDto>.Failure("You are not the designated approver for this application.");
        }

        if (application.Status != LeaveApplicationStatus.Pending)
        {
            return Result<LeaveApplicationDto>.Failure("This application has already been reviewed.");
        }

        var comment = NormalizeOptionalText(request.Comment);
        if (comment is not null && comment.Length > MaxCommentLength)
        {
            return Result<LeaveApplicationDto>.Failure($"Comment cannot exceed {MaxCommentLength} characters.");
        }

        if (approve)
        {
            var balanceResult = await ValidateBalanceAsync(
                application.ApplicantDeveloperId,
                application.LeaveType,
                application.FromDate.Year,
                application.RequestedDays,
                application.Id,
                cancellationToken);

            if (balanceResult.IsFailure)
            {
                return Result<LeaveApplicationDto>.Failure(balanceResult.Error!);
            }
        }

        application.Status = approve ? LeaveApplicationStatus.Approved : LeaveApplicationStatus.Rejected;
        application.ReviewerComment = comment;
        application.ReviewedAtUtc = DateTime.UtcNow;

        await _leaveRepository.UpdateApplicationAsync(application, cancellationToken);

        await _telegramNotificationQueue.EnqueueAsync(
            approve ? BuildApprovedMessage(application) : BuildRejectedMessage(application),
            cancellationToken);

        return Result<LeaveApplicationDto>.Success(MapApplication(application));
    }

    private async Task<Result> ValidateBalanceAsync(
        Guid developerId,
        LeaveType leaveType,
        int year,
        decimal additionalDays,
        Guid? excludeApplicationId,
        CancellationToken cancellationToken)
    {
        var applications = await _leaveRepository.GetApplicationsForBalanceAsync(
            developerId,
            leaveType.Id,
            year,
            cancellationToken);

        decimal committed = 0;
        foreach (var app in applications)
        {
            if (excludeApplicationId.HasValue && app.Id == excludeApplicationId.Value)
            {
                continue;
            }

            committed += app.RequestedDays;
        }

        if (committed + additionalDays > leaveType.DaysPerYear)
        {
            var remaining = leaveType.DaysPerYear - committed;
            return Result.Failure($"Insufficient leave balance. Remaining: {remaining:0.#} day(s).");
        }

        return Result.Success();
    }

    private Task<Developer?> ResolveDeveloperAsync(string userEmail, CancellationToken cancellationToken) =>
        _userDeveloperResolver.ResolveAsync(userEmail, cancellationToken);

    private static string BuildSubmittedMessage(LeaveApplication app) =>
        $"""
        <b>Leave Application Submitted</b>
        <b>Employee:</b> {EscapeHtml(app.ApplicantDeveloper.Name)}
        <b>Type:</b> {EscapeHtml(app.LeaveType.Name)}
        <b>Dates:</b> {FormatRange(app.FromDate, app.ToDate)} ({app.RequestedDays:0.#} day(s))
        <b>Approver:</b> {EscapeHtml(app.ApproverDeveloper.Name)}
        <b>Reason:</b> {EscapeHtml(app.Reason)}
        <b>Status:</b> Pending approval
        """;

    private static string BuildApprovedMessage(LeaveApplication app) =>
        $"""
        <b>Leave Approved</b>
        <b>Employee:</b> {EscapeHtml(app.ApplicantDeveloper.Name)}
        <b>Type:</b> {EscapeHtml(app.LeaveType.Name)}
        <b>Dates:</b> {FormatRange(app.FromDate, app.ToDate)} ({app.RequestedDays:0.#} day(s))
        <b>Approved by:</b> {EscapeHtml(app.ApproverDeveloper.Name)}
        {(string.IsNullOrWhiteSpace(app.ReviewerComment) ? "" : $"<b>Comment:</b> {EscapeHtml(app.ReviewerComment)}")}
        """;

    private static string BuildRejectedMessage(LeaveApplication app) =>
        $"""
        <b>Leave Rejected</b>
        <b>Employee:</b> {EscapeHtml(app.ApplicantDeveloper.Name)}
        <b>Type:</b> {EscapeHtml(app.LeaveType.Name)}
        <b>Dates:</b> {FormatRange(app.FromDate, app.ToDate)} ({app.RequestedDays:0.#} day(s))
        <b>Rejected by:</b> {EscapeHtml(app.ApproverDeveloper.Name)}
        {(string.IsNullOrWhiteSpace(app.ReviewerComment) ? "" : $"<b>Comment:</b> {EscapeHtml(app.ReviewerComment)}")}
        """;

    private static string FormatRange(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("dd MMM yyyy") : $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";

    private static string EscapeHtml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static Result ValidateLeaveType(string name, int daysPerYear, string? policyNotes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Leave type name is required.");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return Result.Failure($"Leave type name cannot exceed {MaxNameLength} characters.");
        }

        if (daysPerYear <= 0)
        {
            return Result.Failure("Days per year must be greater than zero.");
        }

        var notes = NormalizeOptionalText(policyNotes);
        if (notes is not null && notes.Length > MaxPolicyNotesLength)
        {
            return Result.Failure($"Policy notes cannot exceed {MaxPolicyNotesLength} characters.");
        }

        return Result.Success();
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static LeaveTypeDto MapLeaveType(LeaveType leaveType) =>
        new(
            leaveType.Id,
            leaveType.Name,
            leaveType.DaysPerYear,
            MapCountingModeDto(leaveType.CountingMode),
            leaveType.PolicyNotes,
            leaveType.IsActive,
            leaveType.CreatedAtUtc);

    private static LeaveApplicationDto MapApplication(LeaveApplication application) =>
        new(
            application.Id,
            application.ApplicantDeveloperId,
            application.ApplicantDeveloper.Name,
            application.LeaveTypeId,
            application.LeaveType.Name,
            application.FromDate,
            application.ToDate,
            application.RequestedDays,
            application.Reason,
            application.ApproverDeveloperId,
            application.ApproverDeveloper.Name,
            MapStatusDto(application.Status),
            application.ReviewerComment,
            application.ReviewedAtUtc,
            application.CreatedAtUtc);

    private static LeaveCountingMode MapCountingMode(LeaveCountingModeDto mode) =>
        mode == LeaveCountingModeDto.CalendarDays ? LeaveCountingMode.CalendarDays : LeaveCountingMode.WorkingDays;

    private static LeaveCountingModeDto MapCountingModeDto(LeaveCountingMode mode) =>
        mode == LeaveCountingMode.CalendarDays ? LeaveCountingModeDto.CalendarDays : LeaveCountingModeDto.WorkingDays;

    private static LeaveApplicationStatusDto MapStatusDto(LeaveApplicationStatus status) => status switch
    {
        LeaveApplicationStatus.Approved => LeaveApplicationStatusDto.Approved,
        LeaveApplicationStatus.Rejected => LeaveApplicationStatusDto.Rejected,
        LeaveApplicationStatus.Cancelled => LeaveApplicationStatusDto.Cancelled,
        _ => LeaveApplicationStatusDto.Pending
    };
}

