namespace DevPulse.Application.Services.Analytics;

public sealed class KpiSyncService : IKpiSyncService
{
    private readonly IReportService _reportService;
    private readonly ICachedAnalyticsService _cachedAnalyticsService;
    private readonly IDeveloperRepository _developerRepository;
    private readonly IClickUpAccountRepository _accountRepository;
    private readonly ISyncedTaskRepository _syncedTaskRepository;
    private readonly ITaskAssignmentPeriodRepository _assignmentPeriodRepository;
    private readonly IKpiSyncRunRepository _syncRunRepository;
    private readonly KpiSyncOptions _options;
    private readonly ILogger<KpiSyncService> _logger;

    public KpiSyncService(
        IReportService reportService,
        ICachedAnalyticsService cachedAnalyticsService,
        IDeveloperRepository developerRepository,
        IClickUpAccountRepository accountRepository,
        ISyncedTaskRepository syncedTaskRepository,
        ITaskAssignmentPeriodRepository assignmentPeriodRepository,
        IKpiSyncRunRepository syncRunRepository,
        IOptions<KpiSyncOptions> options,
        ILogger<KpiSyncService> logger)
    {
        _reportService = reportService;
        _cachedAnalyticsService = cachedAnalyticsService;
        _developerRepository = developerRepository;
        _accountRepository = accountRepository;
        _syncedTaskRepository = syncedTaskRepository;
        _assignmentPeriodRepository = assignmentPeriodRepository;
        _syncRunRepository = syncRunRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<KpiSyncResultDto>> SyncAsync(
        bool triggeredManually = false,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lookbackDays = Math.Clamp(_options.LookbackDays, 1, 365);
        var fromDate = today.AddDays(-(lookbackDays - 1));
        var toDate = today;

        var run = new KpiSyncRun
        {
            FromDate = fromDate,
            ToDate = toDate,
            Status = KpiSyncRunStatus.Running,
            TriggeredManually = triggeredManually,
            StartedAtUtc = DateTime.UtcNow
        };

        await _syncRunRepository.AddAsync(run, cancellationToken);

        try
        {
            var developers = await _developerRepository.GetWithMappingsAsync(cancellationToken);
            if (developers.Count == 0)
            {
                return await CompleteFailedAsync(run, "No developers with ClickUp mappings found.", cancellationToken);
            }

            var accounts = await _accountRepository.GetAllAsync(cancellationToken);
            if (accounts.Count == 0)
            {
                return await CompleteFailedAsync(run, "No ClickUp accounts configured.", cancellationToken);
            }

            var reportResult = await _reportService.GenerateDeveloperReportAsync(
                new DeveloperReportRequest(
                    developers.Select(d => d.Id).ToList(),
                    fromDate,
                    toDate,
                    IncludeClosed: true),
                cancellationToken);

            if (!reportResult.IsSuccess || reportResult.Value is null)
            {
                return await CompleteFailedAsync(
                    run,
                    reportResult.Error ?? "Failed to generate report data from ClickUp.",
                    cancellationToken);
            }

            var report = reportResult.Value;
            var syncedAt = DateTime.UtcNow;
            var syncedTasks = report.Tasks
                .GroupBy(t => (t.AccountId, t.TaskId))
                .Select(g => ReportTaskMapper.ToSyncedTask(
                    g.OrderByDescending(t => t.IsCompleted).First(),
                    syncedAt))
                .ToList();

            await _syncedTaskRepository.UpsertRangeAsync(syncedTasks, cancellationToken);

            var currentAssignees = report.Tasks
                .Select(t => new TaskCurrentAssignee(t.AccountId, t.TaskId, t.DeveloperId, t.DateCreated))
                .Distinct()
                .ToList();
            await _assignmentPeriodRepository.ApplyCurrentAssigneesAsync(currentAssignees, syncedAt, cancellationToken);
            await DemoReportDataProvider.ApplyHandoffPeriodsAsync(
                _assignmentPeriodRepository,
                accounts.Select(a => a.Id).ToHashSet(),
                developers.Select(d => d.Id).ToHashSet(),
                fromDate,
                toDate,
                cancellationToken);

            var dbReportResult = await _cachedAnalyticsService.GenerateReportFromDatabaseAsync(
                new DeveloperReportRequest(
                    developers.Select(d => d.Id).ToList(),
                    fromDate,
                    toDate,
                    IncludeClosed: true),
                cancellationToken);

            var snapshotSource = dbReportResult.IsSuccess && dbReportResult.Value is not null
                ? dbReportResult.Value
                : report;

            await _syncRunRepository.DeleteSnapshotsForPeriodAsync(fromDate, toDate, cancellationToken);

            var snapshots = snapshotSource.Developers
                .Where(d => d.TotalTasks > 0)
                .Select(d => new DeveloperKpiSnapshot
                {
                    SyncRunId = run.Id,
                    DeveloperId = d.DeveloperId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    DeveloperName = d.DeveloperName,
                    Email = d.Email,
                    TotalTasks = d.TotalTasks,
                    CompletedCount = d.CompletedCount,
                    InProgressCount = d.InProgressCount,
                    ChildTaskCount = d.ChildTaskCount,
                    WorkspaceCount = d.WorkspaceCount,
                    ProjectCount = d.ProjectCount,
                    OverdueCount = d.OverdueCount,
                    OnTimeCompletedCount = d.OnTimeCompletedCount,
                    AverageCompletionDays = d.AverageCompletionDays,
                    CompletionRate = ReportTaskMapper.Rate(d.CompletedCount, d.TotalTasks),
                    OnTimeRate = ReportTaskMapper.DeliveryHealth(d.OnTimeCompletedCount, d.OverdueCount),
                    GeneratedAtUtc = syncedAt
                })
                .ToList();

            await _syncRunRepository.AddSnapshotsAsync(snapshots, cancellationToken);

            run.Status = KpiSyncRunStatus.Succeeded;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.TasksUpserted = syncedTasks.Count;
            run.DeveloperCount = developers.Count;
            run.AccountCount = accounts.Count;
            run.ErrorMessage = null;
            await _syncRunRepository.UpdateAsync(run, cancellationToken);

            _logger.LogInformation(
                "KPI sync completed ({Trigger}): {TaskCount} tasks, {DeveloperCount} developers, period {FromDate}–{ToDate}",
                triggeredManually ? "manual" : "scheduled",
                syncedTasks.Count,
                developers.Count,
                fromDate,
                toDate);

            var dto = ToDto(run);
            return Result<KpiSyncResultDto>.Success(new KpiSyncResultDto(
                dto,
                $"Synced {syncedTasks.Count} tasks and generated {snapshots.Count} developer KPI snapshots."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KPI sync failed");
            return await CompleteFailedAsync(run, ex.Message, cancellationToken);
        }
    }

    public async Task<KpiSyncStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var lastRun = await _syncRunRepository.GetLatestAsync(cancellationToken);
        return new KpiSyncStatusResponse(
            lastRun is null ? null : ToDto(lastRun),
            _options.Enabled,
            Math.Clamp(_options.LookbackDays, 1, 365),
            ComputeNextScheduledRunUtc());
    }

    private async Task<Result<KpiSyncResultDto>> CompleteFailedAsync(
        KpiSyncRun run,
        string error,
        CancellationToken cancellationToken)
    {
        run.Status = KpiSyncRunStatus.Failed;
        run.CompletedAtUtc = DateTime.UtcNow;
        run.ErrorMessage = error.Length > 2000 ? error[..2000] : error;
        await _syncRunRepository.UpdateAsync(run, cancellationToken);
        return Result<KpiSyncResultDto>.Failure(error);
    }

    private DateTime? ComputeNextScheduledRunUtc()
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var hour = Math.Clamp(_options.RunHourUtc, 0, 23);
        var minute = Math.Clamp(_options.RunMinuteUtc, 0, 59);
        var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next;
    }

    internal static KpiSyncRunDto ToDto(KpiSyncRun run) =>
        new(
            run.Id,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.FromDate,
            run.ToDate,
            run.Status.ToString(),
            run.TasksUpserted,
            run.DeveloperCount,
            run.AccountCount,
            run.ErrorMessage,
            run.TriggeredManually);
}
