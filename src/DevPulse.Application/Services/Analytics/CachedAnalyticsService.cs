namespace DevPulse.Application.Services.Analytics;

public sealed class CachedAnalyticsService : ICachedAnalyticsService
{
    private readonly ISyncedTaskRepository _syncedTaskRepository;
    private readonly ITaskAssignmentPeriodRepository _assignmentPeriodRepository;
    private readonly IDeveloperRepository _developerRepository;
    private readonly IKpiSyncRunRepository _syncRunRepository;
    private readonly ILogger<CachedAnalyticsService> _logger;

    public CachedAnalyticsService(
        ISyncedTaskRepository syncedTaskRepository,
        ITaskAssignmentPeriodRepository assignmentPeriodRepository,
        IDeveloperRepository developerRepository,
        IKpiSyncRunRepository syncRunRepository,
        ILogger<CachedAnalyticsService> logger)
    {
        _syncedTaskRepository = syncedTaskRepository;
        _assignmentPeriodRepository = assignmentPeriodRepository;
        _developerRepository = developerRepository;
        _syncRunRepository = syncRunRepository;
        _logger = logger;
    }

    public async Task<Result<CachedAnalyticsResponse>> GetAnalyticsFromDatabaseAsync(
        CachedAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var reportResult = await GenerateReportFromDatabaseAsync(
            new DeveloperReportRequest(
                request.DeveloperIds,
                request.FromDate,
                request.ToDate,
                IncludeClosed: true,
                request.AccountIds),
            cancellationToken);

        if (!reportResult.IsSuccess || reportResult.Value is null)
        {
            return Result<CachedAnalyticsResponse>.Failure(reportResult.Error ?? "Failed to load analytics from database.");
        }

        var lastRun = await _syncRunRepository.GetLatestAsync(cancellationToken);
        var syncedAt = await _syncedTaskRepository.GetLatestSyncedAtAsync(cancellationToken);

        return Result<CachedAnalyticsResponse>.Success(new CachedAnalyticsResponse(
            reportResult.Value,
            "database",
            syncedAt,
            lastRun is null ? null : KpiSyncService.ToDto(lastRun)));
    }

    public async Task<Result<DeveloperReportResponse>> GenerateReportFromDatabaseAsync(
        DeveloperReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DeveloperIds.Count == 0)
        {
            return Result<DeveloperReportResponse>.Failure("Select at least one developer.");
        }

        if (request.FromDate > request.ToDate)
        {
            return Result<DeveloperReportResponse>.Failure("From date must be on or before to date.");
        }

        var developers = await _developerRepository.GetByIdsWithMappingsAsync(request.DeveloperIds, cancellationToken);
        if (developers.Count == 0)
        {
            return Result<DeveloperReportResponse>.Failure("No matching developers were found.");
        }

        var lastRun = await _syncRunRepository.GetLatestAsync(cancellationToken);
        var syncedAt = await _syncedTaskRepository.GetLatestSyncedAtAsync(cancellationToken);
        if (lastRun is null && syncedAt is null)
        {
            return Result<DeveloperReportResponse>.Failure("No synced KPI data found. Run a KPI sync first.");
        }

        var rangeStartUtc = ReportTaskMapper.ToRangeStartUtc(request.FromDate);
        var rangeEndExclusiveUtc = ReportTaskMapper.ToRangeEndExclusiveUtc(request.ToDate);
        var fromMs = ReportTaskMapper.ToRangeStartMs(request.FromDate);
        var toExclusiveMs = ReportTaskMapper.ToRangeEndExclusiveMs(request.ToDate);

        var periods = await _assignmentPeriodRepository.GetOverlappingAsync(
            developers.Select(d => d.Id).ToList(),
            rangeStartUtc,
            rangeEndExclusiveUtc,
            request.AccountIds,
            cancellationToken);

        var accountIds = periods.Select(p => p.AccountId).Distinct().ToList();
        var taskIds = periods.Select(p => p.TaskId).Distinct().ToList();
        var snapshots = await _syncedTaskRepository.GetByAccountAndTaskIdsAsync(accountIds, taskIds, cancellationToken);
        var snapshotByKey = snapshots.ToDictionary(t => (t.AccountId, t.TaskId));

        var nameById = developers.ToDictionary(d => d.Id, d => d.Name);
        var emailById = developers.ToDictionary(d => d.Id, d => d.Email);

        var reportTasks = new List<DeveloperReportTaskDto>();
        foreach (var period in periods)
        {
            if (!snapshotByKey.TryGetValue((period.AccountId, period.TaskId), out var snapshot))
            {
                continue;
            }

            var dateDoneUtc = ReportTaskMapper.ToUtc(snapshot.DateDone);
            var dateDoneInPeriod = dateDoneUtc.HasValue
                && ReportTaskMapper.InstantInPeriod(dateDoneUtc.Value, period);
            var dateDoneInRange = snapshot.DateDone.HasValue
                && snapshot.DateDone.Value >= fromMs
                && snapshot.DateDone.Value < toExclusiveMs;
            var dateCreatedInRange = snapshot.DateCreated.HasValue
                && snapshot.DateCreated.Value >= fromMs
                && snapshot.DateCreated.Value < toExclusiveMs;

            var completedForPerson = snapshot.IsCompleted && dateDoneInPeriod;
            if (!request.IncludeClosed && snapshot.IsCompleted)
            {
                continue;
            }

            var include = completedForPerson
                ? dateDoneInRange
                : snapshot.IsCompleted || dateCreatedInRange;

            if (!include)
            {
                continue;
            }

            var statusOverride = !completedForPerson && snapshot.IsCompleted
                ? "handed off"
                : null;

            reportTasks.Add(ReportTaskMapper.ToReportTask(
                snapshot,
                period.DeveloperId,
                nameById.GetValueOrDefault(period.DeveloperId, "Unknown"),
                completedForPerson && dateDoneInRange,
                statusOverride));
        }

        reportTasks = reportTasks
            .GroupBy(t => (t.DeveloperId, t.AccountId, t.TaskId))
            .Select(g => g.OrderByDescending(t => t.IsCompleted).First())
            .OrderBy(t => t.DeveloperName)
            .ThenBy(t => t.IsCompleted)
            .ThenBy(t => t.AccountName)
            .ThenBy(t => t.ProjectName)
            .ThenBy(t => t.TaskName)
            .ToList();

        var summaries = developers
            .Select(d => ReportTaskMapper.BuildSummary(
                d.Id,
                d.Name,
                emailById.GetValueOrDefault(d.Id),
                reportTasks))
            .Where(s => s.TotalTasks > 0 || request.DeveloperIds.Contains(s.DeveloperId))
            .OrderByDescending(s => s.TotalTasks)
            .ThenBy(s => s.DeveloperName)
            .ToList();

        var workspaceCount = reportTasks.Select(t => t.AccountId).Distinct().Count();
        var response = new DeveloperReportResponse(
            request.FromDate,
            request.ToDate,
            reportTasks.Count(t => t.IsCompleted),
            reportTasks.Count(t => !t.IsCompleted),
            workspaceCount,
            summaries,
            reportTasks);

        _logger.LogInformation(
            "Generated DB-backed report for {FromDate}–{ToDate}: {Completed} completed, {InProgress} in progress from {TaskCount} assignment-period rows",
            request.FromDate,
            request.ToDate,
            response.TotalTasksCompleted,
            response.TotalInProgress,
            reportTasks.Count);

        return Result<DeveloperReportResponse>.Success(response);
    }
}
