namespace DevPulse.Application.Services.Reports;

public sealed class ReportService : IReportService
{
    private const int ClickUpPageSize = 100;

    private readonly IDeveloperRepository _developerRepository;
    private readonly IClickUpAccountRepository _accountRepository;
    private readonly IClickUpApiClient _apiClient;
    private readonly ITokenProtector _tokenProtector;
    private readonly ClickUpApiOptions _clickUpOptions;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        IDeveloperRepository developerRepository,
        IClickUpAccountRepository accountRepository,
        IClickUpApiClient apiClient,
        ITokenProtector tokenProtector,
        IOptions<ClickUpApiOptions> clickUpOptions,
        ILogger<ReportService> logger)
    {
        _developerRepository = developerRepository;
        _accountRepository = accountRepository;
        _apiClient = apiClient;
        _tokenProtector = tokenProtector;
        _clickUpOptions = clickUpOptions.Value;
        _logger = logger;
    }

    public async Task<Result<DeveloperReportResponse>> GenerateDeveloperReportAsync(
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

        // IsActive is registry-only for developers and ClickUp accounts; inactive records remain in reports and analytics.
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);
        if (accounts.Count == 0)
        {
            return Result<DeveloperReportResponse>.Failure("No ClickUp accounts configured.");
        }

        if (request.AccountIds is { Count: > 0 })
        {
            accounts = accounts.Where(a => request.AccountIds.Contains(a.Id)).ToList();
            if (accounts.Count == 0)
            {
                return Result<DeveloperReportResponse>.Failure("No matching ClickUp accounts were found.");
            }
        }

        var reportTasks = new List<DeveloperReportTaskDto>();
        var queriedWorkspaces = new HashSet<Guid>();
        var assigneeBatchSize = Math.Clamp(_clickUpOptions.AssigneeBatchSize, 1, 50);

        foreach (var account in accounts)
        {
            var developersForAccount = developers
                .Select(d => new
                {
                    Developer = d,
                    Mapping = d.ClickUpMappings.FirstOrDefault(m => m.ClickUpAccountId == account.Id)
                })
                .Where(x => x.Mapping is not null)
                .ToList();

            if (developersForAccount.Count == 0)
            {
                continue;
            }

            queriedWorkspaces.Add(account.Id);

            if (DemoSeedData.IsDemoWorkspace(account.WorkspaceId))
            {
                foreach (var entry in developersForAccount)
                {
                    reportTasks.AddRange(
                        DemoReportDataProvider.GetTasksForDateRange(
                            request.FromDate,
                            request.ToDate,
                            account,
                            entry.Developer)
                            .Where(IsTaskOrSubtask));
                }

                continue;
            }

            string token = _tokenProtector.Unprotect(account.EncryptedAccessToken);
            var developerByClickUpUserId = developersForAccount
                .GroupBy(x => x.Mapping!.ClickUpUserId)
                .ToDictionary(g => g.Key, g => g.First().Developer);

            var clickUpUserIds = developerByClickUpUserId.Keys.ToList();
            foreach (var assigneeChunk in clickUpUserIds.Chunk(assigneeBatchSize))
            {
                var chunkIds = assigneeChunk.ToList();
                var accountTasks = await FetchAccountTasksAsync(
                    token,
                    account,
                    chunkIds,
                    request,
                    cancellationToken);

                foreach (var task in accountTasks)
                {
                    var assigneeIds = task.AssigneeIds ?? [];
                    var matchedDevelopers = assigneeIds
                        .Where(developerByClickUpUserId.ContainsKey)
                        .Select(id => developerByClickUpUserId[id])
                        .DistinctBy(d => d.Id)
                        .ToList();

                    // Fallback: if ClickUp omitted assignee ids, attribute to the sole developer in this chunk.
                    if (matchedDevelopers.Count == 0 && chunkIds.Count == 1
                        && developerByClickUpUserId.TryGetValue(chunkIds[0], out var soleDeveloper))
                    {
                        matchedDevelopers.Add(soleDeveloper);
                    }

                    foreach (var developer in matchedDevelopers)
                    {
                        reportTasks.Add(ToReportTask(developer, account, task));
                    }
                }
            }
        }

        // Prefer completed row when the same task appears in both queries.
        reportTasks = reportTasks
            .GroupBy(t => (t.DeveloperId, t.AccountId, t.TaskId))
            .Select(g => g.OrderByDescending(t => t.IsCompleted).First())
            .ToList();

        reportTasks = EnrichParentTaskNames(reportTasks);

        var summaries = developers
            .Select(developer => BuildSummary(developer, reportTasks))
            .Where(s => s.TotalTasks > 0 || request.DeveloperIds.Contains(s.DeveloperId))
            .OrderByDescending(s => s.TotalTasks)
            .ThenBy(s => s.DeveloperName)
            .ToList();

        var completedCount = reportTasks.Count(t => t.IsCompleted);
        var inProgressCount = reportTasks.Count(t => !t.IsCompleted);

        var response = new DeveloperReportResponse(
            request.FromDate,
            request.ToDate,
            completedCount,
            inProgressCount,
            queriedWorkspaces.Count,
            summaries,
            reportTasks
                .OrderBy(t => t.DeveloperName)
                .ThenBy(t => t.IsCompleted)
                .ThenBy(t => t.AccountName)
                .ThenBy(t => t.ProjectName)
                .ThenBy(t => t.TaskName)
                .ToList());

        _logger.LogInformation(
            "Generated developer report for {FromDate} to {ToDate}: {CompletedCount} completed, {InProgressCount} in progress across {WorkspaceCount} workspaces",
            request.FromDate,
            request.ToDate,
            response.TotalTasksCompleted,
            response.TotalInProgress,
            response.WorkspaceCount);

        return Result<DeveloperReportResponse>.Success(response);
    }

    private async Task<IReadOnlyList<ClickUpTaskDto>> FetchAccountTasksAsync(
        string token,
        ClickUpAccount account,
        IReadOnlyList<int> clickUpUserIds,
        DeveloperReportRequest request,
        CancellationToken cancellationToken)
    {
        var fromMs = ToRangeStartMs(request.FromDate);
        var toExclusiveMs = ToRangeEndExclusiveMs(request.ToDate);

        var completedQuery = new ClickUpTaskQueryRequest(
            account.Id,
            clickUpUserIds,
            Month: null,
            request.IncludeClosed,
            Page: 0,
            request.FromDate,
            request.ToDate,
            IncludeSubtasks: true,
            CustomItemIds: [0],
            DateFilter: ClickUpDateFilterMode.DateDone);

        var openQuery = new ClickUpTaskQueryRequest(
            account.Id,
            clickUpUserIds,
            Month: null,
            IncludeClosed: false,
            Page: 0,
            request.FromDate,
            request.ToDate,
            IncludeSubtasks: true,
            CustomItemIds: [0],
            DateFilter: ClickUpDateFilterMode.DateCreated);

        var completed = await FetchAllTasksAsync(token, account, completedQuery, cancellationToken);
        var open = await FetchAllTasksAsync(token, account, openQuery, cancellationToken);

        return completed
            .Concat(open)
            .Where(t => !IsBugCustomItem(t.CustomItemId, t.TaskTypeName))
            .Where(t => IsWithinSelectedDateRange(t, fromMs, toExclusiveMs))
            .GroupBy(t => t.Id, StringComparer.Ordinal)
            .Select(g => g.FirstOrDefault(t => IsCompletedTask(t)) ?? g.First())
            .ToList();
    }

    private static bool IsWithinSelectedDateRange(ClickUpTaskDto task, long fromMs, long toExclusiveMs)
    {
        if (IsCompletedTask(task))
        {
            return task.DateDone.HasValue
                && task.DateDone.Value >= fromMs
                && task.DateDone.Value < toExclusiveMs;
        }

        return task.DateCreated.HasValue
            && task.DateCreated.Value >= fromMs
            && task.DateCreated.Value < toExclusiveMs;
    }

    private static long ToRangeStartMs(DateOnly fromDate) =>
        new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static long ToRangeEndExclusiveMs(DateOnly toDate) =>
        new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private async Task<IReadOnlyList<ClickUpTaskDto>> FetchAllTasksAsync(
        string token,
        ClickUpAccount account,
        ClickUpTaskQueryRequest baseQuery,
        CancellationToken cancellationToken)
    {
        var allTasks = new List<ClickUpTaskDto>();
        var page = 0;

        while (true)
        {
            var query = baseQuery with { Page = page };
            var response = await _apiClient.GetFilteredTasksAsync(
                token,
                account.WorkspaceId,
                account.Name,
                account.Id,
                query,
                cancellationToken);

            allTasks.AddRange(response.Tasks);

            if (response.Tasks.Count < ClickUpPageSize)
            {
                break;
            }

            page++;
        }

        return allTasks;
    }

    private static DeveloperReportTaskDto ToReportTask(Developer developer, ClickUpAccount account, ClickUpTaskDto task)
    {
        var isCompleted = IsCompletedTask(task);
        return new DeveloperReportTaskDto(
            developer.Id,
            developer.Name,
            account.Id,
            account.Name,
            task.ProjectName,
            task.FolderName,
            task.Id,
            task.Name,
            task.Status,
            task.Priority,
            task.ListName,
            task.Url,
            task.DateCreated,
            task.DateDone,
            task.DueDate,
            isCompleted ? CalculateCompletionDays(task.DateCreated, task.DateDone) : null,
            task.IsSubtask,
            task.ParentTaskId,
            ParentTaskName: null,
            task.IsSubtask ? "Subtask" : task.TaskTypeName,
            isCompleted,
            task.AssigneeIds);
    }

    private static DeveloperReportSummaryDto BuildSummary(Developer developer, IReadOnlyList<DeveloperReportTaskDto> tasks)
    {
        var developerTasks = tasks.Where(t => t.DeveloperId == developer.Id).ToList();
        var byWorkspace = developerTasks
            .GroupBy(t => new { t.AccountId, t.AccountName })
            .Select(g => new DeveloperWorkspaceBreakdownDto(g.Key.AccountId, g.Key.AccountName, g.Count()))
            .OrderByDescending(x => x.TaskCount)
            .ToList();

        var byProject = developerTasks
            .GroupBy(t => new { t.AccountId, t.AccountName, ProjectName = t.ProjectName ?? "Unknown" })
            .Select(g => new DeveloperProjectBreakdownDto(g.Key.AccountId, g.Key.AccountName, g.Key.ProjectName, g.Count()))
            .OrderByDescending(x => x.TaskCount)
            .ToList();

        var completionDays = developerTasks
            .Where(t => t.IsCompleted && t.CompletionDays.HasValue)
            .Select(t => t.CompletionDays!.Value)
            .ToList();

        return new DeveloperReportSummaryDto(
            developer.Id,
            developer.Name,
            developer.Email,
            developerTasks.Count,
            developerTasks.Count(t => t.IsCompleted),
            developerTasks.Count(t => !t.IsCompleted),
            developerTasks.Count(t => t.IsSubtask),
            byWorkspace.Count,
            byProject.Count,
            developerTasks.Count(IsOverdue),
            developerTasks.Count(IsOnTimeCompletion),
            completionDays.Count > 0 ? Math.Round(completionDays.Average(), 1) : null,
            byWorkspace,
            byProject);
    }

    private static List<DeveloperReportTaskDto> EnrichParentTaskNames(IReadOnlyList<DeveloperReportTaskDto> tasks)
    {
        // Same ClickUp task id can appear on multiple developer rows (multi-assignee / batched fetch).
        var namesById = tasks
            .GroupBy(t => t.TaskId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().TaskName, StringComparer.Ordinal);

        return tasks
            .Select(task =>
            {
                if (string.IsNullOrWhiteSpace(task.ParentTaskId)
                    || !namesById.TryGetValue(task.ParentTaskId, out var parentName))
                {
                    return task;
                }

                return task with { ParentTaskName = parentName };
            })
            .ToList();
    }

    private static bool IsOverdue(DeveloperReportTaskDto task)
    {
        if (!task.DueDate.HasValue)
        {
            return false;
        }

        if (task.IsCompleted)
        {
            return task.DateDone.HasValue && task.DateDone.Value > task.DueDate.Value;
        }

        return task.DueDate.Value < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static bool IsOnTimeCompletion(DeveloperReportTaskDto task) =>
        task.IsCompleted
        && task.DueDate.HasValue
        && task.DateDone.HasValue
        && task.DateDone.Value <= task.DueDate.Value;

    private static bool IsCompletedTask(ClickUpTaskDto task)
    {
        if (task.DateDone.HasValue)
        {
            return true;
        }

        return IsCompletedStatus(task.Status);
    }

    private static bool IsCompletedStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim();
        return normalized.Equals("complete", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("closed", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("done", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTaskOrSubtask(DeveloperReportTaskDto task) =>
        !task.TaskType.Contains("bug", StringComparison.OrdinalIgnoreCase);

    private static bool IsBugCustomItem(int? customItemId, string taskTypeName) =>
        customItemId is > 0 ||
        taskTypeName.Contains("bug", StringComparison.OrdinalIgnoreCase);

    private static double? CalculateCompletionDays(long? dateCreated, long? dateDone)
    {
        if (!dateCreated.HasValue || !dateDone.HasValue)
        {
            return null;
        }

        var created = DateTimeOffset.FromUnixTimeMilliseconds(dateCreated.Value);
        var done = DateTimeOffset.FromUnixTimeMilliseconds(dateDone.Value);
        return Math.Round((done - created).TotalDays, 1);
    }
}
