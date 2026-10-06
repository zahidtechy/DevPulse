namespace DevPulse.Client.Analytics;

public sealed record DeveloperLeaveSummary(
    Guid DeveloperId,
    string DeveloperName,
    decimal UsedDays,
    decimal PendingDays,
    decimal RemainingDays,
    int DaysPerYear);

public sealed record LeaveTypeSummary(
    Guid LeaveTypeId,
    string LeaveTypeName,
    decimal UsedDays,
    decimal PendingDays,
    decimal RemainingDays);

public static class LeaveChartData
{
    public static string FormatDays(decimal days) =>
        days % 1 == 0 ? days.ToString("0") : days.ToString("0.#");

    public static IReadOnlyList<DeveloperLeaveSummary> AggregateByDeveloper(
        LeaveAnalyticsSummaryDto summary,
        Guid? leaveTypeId = null)
    {
        var query = summary.Balances.AsEnumerable();

        if (leaveTypeId.HasValue)
        {
            query = query.Where(b => b.LeaveTypeId == leaveTypeId.Value);
        }

        return query
            .GroupBy(b => new { b.DeveloperId, b.DeveloperName })
            .Select(g => new DeveloperLeaveSummary(
                g.Key.DeveloperId,
                g.Key.DeveloperName,
                g.Sum(x => x.UsedDays),
                g.Sum(x => x.PendingDays),
                g.Sum(x => x.RemainingDays),
                g.Sum(x => x.DaysPerYear)))
            .OrderByDescending(d => d.UsedDays + d.PendingDays)
            .ThenBy(d => d.DeveloperName)
            .ToList();
    }

    public static IReadOnlyList<LeaveTypeSummary> AggregateByLeaveType(LeaveAnalyticsSummaryDto summary) =>
        summary.Balances
            .GroupBy(b => new { b.LeaveTypeId, b.LeaveTypeName })
            .Select(g => new LeaveTypeSummary(
                g.Key.LeaveTypeId,
                g.Key.LeaveTypeName,
                g.Sum(x => x.UsedDays),
                g.Sum(x => x.PendingDays),
                g.Sum(x => x.RemainingDays)))
            .OrderByDescending(t => t.UsedDays + t.PendingDays)
            .ThenBy(t => t.LeaveTypeName)
            .ToList();

    public static IReadOnlyList<string> GetLeaveTypeNames(LeaveAnalyticsSummaryDto summary) =>
        summary.Balances
            .Select(b => b.LeaveTypeName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

    public static IReadOnlyList<(Guid Id, string Name)> GetLeaveTypes(LeaveAnalyticsSummaryDto summary) =>
        summary.Balances
            .GroupBy(b => new { b.LeaveTypeId, b.LeaveTypeName })
            .Select(g => (g.Key.LeaveTypeId, g.Key.LeaveTypeName))
            .OrderBy(t => t.LeaveTypeName)
            .ToList();

    public static double UtilizationPercent(decimal used, decimal pending, int daysPerYear)
    {
        if (daysPerYear <= 0)
        {
            return 0;
        }

        return (double)((used + pending) / daysPerYear * 100m);
    }

    public static double TeamUtilizationPercent(decimal used, decimal pending, decimal allocated)
    {
        if (allocated <= 0)
        {
            return 0;
        }

        return (double)((used + pending) / allocated * 100m);
    }

    public static decimal TotalAllocated(IEnumerable<DeveloperLeaveSummary> developers) =>
        developers.Sum(d => d.DaysPerYear);

    public static int CountWithPending(IEnumerable<DeveloperLeaveSummary> developers) =>
        developers.Count(d => d.PendingDays > 0);

    public static int CountHighUtilization(IEnumerable<DeveloperLeaveSummary> developers, double threshold = 90) =>
        developers.Count(d => UtilizationPercent(d.UsedDays, d.PendingDays, d.DaysPerYear) >= threshold);

    public static IReadOnlyList<DeveloperLeaveSummary> GetAttentionDevelopers(
        IEnumerable<DeveloperLeaveSummary> developers,
        int limit = 6) =>
        developers
            .Where(d => d.PendingDays > 0 || UtilizationPercent(d.UsedDays, d.PendingDays, d.DaysPerYear) >= 90)
            .OrderByDescending(d => d.PendingDays)
            .ThenByDescending(d => UtilizationPercent(d.UsedDays, d.PendingDays, d.DaysPerYear))
            .Take(limit)
            .ToList();

    public static string UtilizationBand(double percent) => percent switch
    {
        >= 90 => "Critical",
        >= 70 => "High",
        >= 40 => "Moderate",
        _ => "Healthy"
    };

    public static string UtilizationTone(double percent) => percent switch
    {
        >= 90 => "danger",
        >= 70 => "warning",
        _ => "accent"
    };

    public static string FormatPercent(double value) => $"{value:0.#}%";
}
