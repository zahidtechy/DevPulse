namespace DevPulse.Client.Analytics;

public sealed record NamedCount(string Name, int Count);

public sealed record DeveloperKpi(
    Guid DeveloperId,
    string DeveloperName,
    string? Email,
    int TotalTasks,
    int CompletedCount,
    int InProgressCount,
    int OverdueCount,
    int OnTimeCompletedCount,
    double CompletionRate,
    double? OnTimeRate,
    double? AverageCycleDays,
    int WorkspaceCount,
    int ProjectCount);

public sealed record TeamKpis(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalTasks,
    int CompletedCount,
    int InProgressCount,
    int OverdueCount,
    int OnTimeCompletedCount,
    double CompletionRate,
    double? OnTimeRate,
    double? AverageCycleDays,
    int ActiveDevelopers,
    int WorkspaceCount,
    IReadOnlyList<DeveloperKpi> Developers,
    IReadOnlyList<NamedCount> ByWorkspace,
    IReadOnlyList<NamedCount> ByProject);

public enum TrendDirection
{
    Up,
    Down,
    Flat,
    Unknown
}

public sealed record KpiTrend(
    TrendDirection Direction,
    bool IsPositive,
    double? DeltaAbsolute,
    double? DeltaPoints)
{
    public static KpiTrend Unknown { get; } = new(TrendDirection.Unknown, true, null, null);
}

public sealed record KpiTrendDisplay(string Text, string CssClass, string Title);

public sealed record SlaTargets(
    double MinCompletionRate = 70,
    double MinDeliveryHealth = 85,
    double MaxAverageCycleDays = 7,
    int MaxTeamOverdueCount = 5);

public sealed record SlaCheckResult(
    string Metric,
    bool Met,
    string Actual,
    string Target,
    string Detail);

public sealed record AtRiskDeveloperEntry(
    DeveloperKpi Developer,
    IReadOnlyList<SlaCheckResult> MissedChecks);

public static class KpiAnalytics
{
    public static readonly SlaTargets DefaultSlaTargets = new();

    public static TeamKpis FromReport(DeveloperReportResponse report)
    {
        var activeDevelopers = report.Developers
            .Where(d => d.TotalTasks > 0)
            .Select(ToDeveloperKpi)
            .OrderByDescending(d => d.CompletedCount)
            .ThenByDescending(d => d.CompletionRate)
            .ToList();

        var totalTasks = activeDevelopers.Sum(d => d.TotalTasks);
        var completed = report.TotalTasksCompleted;
        var overdue = activeDevelopers.Sum(d => d.OverdueCount);
        var onTime = activeDevelopers.Sum(d => d.OnTimeCompletedCount);

        var cycleSamples = activeDevelopers
            .Where(d => d.AverageCycleDays.HasValue && d.CompletedCount > 0)
            .Select(d => (Days: d.AverageCycleDays!.Value, Weight: d.CompletedCount))
            .ToList();

        double? avgCycle = null;
        if (cycleSamples.Count > 0)
        {
            var weight = cycleSamples.Sum(s => s.Weight);
            if (weight > 0)
            {
                avgCycle = Math.Round(cycleSamples.Sum(s => s.Days * s.Weight) / weight, 1);
            }
        }

        var byWorkspace = report.Developers
            .SelectMany(d => d.ByWorkspace)
            .GroupBy(w => w.AccountName)
            .Select(g => new NamedCount(g.Key, g.Sum(x => x.TaskCount)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var byProject = report.Developers
            .SelectMany(d => d.ByProject)
            .GroupBy(p => p.ProjectName)
            .Select(g => new NamedCount(g.Key, g.Sum(x => x.TaskCount)))
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        return new TeamKpis(
            report.FromDate,
            report.ToDate,
            totalTasks,
            completed,
            report.TotalInProgress,
            overdue,
            onTime,
            Rate(completed, totalTasks),
            DeliveryHealth(onTime, overdue),
            avgCycle,
            activeDevelopers.Count,
            report.WorkspaceCount,
            activeDevelopers,
            byWorkspace,
            byProject);
    }

    private static DeveloperKpi ToDeveloperKpi(DeveloperReportSummaryDto summary) =>
        new(
            summary.DeveloperId,
            summary.DeveloperName,
            summary.Email,
            summary.TotalTasks,
            summary.CompletedCount,
            summary.InProgressCount,
            summary.OverdueCount,
            summary.OnTimeCompletedCount,
            Rate(summary.CompletedCount, summary.TotalTasks),
            DeliveryHealth(summary.OnTimeCompletedCount, summary.OverdueCount),
            summary.AverageCompletionDays,
            summary.WorkspaceCount,
            summary.ProjectCount);

    /// <summary>
    /// Delivery health: on-time completions vs on-time + overdue pressure.
    /// </summary>
    private static double? DeliveryHealth(int onTimeCompleted, int overdueCount)
    {
        var denominator = onTimeCompleted + overdueCount;
        return denominator > 0 ? Rate(onTimeCompleted, denominator) : null;
    }

    public static double Rate(int numerator, int denominator) =>
        denominator <= 0 ? 0 : Math.Round(numerator * 100.0 / denominator, 1);

    /// <summary>
    /// Completed ÷ assigned tasks in the period. Same formula as completion rate.
    /// </summary>
    public static double AchievementRatio(int achievement, int target) =>
        Rate(achievement, target);

    public static string FormatTargetVsAchievement(int achievement, int target) =>
        $"{achievement} / {target}";

    public static string AchievementRatioTone(double ratio, SlaTargets? targets = null)
    {
        targets ??= DefaultSlaTargets;
        if (ratio >= 100)
        {
            return "tone-success";
        }

        if (ratio >= targets.MinCompletionRate)
        {
            return "tone-primary";
        }

        if (ratio >= 40)
        {
            return "tone-warning";
        }

        return "tone-danger";
    }

    public static string FormatPercent(double? value) =>
        value.HasValue ? $"{value.Value:0.#}%" : "—";

    public static string FormatDays(double? value) =>
        value.HasValue ? $"{value.Value:0.#}d" : "—";

    public static string FormatPeriod(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate == toDate)
        {
            return fromDate.ToString("MMM d, yyyy");
        }

        if (fromDate.Year == toDate.Year && fromDate.Month == toDate.Month && fromDate.Day == 1
            && toDate.Day == DateTime.DaysInMonth(toDate.Year, toDate.Month))
        {
            return fromDate.ToString("MMMM yyyy");
        }

        return $"{fromDate:MMM d, yyyy} – {toDate:MMM d, yyyy}";
    }

    public static string PerformanceBand(double? deliveryHealth, double completionRate) =>
        (deliveryHealth ?? completionRate, completionRate) switch
        {
            ( >= 85, >= 70) => "Excellent",
            ( >= 70, >= 55) => "Strong",
            ( >= 50, >= 40) => "Steady",
            _ => "Watch"
        };

    public static string PerformanceTone(string band) => band switch
    {
        "Excellent" => "tone-excellent",
        "Strong" => "tone-strong",
        "Steady" => "tone-steady",
        _ => "tone-watch"
    };

    public static (DateOnly FromDate, DateOnly ToDate) GetPreviousPeriod(DateOnly fromDate, DateOnly toDate)
    {
        var lengthDays = toDate.DayNumber - fromDate.DayNumber + 1;
        var previousTo = fromDate.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(lengthDays - 1));
        return (previousFrom, previousTo);
    }

    public static KpiTrend TrendCount(int current, int previous, bool higherIsBetter)
    {
        if (current == previous)
        {
            return new KpiTrend(TrendDirection.Flat, true, 0, null);
        }

        var delta = current - previous;
        var direction = delta > 0 ? TrendDirection.Up : TrendDirection.Down;
        var isPositive = higherIsBetter ? delta > 0 : delta < 0;
        return new KpiTrend(direction, isPositive, delta, null);
    }

    public static KpiTrend TrendPercent(double? current, double? previous, bool higherIsBetter)
    {
        if (!current.HasValue || !previous.HasValue)
        {
            return KpiTrend.Unknown;
        }

        var delta = Math.Round(current.Value - previous.Value, 1);
        if (Math.Abs(delta) < 0.05)
        {
            return new KpiTrend(TrendDirection.Flat, true, null, 0);
        }

        var direction = delta > 0 ? TrendDirection.Up : TrendDirection.Down;
        var isPositive = higherIsBetter ? delta > 0 : delta < 0;
        return new KpiTrend(direction, isPositive, null, delta);
    }

    public static KpiTrend TrendDays(double? current, double? previous)
    {
        if (!current.HasValue || !previous.HasValue)
        {
            return KpiTrend.Unknown;
        }

        var delta = Math.Round(current.Value - previous.Value, 1);
        if (Math.Abs(delta) < 0.05)
        {
            return new KpiTrend(TrendDirection.Flat, true, delta, null);
        }

        var direction = delta > 0 ? TrendDirection.Up : TrendDirection.Down;
        var isPositive = delta < 0;
        return new KpiTrend(direction, isPositive, delta, null);
    }

    public static KpiTrendDisplay FormatTrend(KpiTrend trend, string countLabel = "tasks")
    {
        if (trend.Direction == TrendDirection.Unknown)
        {
            return new KpiTrendDisplay("—", "kpi-trend-neutral", "No prior period data");
        }

        if (trend.Direction == TrendDirection.Flat)
        {
            return new KpiTrendDisplay("No change", "kpi-trend-neutral", "Same as prior period");
        }

        var cssClass = trend.IsPositive ? "kpi-trend-positive" : "kpi-trend-negative";
        var arrow = trend.Direction == TrendDirection.Up ? "↑" : "↓";

        if (trend.DeltaPoints.HasValue)
        {
            var sign = trend.DeltaPoints.Value > 0 ? "+" : "";
            var text = $"{arrow} {sign}{trend.DeltaPoints.Value:0.#} pts";
            return new KpiTrendDisplay(text, cssClass, $"{sign}{trend.DeltaPoints.Value:0.#} percentage points vs prior period");
        }

        if (trend.DeltaAbsolute.HasValue)
        {
            var delta = trend.DeltaAbsolute.Value;
            var sign = delta > 0 ? "+" : "";
            var suffix = countLabel switch
            {
                "days" => "d",
                _ => ""
            };
            var text = suffix == "d"
                ? $"{arrow} {sign}{delta:0.#}{suffix}"
                : $"{arrow} {sign}{delta:0.#}";
            return new KpiTrendDisplay(text, cssClass, $"{sign}{delta:0.#} {countLabel} vs prior period");
        }

        return new KpiTrendDisplay("—", "kpi-trend-neutral", "No comparison available");
    }

    public static IReadOnlyList<SlaCheckResult> EvaluateTeamSla(TeamKpis kpis, SlaTargets? targets = null)
    {
        targets ??= DefaultSlaTargets;

        return
        [
            new SlaCheckResult(
                "Completion Rate",
                kpis.CompletionRate >= targets.MinCompletionRate,
                FormatPercent(kpis.CompletionRate),
                $"≥ {targets.MinCompletionRate:0.#}%",
                "Share of tasks completed in the period"),
            new SlaCheckResult(
                "Delivery Health",
                kpis.OnTimeRate.HasValue && kpis.OnTimeRate.Value >= targets.MinDeliveryHealth,
                FormatPercent(kpis.OnTimeRate),
                $"≥ {targets.MinDeliveryHealth:0.#}%",
                "On-time completions vs overdue pressure"),
            new SlaCheckResult(
                "Avg. Cycle Time",
                !kpis.AverageCycleDays.HasValue || kpis.AverageCycleDays.Value <= targets.MaxAverageCycleDays,
                FormatDays(kpis.AverageCycleDays),
                $"≤ {targets.MaxAverageCycleDays:0.#}d",
                "Weighted average days to complete"),
            new SlaCheckResult(
                "Overdue Load",
                kpis.OverdueCount <= targets.MaxTeamOverdueCount,
                kpis.OverdueCount.ToString(),
                $"≤ {targets.MaxTeamOverdueCount}",
                "Tasks past due or completed late")
        ];
    }

    public static IReadOnlyList<SlaCheckResult> EvaluateDeveloperSla(DeveloperKpi developer, SlaTargets? targets = null)
    {
        targets ??= DefaultSlaTargets;

        return
        [
            new SlaCheckResult(
                "Completion Rate",
                developer.CompletionRate >= targets.MinCompletionRate,
                FormatPercent(developer.CompletionRate),
                $"≥ {targets.MinCompletionRate:0.#}%",
                "Share of assigned tasks completed"),
            new SlaCheckResult(
                "Delivery Health",
                developer.OnTimeRate.HasValue && developer.OnTimeRate.Value >= targets.MinDeliveryHealth,
                FormatPercent(developer.OnTimeRate),
                $"≥ {targets.MinDeliveryHealth:0.#}%",
                "On-time vs overdue for this developer"),
            new SlaCheckResult(
                "Avg. Cycle Time",
                !developer.AverageCycleDays.HasValue || developer.AverageCycleDays.Value <= targets.MaxAverageCycleDays,
                FormatDays(developer.AverageCycleDays),
                $"≤ {targets.MaxAverageCycleDays:0.#}d",
                "Average completion days"),
            new SlaCheckResult(
                "Overdue Tasks",
                developer.OverdueCount == 0,
                developer.OverdueCount.ToString(),
                "0",
                "Open or completed late tasks")
        ];
    }

    public static bool MeetsAllSla(IReadOnlyList<SlaCheckResult> checks) =>
        checks.All(c => c.Met);

    public static string FormatSlaTooltip(IReadOnlyList<SlaCheckResult> checks) =>
        string.Join("; ", checks.Select(c => $"{c.Metric}: {c.Actual} (target {c.Target})"));

    public static IReadOnlyList<AtRiskDeveloperEntry> BuildAtRiskDevelopers(TeamKpis kpis) =>
        kpis.Developers
            .Select(dev =>
            {
                var checks = EvaluateDeveloperSla(dev);
                var missed = checks.Where(c => !c.Met).ToList();
                return missed.Count == 0 ? null : new AtRiskDeveloperEntry(dev, missed);
            })
            .Where(entry => entry is not null)
            .Cast<AtRiskDeveloperEntry>()
            .OrderByDescending(entry => entry.MissedChecks.Count)
            .ThenByDescending(entry => entry.Developer.OverdueCount)
            .ToList();

    public static IReadOnlyList<NamedCount> GroupByPerformanceBand(IEnumerable<DeveloperKpi> developers)
    {
        var order = new[] { "Excellent", "Strong", "Steady", "Watch" };
        var counts = developers
            .GroupBy(d => PerformanceBand(d.OnTimeRate, d.CompletionRate))
            .ToDictionary(g => g.Key, g => g.Count());

        return order
            .Select(name => new NamedCount(name, counts.GetValueOrDefault(name)))
            .ToList();
    }

    public static IReadOnlyList<NamedCount> GroupCompletedByWeek(
        IEnumerable<DeveloperReportTaskDto> tasks,
        DateOnly fromDate,
        DateOnly toDate)
    {
        var buckets = new Dictionary<DateOnly, int>();
        var cursor = fromDate;
        while (cursor <= toDate)
        {
            buckets[cursor] = 0;
            cursor = cursor.AddDays(7);
        }

        foreach (var task in tasks.Where(t => t.IsCompleted && t.DateDone.HasValue))
        {
            var doneDate = DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeMilliseconds(task.DateDone!.Value).ToLocalTime().DateTime);

            if (doneDate < fromDate || doneDate > toDate)
            {
                continue;
            }

            var weekStart = fromDate.AddDays((doneDate.DayNumber - fromDate.DayNumber) / 7 * 7);
            if (buckets.ContainsKey(weekStart))
            {
                buckets[weekStart]++;
            }
        }

        return buckets
            .OrderBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                var weekEnd = kvp.Key.AddDays(6);
                if (weekEnd > toDate)
                {
                    weekEnd = toDate;
                }

                var label = kvp.Key == weekEnd
                    ? kvp.Key.ToString("MMM d")
                    : $"{kvp.Key:MMM d}–{weekEnd:MMM d}";

                return new NamedCount(label, kvp.Value);
            })
            .ToList();
    }
}
