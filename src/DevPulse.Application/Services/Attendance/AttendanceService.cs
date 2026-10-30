namespace DevPulse.Application.Services.Attendance;

public sealed class AttendanceService : IAttendanceService
{
    private const int MaxReasonLength = 1000;
    private const int MaxCommentLength = 1000;
    private const int MaxCorrectionLookbackDays = 30;

    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IDeveloperRepository _developerRepository;
    private readonly ILeaveRepository _leaveRepository;
    private readonly IHolidayRepository _holidayRepository;
    private readonly AttendanceStatusCalculator _statusCalculator;
    private readonly IUserDeveloperResolver _userDeveloperResolver;

    public AttendanceService(
        IAttendanceRepository attendanceRepository,
        IDeveloperRepository developerRepository,
        ILeaveRepository leaveRepository,
        IHolidayRepository holidayRepository,
        AttendanceStatusCalculator statusCalculator,
        IUserDeveloperResolver userDeveloperResolver)
    {
        _attendanceRepository = attendanceRepository;
        _developerRepository = developerRepository;
        _leaveRepository = leaveRepository;
        _holidayRepository = holidayRepository;
        _statusCalculator = statusCalculator;
        _userDeveloperResolver = userDeveloperResolver;
    }

    public async Task<AttendanceMeDto> GetMeAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        var settings = await GetSettingsEntityAsync(cancellationToken);
        if (developer is null)
        {
            return new AttendanceMeDto(
                null,
                null,
                false,
                AttendanceNextActionDto.PunchIn,
                null,
                settings.OfficeTimeZoneId,
                true,
                true,
                _statusCalculator.GetPunchInEarliestTime(settings),
                null,
                _statusCalculator.GetPunchOutLatestTime(settings),
                _statusCalculator.GetPunchInLatestTime(settings));
        }

        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var today = _statusCalculator.GetOfficeToday(timeZone);
        var record = await _attendanceRepository.GetRecordAsync(developer.Id, today, cancellationToken);
        var nextAction = GetNextAction(record);
        var todayDto = record is null
            ? null
            : MapRecord(record, settings, timeZone);
        var nowUtc = DateTime.UtcNow;
        var canPunchIn = nextAction != AttendanceNextActionDto.PunchIn
            || _statusCalculator.CanPunchInNow(settings, timeZone, nowUtc);
        var canPunchOut = nextAction != AttendanceNextActionDto.PunchOut
            || _statusCalculator.CanPunchOutNow(settings, timeZone, nowUtc, record);

        return new AttendanceMeDto(
            developer.Id,
            developer.Name,
            developer.IsActive,
            nextAction,
            todayDto,
            settings.OfficeTimeZoneId,
            canPunchIn,
            canPunchOut,
            _statusCalculator.GetPunchInEarliestTime(settings),
            _statusCalculator.GetPunchOutEarliestTime(settings, record, timeZone),
            _statusCalculator.GetPunchOutLatestTime(settings),
            _statusCalculator.GetPunchInLatestTime(settings));
    }

    public async Task<Result<AttendancePunchResultDto>> PunchAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return Result<AttendancePunchResultDto>.Failure("Your login email is not linked to a Developer record.");
        }

        if (!developer.IsActive)
        {
            return Result<AttendancePunchResultDto>.Failure("Your developer profile is inactive.");
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var today = _statusCalculator.GetOfficeToday(timeZone);
        var nowUtc = DateTime.UtcNow;

        var record = await _attendanceRepository.GetRecordAsync(developer.Id, today, cancellationToken);
        var nextAction = GetNextAction(record);

        if (nextAction == AttendanceNextActionDto.Complete)
        {
            return Result<AttendancePunchResultDto>.Failure("You have already completed attendance for today.");
        }

        if (record is null)
        {
            if (!_statusCalculator.CanPunchInNow(settings, timeZone, nowUtc))
            {
                var earliest = _statusCalculator.GetPunchInEarliestTime(settings);
                var latest = _statusCalculator.GetPunchInLatestTime(settings);
                return Result<AttendancePunchResultDto>.Failure(
                    $"Punch in is available from {earliest:HH:mm} until {latest:HH:mm} office time.");
            }

            record = new AttendanceRecord
            {
                DeveloperId = developer.Id,
                WorkDate = today,
                PunchInUtc = nowUtc,
                CreatedAtUtc = nowUtc
            };
            await _attendanceRepository.AddRecordAsync(record, cancellationToken);
            record.Developer = developer;
        }
        else if (nextAction == AttendanceNextActionDto.PunchOut)
        {
            if (!_statusCalculator.CanPunchOutNow(settings, timeZone, nowUtc, record))
            {
                var earliest = _statusCalculator.GetPunchOutEarliestTime(settings, record, timeZone);
                var latest = _statusCalculator.GetPunchOutLatestTime(settings);
                var earliestLabel = earliest?.ToString("HH:mm") ?? "punch in";
                return Result<AttendancePunchResultDto>.Failure(
                    $"Punch out is available after {earliestLabel} until {latest:HH:mm} office time.");
            }

            record.PunchOutUtc = nowUtc;
            await _attendanceRepository.UpdateRecordAsync(record, cancellationToken);
        }

        var updatedNext = GetNextAction(record);
        var dto = MapRecord(record, settings, timeZone);
        return Result<AttendancePunchResultDto>.Success(new AttendancePunchResultDto(dto, updatedNext));
    }

    public async Task<IReadOnlyList<AttendanceDayRowDto>> GetAttendanceDaysAsync(
        string userEmail,
        bool canViewAll,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? developerId,
        CancellationToken cancellationToken = default)
    {
        var effectiveDeveloperId = developerId;
        if (!canViewAll)
        {
            var currentDeveloper = await ResolveDeveloperAsync(userEmail, cancellationToken);
            if (currentDeveloper is null)
            {
                return [];
            }

            // The caller cannot widen or change their own scope with a query parameter.
            effectiveDeveloperId = currentDeveloper.Id;
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var sourceRows = await _attendanceRepository.GetAttendanceDaySourceRowsAsync(
            fromDate,
            toDate,
            effectiveDeveloperId,
            cancellationToken);

        return sourceRows
            .GroupBy(row => (row.DeveloperId, row.WorkDate))
            .Select(group => BuildAttendanceDayRow(group, settings, timeZone))
            .OrderByDescending(row => row.WorkDate)
            .ThenBy(row => row.DeveloperName)
            .ToList();
    }

    public async Task<AttendanceDayDetailsDto?> GetAttendanceDayDetailsAsync(
        string userEmail,
        bool canViewAll,
        Guid developerId,
        DateOnly workDate,
        CancellationToken cancellationToken = default)
    {
        Developer? developer;
        if (canViewAll)
        {
            developer = await _developerRepository.GetByIdAsync(developerId, cancellationToken);
        }
        else
        {
            developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
            if (developer?.Id != developerId)
            {
                return null;
            }
        }

        if (developer is null)
        {
            return null;
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var record = await _attendanceRepository.GetRecordAsync(developerId, workDate, cancellationToken);
        var corrections = await _attendanceRepository.GetCorrectionRequestsAsync(
            workDate,
            workDate,
            developerId,
            cancellationToken);
        var row = BuildAttendanceDayRow(
            developerId,
            workDate,
            record,
            corrections,
            developer,
            settings,
            timeZone);

        return new AttendanceDayDetailsDto(
            row,
            record is null ? null : MapRecord(record, settings, timeZone),
            corrections.Select(MapCorrectionRequest).ToList());
    }

    public async Task<AttendancePendingCorrectionCountsDto> GetPendingCorrectionCountsAsync(
        string userEmail,
        bool canViewAll,
        CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (!canViewAll && developer is null)
        {
            return new AttendancePendingCorrectionCountsDto(0, 0);
        }

        var counts = await _attendanceRepository.GetPendingCorrectionCountsAsync(
            developer?.Id,
            includeTotal: canViewAll,
            cancellationToken);

        return new AttendancePendingCorrectionCountsDto(
            canViewAll ? counts.TotalCount : counts.DeveloperCount,
            counts.DeveloperCount);
    }

    public async Task<AttendanceSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsEntityAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<Result<AttendanceSettingsDto>> UpdateSettingsAsync(
        UpdateAttendanceSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateSettings(request);
        if (validation.IsFailure)
        {
            return Result<AttendanceSettingsDto>.Failure(validation.Error!);
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        settings.WorkStartTime = request.WorkStartTime;
        settings.WorkEndTime = request.WorkEndTime;
        settings.BufferStartTime = request.BufferStartTime;
        settings.BufferEndTime = request.BufferEndTime;
        settings.PunchInAllowMinutesBeforeWorkStart = request.PunchInAllowMinutesBeforeWorkStart;
        settings.PunchOutAllowMinutesAfterWorkEnd = request.PunchOutAllowMinutesAfterWorkEnd;
        settings.StrictPunchIn = request.StrictPunchIn;
        settings.StrictPunchOut = request.StrictPunchOut;
        settings.OfficeTimeZoneId = request.OfficeTimeZoneId.Trim();
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await _attendanceRepository.UpdateSettingsAsync(settings, cancellationToken);
        return Result<AttendanceSettingsDto>.Success(MapSettings(settings));
    }

    public async Task<AttendanceAnalyticsSummaryDto> GetAnalyticsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            return new AttendanceAnalyticsSummaryDto(fromDate, toDate, 0, 0, 0, 0, 0, 0, 0);
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var leaveSettings = await _leaveRepository.GetOrCreateSettingsAsync(cancellationToken);
        var holidays = await LoadHolidayDatesAsync(fromDate, toDate, cancellationToken);
        var workingDays = EnumerateWorkingDays(fromDate, toDate, leaveSettings.WeekendDaysBitmask, holidays).ToList();

        var developers = await _developerRepository.GetAllAsync(cancellationToken);
        var activeDevelopers = developers.Where(d => d.IsActive).ToList();
        var records = await _attendanceRepository.GetRecordsAsync(fromDate, toDate, null, cancellationToken);
        var recordLookup = records.ToDictionary(r => (r.DeveloperId, r.WorkDate));

        var onTime = 0;
        var late = 0;
        var earlyLeave = 0;
        var incomplete = 0;
        var absent = 0;

        foreach (var developer in activeDevelopers)
        {
            foreach (var day in workingDays)
            {
                recordLookup.TryGetValue((developer.Id, day), out var record);
                var status = _statusCalculator.ComputeStatus(record, settings, timeZone);
                switch (status)
                {
                    case AttendanceDayStatusDto.OnTime:
                        onTime++;
                        break;
                    case AttendanceDayStatusDto.Late:
                        late++;
                        break;
                    case AttendanceDayStatusDto.EarlyLeave:
                        earlyLeave++;
                        break;
                    case AttendanceDayStatusDto.Incomplete:
                        incomplete++;
                        break;
                    case AttendanceDayStatusDto.Absent:
                        absent++;
                        break;
                }
            }
        }

        var total = onTime + late + earlyLeave + incomplete + absent;
        var onTimePercent = total == 0 ? 0 : Math.Round(onTime * 100m / total, 1);

        return new AttendanceAnalyticsSummaryDto(
            fromDate,
            toDate,
            workingDays.Count,
            onTime,
            late,
            earlyLeave,
            incomplete,
            absent,
            onTimePercent);
    }

    public async Task<Result<AttendanceCorrectionRequestDto>> SubmitCorrectionRequestAsync(
        string userEmail,
        CreateAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Your login email is not linked to a Developer record.");
        }

        var validation = ValidateCorrectionRequest(request);
        if (validation.IsFailure)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure(validation.Error!);
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var today = _statusCalculator.GetOfficeToday(timeZone);

        if (request.WorkDate > today)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Correction requests cannot be submitted for future dates.");
        }

        if (today.DayNumber - request.WorkDate.DayNumber > MaxCorrectionLookbackDays)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure($"Correction requests are limited to the last {MaxCorrectionLookbackDays} days.");
        }

        var existingPending = await _attendanceRepository.GetPendingCorrectionRequestAsync(
            developer.Id,
            request.WorkDate,
            cancellationToken);
        if (existingPending is not null)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("A pending correction request already exists for this date.");
        }

        var record = await _attendanceRepository.GetRecordAsync(developer.Id, request.WorkDate, cancellationToken);
        if (request.RequestedPunchInUtc.HasValue && record?.PunchInUtc is not null)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Punch in already exists for this date.");
        }

        if (request.RequestedPunchOutUtc.HasValue && record?.PunchOutUtc is not null)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Punch out already exists for this date.");
        }

        if (request.RequestedPunchInUtc.HasValue
            && request.RequestedPunchOutUtc.HasValue
            && request.RequestedPunchOutUtc <= request.RequestedPunchInUtc)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Punch out must be after punch in.");
        }

        var entity = new AttendanceCorrectionRequest
        {
            DeveloperId = developer.Id,
            WorkDate = request.WorkDate,
            RequestedPunchInUtc = request.RequestedPunchInUtc,
            RequestedPunchOutUtc = request.RequestedPunchOutUtc,
            Reason = request.Reason.Trim(),
            Status = AttendanceCorrectionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _attendanceRepository.AddCorrectionRequestAsync(entity, cancellationToken);
        entity.Developer = developer;
        return Result<AttendanceCorrectionRequestDto>.Success(MapCorrectionRequest(entity));
    }

    public async Task<Result> CancelCorrectionRequestAsync(
        string userEmail,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var developer = await ResolveDeveloperAsync(userEmail, cancellationToken);
        if (developer is null)
        {
            return Result.Failure("Your login email is not linked to a Developer record.");
        }

        var entity = await _attendanceRepository.GetCorrectionRequestByIdAsync(requestId, cancellationToken);
        if (entity is null || entity.DeveloperId != developer.Id)
        {
            return Result.Failure("Correction request was not found.");
        }

        if (entity.Status != AttendanceCorrectionStatus.Pending)
        {
            return Result.Failure("Only pending requests can be cancelled.");
        }

        await _attendanceRepository.DeleteCorrectionRequestAsync(entity, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<AttendanceCorrectionRequestDto>> ApproveCorrectionRequestAsync(
        Guid requestId,
        Guid adminUserId,
        ReviewAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _attendanceRepository.GetCorrectionRequestByIdAsync(requestId, cancellationToken);
        if (entity is null)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Correction request was not found.");
        }

        if (entity.Status != AttendanceCorrectionStatus.Pending)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("This request has already been reviewed.");
        }

        var punchIn = request.ApprovedPunchInUtc ?? entity.RequestedPunchInUtc;
        var punchOut = request.ApprovedPunchOutUtc ?? entity.RequestedPunchOutUtc;
        var reviewedAtUtc = DateTime.UtcNow;

        var upsertResult = await ApplyCorrectedPunchesAsync(
            entity.DeveloperId,
            entity.WorkDate,
            punchIn,
            punchOut,
            reviewedAtUtc,
            cancellationToken);
        if (upsertResult.IsFailure)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure(upsertResult.Error!);
        }

        entity.Status = AttendanceCorrectionStatus.Approved;
        entity.ReviewerComment = NormalizeOptionalText(request.ReviewerComment, MaxCommentLength);
        entity.ReviewedByUserId = adminUserId;
        entity.ReviewedAtUtc = reviewedAtUtc;

        await _attendanceRepository.UpdateCorrectionRequestAsync(entity, cancellationToken);
        return Result<AttendanceCorrectionRequestDto>.Success(MapCorrectionRequest(entity));
    }

    public async Task<Result<AttendanceCorrectionRequestDto>> RejectCorrectionRequestAsync(
        Guid requestId,
        Guid adminUserId,
        RejectAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _attendanceRepository.GetCorrectionRequestByIdAsync(requestId, cancellationToken);
        if (entity is null)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("Correction request was not found.");
        }

        if (entity.Status != AttendanceCorrectionStatus.Pending)
        {
            return Result<AttendanceCorrectionRequestDto>.Failure("This request has already been reviewed.");
        }

        entity.Status = AttendanceCorrectionStatus.Rejected;
        entity.ReviewerComment = NormalizeOptionalText(request.ReviewerComment, MaxCommentLength);
        entity.ReviewedByUserId = adminUserId;
        entity.ReviewedAtUtc = DateTime.UtcNow;

        await _attendanceRepository.UpdateCorrectionRequestAsync(entity, cancellationToken);
        return Result<AttendanceCorrectionRequestDto>.Success(MapCorrectionRequest(entity));
    }

    public async Task<Result<AttendanceRecordDto>> AdminUpsertRecordAsync(
        Guid developerId,
        DateOnly workDate,
        AdminUpsertAttendanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdAsync(developerId, cancellationToken);
        if (developer is null)
        {
            return Result<AttendanceRecordDto>.Failure("Developer was not found.");
        }

        if (request.PunchInUtc.HasValue
            && request.PunchOutUtc.HasValue
            && request.PunchOutUtc <= request.PunchInUtc)
        {
            return Result<AttendanceRecordDto>.Failure("Punch out must be after punch in.");
        }

        var applyResult = await ApplyCorrectedPunchesAsync(
            developerId,
            workDate,
            request.PunchInUtc,
            request.PunchOutUtc,
            DateTime.UtcNow,
            cancellationToken);
        if (applyResult.IsFailure)
        {
            return Result<AttendanceRecordDto>.Failure(applyResult.Error!);
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var timeZone = AttendanceStatusCalculator.ResolveTimeZone(settings.OfficeTimeZoneId);
        var record = await _attendanceRepository.GetRecordAsync(developerId, workDate, cancellationToken);
        return record is null
            ? Result<AttendanceRecordDto>.Failure("Failed to load attendance record.")
            : Result<AttendanceRecordDto>.Success(MapRecord(record, settings, timeZone));
    }

    private async Task<Result> ApplyCorrectedPunchesAsync(
        Guid developerId,
        DateOnly workDate,
        DateTime? punchInUtc,
        DateTime? punchOutUtc,
        DateTime correctedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!punchInUtc.HasValue && !punchOutUtc.HasValue)
        {
            return Result.Failure("At least one punch time must be provided.");
        }

        var record = await _attendanceRepository.GetRecordAsync(developerId, workDate, cancellationToken);
        var isNew = record is null;
        if (record is null)
        {
            record = new AttendanceRecord
            {
                DeveloperId = developerId,
                WorkDate = workDate,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        if (punchInUtc.HasValue && record.PunchInUtc != punchInUtc)
        {
            record.PunchInUtc = punchInUtc;
            record.PunchInIsCorrected = true;
            record.PunchInCorrectedAtUtc = correctedAtUtc;
        }

        if (punchOutUtc.HasValue && record.PunchOutUtc != punchOutUtc)
        {
            record.PunchOutUtc = punchOutUtc;
            record.PunchOutIsCorrected = true;
            record.PunchOutCorrectedAtUtc = correctedAtUtc;
        }

        if (record.PunchInUtc.HasValue
            && record.PunchOutUtc.HasValue
            && record.PunchOutUtc <= record.PunchInUtc)
        {
            return Result.Failure("Punch out must be after punch in.");
        }

        if (isNew)
        {
            await _attendanceRepository.AddRecordAsync(record, cancellationToken);
        }
        else
        {
            await _attendanceRepository.UpdateRecordAsync(record, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<AttendanceSettings> GetSettingsEntityAsync(CancellationToken cancellationToken)
    {
        var settings = await _attendanceRepository.GetSettingsAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        await _attendanceRepository.EnsureSettingsAsync(cancellationToken);
        return await _attendanceRepository.GetSettingsAsync(cancellationToken)
            ?? throw new InvalidOperationException("Attendance settings could not be initialized.");
    }

    private Task<Developer?> ResolveDeveloperAsync(string userEmail, CancellationToken cancellationToken) =>
        _userDeveloperResolver.ResolveAsync(userEmail, cancellationToken);

    private static AttendanceNextActionDto GetNextAction(AttendanceRecord? record)
    {
        if (record?.PunchInUtc is null)
        {
            return AttendanceNextActionDto.PunchIn;
        }

        if (record.PunchOutUtc is null)
        {
            return AttendanceNextActionDto.PunchOut;
        }

        return AttendanceNextActionDto.Complete;
    }

    private AttendanceDayRowDto BuildAttendanceDayRow(
        IGrouping<(Guid DeveloperId, DateOnly WorkDate), AttendanceDaySourceRow> sourceRows,
        AttendanceSettings settings,
        TimeZoneInfo timeZone)
    {
        var recordRow = sourceRows.FirstOrDefault(row => row.HasRecord);
        var correctionRows = sourceRows.Where(row => !row.HasRecord).ToList();
        var pending = correctionRows
            .Where(row => row.CorrectionStatus == AttendanceCorrectionStatus.Pending)
            .OrderByDescending(row => row.CorrectionCreatedAtUtc)
            .FirstOrDefault();
        var record = recordRow is null
            ? null
            : new AttendanceRecord
            {
                DeveloperId = sourceRows.Key.DeveloperId,
                WorkDate = sourceRows.Key.WorkDate,
                PunchInUtc = recordRow.PunchInUtc,
                PunchOutUtc = recordRow.PunchOutUtc,
                PunchInIsCorrected = recordRow.PunchInIsCorrected,
                PunchOutIsCorrected = recordRow.PunchOutIsCorrected,
                PunchInCorrectedAtUtc = recordRow.PunchInCorrectedAtUtc,
                PunchOutCorrectedAtUtc = recordRow.PunchOutCorrectedAtUtc
            };

        return new AttendanceDayRowDto(
            sourceRows.Key.DeveloperId,
            recordRow?.DeveloperName ?? correctionRows.FirstOrDefault()?.DeveloperName ?? string.Empty,
            sourceRows.Key.WorkDate,
            recordRow?.PunchInUtc,
            recordRow?.PunchOutUtc,
            recordRow?.PunchInIsCorrected ?? false,
            recordRow?.PunchOutIsCorrected ?? false,
            recordRow?.PunchInCorrectedAtUtc,
            recordRow?.PunchOutCorrectedAtUtc,
            record is null ? null : _statusCalculator.ComputeStatus(record, settings, timeZone),
            record is null ? null : _statusCalculator.ComputeWorkHours(record),
            pending is not null,
            pending?.RequestedPunchInUtc is not null,
            pending?.RequestedPunchOutUtc is not null,
            pending?.CorrectionRequestId,
            correctionRows.Count);
    }

    private AttendanceDayRowDto BuildAttendanceDayRow(
        Guid developerId,
        DateOnly workDate,
        AttendanceRecord? record,
        IReadOnlyList<AttendanceCorrectionRequest> corrections,
        Developer? fallbackDeveloper,
        AttendanceSettings settings,
        TimeZoneInfo timeZone)
    {
        var pending = corrections
            .Where(request => request.Status == AttendanceCorrectionStatus.Pending)
            .OrderByDescending(request => request.CreatedAtUtc)
            .FirstOrDefault();
        var firstCorrection = corrections.FirstOrDefault();
        var developerName = record?.Developer?.Name
            ?? firstCorrection?.Developer?.Name
            ?? fallbackDeveloper?.Name
            ?? string.Empty;

        return new AttendanceDayRowDto(
            developerId,
            developerName,
            workDate,
            record?.PunchInUtc,
            record?.PunchOutUtc,
            record?.PunchInIsCorrected ?? false,
            record?.PunchOutIsCorrected ?? false,
            record?.PunchInCorrectedAtUtc,
            record?.PunchOutCorrectedAtUtc,
            record is null ? null : _statusCalculator.ComputeStatus(record, settings, timeZone),
            record is null ? null : _statusCalculator.ComputeWorkHours(record),
            pending is not null,
            pending?.RequestedPunchInUtc is not null,
            pending?.RequestedPunchOutUtc is not null,
            pending?.Id,
            corrections.Count);
    }

    private AttendanceRecordDto MapRecord(
        AttendanceRecord record,
        AttendanceSettings settings,
        TimeZoneInfo timeZone)
    {
        var status = _statusCalculator.ComputeStatus(record, settings, timeZone);
        var workHours = _statusCalculator.ComputeWorkHours(record);
        var developerName = record.Developer?.Name ?? string.Empty;

        return new AttendanceRecordDto(
            record.Id,
            record.DeveloperId,
            developerName,
            record.WorkDate,
            record.PunchInUtc,
            record.PunchOutUtc,
            record.PunchInIsCorrected,
            record.PunchOutIsCorrected,
            status,
            workHours,
            record.PunchInCorrectedAtUtc,
            record.PunchOutCorrectedAtUtc);
    }

    private static AttendanceSettingsDto MapSettings(AttendanceSettings settings) =>
        new(
            settings.WorkStartTime,
            settings.WorkEndTime,
            settings.BufferStartTime,
            settings.BufferEndTime,
            settings.PunchInAllowMinutesBeforeWorkStart,
            settings.PunchOutAllowMinutesAfterWorkEnd,
            settings.OfficeTimeZoneId,
            settings.UpdatedAtUtc,
            settings.StrictPunchIn,
            settings.StrictPunchOut);

    private static AttendanceCorrectionRequestDto MapCorrectionRequest(AttendanceCorrectionRequest request) =>
        new(
            request.Id,
            request.DeveloperId,
            request.Developer?.Name ?? string.Empty,
            request.WorkDate,
            request.RequestedPunchInUtc,
            request.RequestedPunchOutUtc,
            request.Reason,
            MapCorrectionStatus(request.Status),
            request.ReviewerComment,
            request.ReviewedAtUtc,
            request.CreatedAtUtc);

    private static AttendanceCorrectionStatusDto MapCorrectionStatus(AttendanceCorrectionStatus status) => status switch
    {
        AttendanceCorrectionStatus.Pending => AttendanceCorrectionStatusDto.Pending,
        AttendanceCorrectionStatus.Approved => AttendanceCorrectionStatusDto.Approved,
        AttendanceCorrectionStatus.Rejected => AttendanceCorrectionStatusDto.Rejected,
        _ => AttendanceCorrectionStatusDto.Pending
    };

    private static Result ValidateSettings(UpdateAttendanceSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OfficeTimeZoneId))
        {
            return Result.Failure("Office time zone is required.");
        }

        if (request.WorkStartTime >= request.WorkEndTime)
        {
            return Result.Failure("Work start time must be before work end time.");
        }

        if (request.BufferStartTime < request.WorkStartTime || request.BufferStartTime > request.WorkEndTime)
        {
            return Result.Failure("Buffer start time must be between work start and work end.");
        }

        if (request.BufferEndTime < request.WorkStartTime || request.BufferEndTime > request.WorkEndTime)
        {
            return Result.Failure("Buffer end time must be between work start and work end.");
        }

        if (request.BufferEndTime < request.BufferStartTime)
        {
            return Result.Failure("Buffer end time must be after buffer start time.");
        }

        if (request.PunchInAllowMinutesBeforeWorkStart < 0 || request.PunchInAllowMinutesBeforeWorkStart > 12 * 60)
        {
            return Result.Failure("Punch in allowance must be between 0 and 720 minutes.");
        }

        if (request.PunchOutAllowMinutesAfterWorkEnd < 0 || request.PunchOutAllowMinutesAfterWorkEnd > 12 * 60)
        {
            return Result.Failure("Punch out allowance must be between 0 and 720 minutes.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(request.OfficeTimeZoneId.Trim());
        }
        catch
        {
            return Result.Failure("Office time zone is not valid on this server.");
        }

        return Result.Success();
    }

    private static Result ValidateCorrectionRequest(CreateAttendanceCorrectionRequest request)
    {
        if (!request.RequestedPunchInUtc.HasValue && !request.RequestedPunchOutUtc.HasValue)
        {
            return Result.Failure("At least one punch time must be requested.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure("Reason is required.");
        }

        if (request.Reason.Trim().Length > MaxReasonLength)
        {
            return Result.Failure($"Reason cannot exceed {MaxReasonLength} characters.");
        }

        return Result.Success();
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private async Task<HashSet<DateOnly>> LoadHolidayDatesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var holidays = await _holidayRepository.GetByYearAsync(fromDate.Year, cancellationToken);
        if (toDate.Year != fromDate.Year)
        {
            var nextYear = await _holidayRepository.GetByYearAsync(toDate.Year, cancellationToken);
            holidays = holidays.Concat(nextYear).ToList();
        }

        var dates = new HashSet<DateOnly>();
        foreach (var holiday in holidays)
        {
            for (var date = holiday.FromDate; date <= holiday.ToDate; date = date.AddDays(1))
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private static IEnumerable<DateOnly> EnumerateWorkingDays(
        DateOnly fromDate,
        DateOnly toDate,
        int weekendDaysBitmask,
        HashSet<DateOnly> holidays)
    {
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            if (AttendanceStatusCalculator.IsWeekend(date, weekendDaysBitmask))
            {
                continue;
            }

            if (holidays.Contains(date))
            {
                continue;
            }

            yield return date;
        }
    }
}
