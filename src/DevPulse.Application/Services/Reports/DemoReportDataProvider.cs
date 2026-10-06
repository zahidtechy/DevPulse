namespace DevPulse.Application.Services.Reports;

/// <summary>
/// Provides sample completed and in-progress tasks for seeded demo workspaces so reports work without live ClickUp API calls.
/// </summary>
public static class DemoReportDataProvider
{
    public const string HandoffTaskId = "dv-5001";
    private sealed record DemoTaskTemplate(
        Guid DeveloperId,
        Guid AccountId,
        string TaskId,
        string TaskName,
        string ProjectName,
        string? FolderName,
        string ListName,
        int CreatedDay,
        int? CompletedDay,
        int? DueDay,
        double? CompletionDays,
        string Status,
        string? Priority = "normal",
        bool IsSubtask = false,
        string? ParentTaskId = null,
        string TaskType = "Task");

    private static readonly DemoTaskTemplate[] TaskTemplates =
    [
        new(SarahChenId(), DemoSeedData.InternalAccountId, "dv-1001", "Implement SSO login flow", "Internal Platform", "Authentication", "Platform", 2, 5, 6, 3.0, "complete", "high"),
        new(SarahChenId(), DemoSeedData.InternalAccountId, "dv-1001a", "Wire OIDC callback handler", "Internal Platform", "Authentication", "Platform", 3, 5, 6, 2.0, "complete", "high", true, "dv-1001", "Subtask"),
        new(SarahChenId(), DemoSeedData.InternalAccountId, "dv-1002", "Add role-based access checks", "Internal Platform", "Authentication", "Platform", 6, 9, 10, 3.0, "complete", "normal"),
        new(SarahChenId(), DemoSeedData.InternalAccountId, "dv-1005", "Harden session token rotation", "Internal Platform", "Authentication", "Platform", 10, null, 18, null, "in progress", "urgent"),
        new(SarahChenId(), DemoSeedData.AcmeAccountId, "dv-1003", "Build invoice export endpoint", "Acme Billing", null, "Acme Billing", 4, 8, 9, 4.0, "complete", "high"),
        new(SarahChenId(), DemoSeedData.AcmeAccountId, "dv-1004", "Fix timezone handling in reports", "Acme Billing", null, "Acme Billing", 12, 14, 13, 2.0, "complete", "normal"),
        new(SarahChenId(), DemoSeedData.AcmeAccountId, "dv-1006", "Invoice PDF branding polish", "Acme Billing", null, "Acme Billing", 15, null, 20, null, "in progress", "low", true, "dv-1003", "Subtask"),

        new(JamesOkonkwoId(), DemoSeedData.InternalAccountId, "dv-2001", "Optimize dashboard query performance", "Internal Platform", "Performance", "Platform", 1, 4, 5, 3.0, "complete", "high"),
        new(JamesOkonkwoId(), DemoSeedData.InternalAccountId, "dv-2001a", "Add index for report date filters", "Internal Platform", "Performance", "Platform", 2, 3, 5, 1.0, "complete", "normal", true, "dv-2001", "Subtask"),
        new(JamesOkonkwoId(), DemoSeedData.InternalAccountId, "dv-2002", "Set up CI pipeline for API", "Internal DevOps", "Pipelines", "DevOps", 7, 10, 12, 3.0, "complete", "normal"),
        new(JamesOkonkwoId(), DemoSeedData.InternalAccountId, "dv-2005", "Cache workspace member lookups", "Internal Platform", "Performance", "Platform", 11, null, 10, null, "in progress", "normal"),
        new(JamesOkonkwoId(), DemoSeedData.AcmeAccountId, "dv-2003", "Integrate payment webhook handler", "Acme Payments", null, "Acme Payments", 3, 7, 8, 4.0, "complete", "urgent"),

        new(PriyaSharmaId(), DemoSeedData.InternalAccountId, "dv-3001", "Redesign developer report filters", "Internal UX", "Reports UI", "UX", 2, 6, 8, 4.0, "complete", "normal"),
        new(PriyaSharmaId(), DemoSeedData.InternalAccountId, "dv-3002", "Add empty-state messaging", "Internal UX", "Reports UI", "UX", 8, 11, 12, 3.0, "complete", "low"),
        new(PriyaSharmaId(), DemoSeedData.InternalAccountId, "dv-3005", "Report status badge polish", "Internal UX", "Reports UI", "UX", 14, null, 16, null, "in progress", "normal"),
        new(PriyaSharmaId(), DemoSeedData.AcmeAccountId, "dv-3003", "Ship customer onboarding checklist", "Acme Delivery", "Onboarding", "Acme Delivery", 5, 9, 10, 4.0, "complete", "high"),
        new(PriyaSharmaId(), DemoSeedData.AcmeAccountId, "dv-3003a", "Checklist progress persistence", "Acme Delivery", "Onboarding", "Acme Delivery", 6, 8, 10, 2.0, "complete", "normal", true, "dv-3003", "Subtask"),
        new(PriyaSharmaId(), DemoSeedData.AcmeAccountId, "dv-3004", "Resolve mobile layout regressions", "Acme Delivery", "Onboarding", "Acme Delivery", 13, 15, 14, 2.0, "complete", "high"),

        new(MarcusWebbId(), DemoSeedData.InternalAccountId, "dv-4001", "Write integration tests for auth", "Internal QA", "Automation", "QA", 3, 8, 9, 5.0, "complete", "normal"),
        new(MarcusWebbId(), DemoSeedData.InternalAccountId, "dv-4002", "Automate regression suite in CI", "Internal QA", "Automation", "QA", 9, 13, 14, 4.0, "complete", "high"),
        new(MarcusWebbId(), DemoSeedData.InternalAccountId, "dv-4005", "Expand smoke coverage for reports", "Internal QA", "Automation", "QA", 16, null, 15, null, "in progress", "normal", true, "dv-4002", "Subtask"),
        new(MarcusWebbId(), DemoSeedData.InternalAccountId, HandoffTaskId, "Validate payment flow after developer handoff", "Internal Platform", "Payments", "QA", 2, 16, 18, 8.0, "complete", "high"),
        new(MarcusWebbId(), DemoSeedData.AcmeAccountId, "dv-4003", "Validate API contract changes", "Acme QA", null, "Acme QA", 6, 10, 11, 4.0, "complete", "normal"),
    ];

    public static async Task ApplyHandoffPeriodsAsync(
        ITaskAssignmentPeriodRepository repository,
        IReadOnlySet<Guid> activeAccountIds,
        IReadOnlySet<Guid> activeDeveloperIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        if (!activeAccountIds.Contains(DemoSeedData.InternalAccountId)
            || !activeDeveloperIds.Contains(DemoSeedData.SarahChenId)
            || !activeDeveloperIds.Contains(DemoSeedData.MarcusWebbId)
            || fromDate > toDate)
        {
            return;
        }

        var month = new DateOnly(toDate.Year, toDate.Month, 1);
        var assignedAt = ToUtc(month, 2);
        var handedOffAt = ToUtc(month, 10);

        await repository.InsertIfMissingAsync(
            [
                new TaskAssignmentPeriod
                {
                    AccountId = DemoSeedData.InternalAccountId,
                    TaskId = HandoffTaskId,
                    DeveloperId = DemoSeedData.SarahChenId,
                    AssignedAtUtc = assignedAt,
                    UnassignedAtUtc = handedOffAt
                }
            ],
            cancellationToken);

        await repository.AdjustOpenPeriodStartAsync(
            DemoSeedData.InternalAccountId,
            HandoffTaskId,
            DemoSeedData.MarcusWebbId,
            handedOffAt,
            cancellationToken);
    }

    public static IReadOnlyList<DeveloperReportTaskDto> GetTasksForDateRange(
        DateOnly fromDate,
        DateOnly toDate,
        ClickUpAccount account,
        Developer developer)
    {
        if (!DemoSeedData.IsDemoWorkspace(account.WorkspaceId) || fromDate > toDate)
        {
            return [];
        }

        var fromMs = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var toExclusiveMs = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        var tasks = new List<DeveloperReportTaskDto>();
        var monthCursor = new DateOnly(fromDate.Year, fromDate.Month, 1);
        var endMonth = new DateOnly(toDate.Year, toDate.Month, 1);

        while (monthCursor <= endMonth)
        {
            tasks.AddRange(GetTasksForMonth(monthCursor, account, developer));
            monthCursor = monthCursor.AddMonths(1);
        }

        return tasks
            .Where(t =>
                (t.IsCompleted
                    && t.DateDone.HasValue
                    && t.DateDone.Value >= fromMs
                    && t.DateDone.Value < toExclusiveMs)
                || (!t.IsCompleted
                    && t.DateCreated.HasValue
                    && t.DateCreated.Value >= fromMs
                    && t.DateCreated.Value < toExclusiveMs))
            .GroupBy(t => t.TaskId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(t => t.IsCompleted).First())
            .ToList();
    }

    public static IReadOnlyList<DeveloperReportTaskDto> GetTasksForMonth(
        DateOnly month,
        ClickUpAccount account,
        Developer developer)
    {
        if (!DemoSeedData.IsDemoWorkspace(account.WorkspaceId))
        {
            return [];
        }

        return TaskTemplates
            .Where(t => t.DeveloperId == developer.Id && t.AccountId == account.Id)
            .Select(t => ToTaskDto(month, account, developer, t))
            .ToList();
    }

    private static DeveloperReportTaskDto ToTaskDto(
        DateOnly month,
        ClickUpAccount account,
        Developer developer,
        DemoTaskTemplate template)
    {
        var created = ToUnixMs(month, template.CreatedDay);
        var isCompleted = template.CompletedDay.HasValue;
        long? completed = isCompleted ? ToUnixMs(month, template.CompletedDay!.Value) : null;
        long? due = template.DueDay.HasValue ? ToUnixMs(month, template.DueDay.Value) : null;

        return new DeveloperReportTaskDto(
            developer.Id,
            developer.Name,
            account.Id,
            account.Name,
            template.ProjectName,
            template.FolderName,
            template.TaskId,
            template.TaskName,
            template.Status,
            template.Priority,
            template.ListName,
            null,
            created,
            completed,
            due,
            template.CompletionDays,
            template.IsSubtask,
            template.ParentTaskId,
            ParentTaskName: null,
            template.TaskType,
            isCompleted);
    }

    private static DateTime ToUtc(DateOnly month, int day)
    {
        var clampedDay = Math.Clamp(day, 1, DateTime.DaysInMonth(month.Year, month.Month));
        return new DateTime(month.Year, month.Month, clampedDay, 12, 0, 0, DateTimeKind.Utc);
    }

    private static long ToUnixMs(DateOnly month, int day)
    {
        var clampedDay = Math.Clamp(day, 1, DateTime.DaysInMonth(month.Year, month.Month));
        var date = new DateTimeOffset(month.Year, month.Month, clampedDay, 12, 0, 0, TimeSpan.Zero);
        return date.ToUnixTimeMilliseconds();
    }

    private static Guid SarahChenId() => DemoSeedData.SarahChenId;

    private static Guid JamesOkonkwoId() => DemoSeedData.JamesOkonkwoId;

    private static Guid PriyaSharmaId() => DemoSeedData.PriyaSharmaId;

    private static Guid MarcusWebbId() => DemoSeedData.MarcusWebbId;
}
