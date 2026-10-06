using ClosedXML.Excel;

namespace DevPulse.Application.Services.Reports;

public sealed class ReportExcelExportService : IReportExportService
{
    private static readonly XLColor HeaderBackground = XLColor.FromHtml("#1F4E79");
    private static readonly XLColor HeaderText = XLColor.White;
    private static readonly XLColor TitleBackground = XLColor.FromHtml("#2E75B6");
    private static readonly XLColor AltRowBackground = XLColor.FromHtml("#F2F7FB");
    private static readonly XLColor BorderColor = XLColor.FromHtml("#B4C6E7");

    public byte[] ExportDeveloperReportToExcel(DeveloperReportResponse report)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Author = AppBranding.CompanyName;
        workbook.Properties.Title = "Developer Work Report";

        AddOverviewSheet(workbook, report);
        AddProductivitySheet(workbook, report);
        AddTaskDetailsSheet(workbook, report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddOverviewSheet(XLWorkbook workbook, DeveloperReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Overview");
        var periodLabel = FormatPeriod(report.FromDate, report.ToDate);
        var developersWithTasks = report.Developers.Count(d => d.TotalTasks > 0);

        ws.Cell(1, 1).Value = $"{AppBranding.CompanyName} Developer Work Report";
        ws.Range(1, 1, 1, 4).Merge();
        StyleTitleRow(ws.Range(1, 1, 1, 4));

        ws.Cell(3, 1).Value = "Report Period";
        ws.Cell(3, 2).Value = periodLabel;
        ws.Cell(4, 1).Value = "Generated";
        ws.Cell(4, 2).Value = DateTime.Now;
        ws.Cell(4, 2).Style.DateFormat.Format = "MMM d, yyyy h:mm tt";
        StyleLabelColumn(ws.Range(3, 1, 4, 1));

        var summaryStartRow = 6;
        ws.Cell(summaryStartRow, 1).Value = "Summary";
        ws.Range(summaryStartRow, 1, summaryStartRow, 2).Merge();
        StyleSectionHeader(ws.Range(summaryStartRow, 1, summaryStartRow, 2));

        var metrics = new (string Label, object Value)[]
        {
            ("Tasks Completed", report.TotalTasksCompleted),
            ("In Progress", report.TotalInProgress),
            ("Subtasks", report.Tasks.Count(t => t.IsSubtask)),
            ("Overdue Tasks", report.Developers.Sum(d => d.OverdueCount)),
            ("On-Time Completions", report.Developers.Sum(d => d.OnTimeCompletedCount)),
            ("Developers with Tasks", developersWithTasks),
            ("Workspaces Queried", report.WorkspaceCount),
            ("Total Task Records", report.Tasks.Count)
        };

        for (var i = 0; i < metrics.Length; i++)
        {
            var row = summaryStartRow + 1 + i;
            ws.Cell(row, 1).Value = metrics[i].Label;
            ws.Cell(row, 2).Value = XLCellValue.FromObject(metrics[i].Value);
            StyleMetricRow(ws.Range(row, 1, row, 2), i % 2 == 1);
        }

        ws.Column(1).Width = 28;
        ws.Column(2).Width = 22;
        ws.SheetView.Freeze(4, 0);
    }

    private static void AddProductivitySheet(XLWorkbook workbook, DeveloperReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Productivity Summary");
        var headers = new[] { "Developer", "Email", "Total", "Completed", "In Progress", "Subtasks", "Workspaces", "Projects", "Overdue", "On Time", "Avg. Completion (days)", "Workspace Breakdown", "Project Breakdown" };

        for (var col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
        }

        StyleHeaderRow(ws.Range(1, 1, 1, headers.Length));

        var summaries = report.Developers
            .Where(d => d.TotalTasks > 0)
            .OrderByDescending(d => d.TotalTasks)
            .ThenBy(d => d.DeveloperName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < summaries.Count; i++)
        {
            var row = i + 2;
            var summary = summaries[i];
            var breakdown = string.Join(", ", summary.ByWorkspace.Select(w => $"{w.AccountName}: {w.TaskCount}"));
            var projectBreakdown = string.Join(", ", summary.ByProject.Select(p => $"{p.ProjectName}: {p.TaskCount}"));

            ws.Cell(row, 1).Value = summary.DeveloperName;
            ws.Cell(row, 2).Value = summary.Email ?? string.Empty;
            ws.Cell(row, 3).Value = summary.TotalTasks;
            ws.Cell(row, 4).Value = summary.CompletedCount;
            ws.Cell(row, 5).Value = summary.InProgressCount;
            ws.Cell(row, 6).Value = summary.ChildTaskCount;
            ws.Cell(row, 7).Value = summary.WorkspaceCount;
            ws.Cell(row, 8).Value = summary.ProjectCount;
            ws.Cell(row, 9).Value = summary.OverdueCount;
            ws.Cell(row, 10).Value = summary.OnTimeCompletedCount;

            if (summary.AverageCompletionDays.HasValue)
            {
                ws.Cell(row, 11).Value = summary.AverageCompletionDays.Value;
                ws.Cell(row, 11).Style.NumberFormat.Format = "0.0";
            }

            ws.Cell(row, 12).Value = breakdown;
            ws.Cell(row, 13).Value = projectBreakdown;
            StyleDataRow(ws.Range(row, 1, row, headers.Length), i % 2 == 1);
        }

        ws.Column(1).Width = 24;
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 10;
        ws.Column(4).Width = 12;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 12;
        ws.Column(7).Width = 14;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 12;
        ws.Column(10).Width = 12;
        ws.Column(11).Width = 22;
        ws.Column(12).Width = 40;
        ws.Column(13).Width = 40;
        ws.Range(1, 1, Math.Max(1, summaries.Count + 1), headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
    }

    private static void AddTaskDetailsSheet(XLWorkbook workbook, DeveloperReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Task Details");
        var headers = new[]
        {
            "Developer", "Workspace", "Project", "Folder", "Kind", "Task", "List", "Priority",
            "Created", "Due", "Status", "State", "Completed", "Duration (days)",
            "Parent Task", "Parent Task ID", "Task URL"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
        }

        StyleHeaderRow(ws.Range(1, 1, 1, headers.Length));

        var tasks = report.Tasks
            .OrderBy(t => t.DeveloperName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.IsCompleted)
            .ThenBy(t => t.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(t => t.DateDone ?? 0)
            .ToList();

        for (var i = 0; i < tasks.Count; i++)
        {
            var row = i + 2;
            var task = tasks[i];

            ws.Cell(row, 1).Value = task.DeveloperName;
            ws.Cell(row, 2).Value = task.AccountName;
            ws.Cell(row, 3).Value = task.ProjectName ?? string.Empty;
            ws.Cell(row, 4).Value = task.FolderName ?? string.Empty;
            ws.Cell(row, 5).Value = task.IsSubtask ? "Subtask" : task.TaskType;
            ws.Cell(row, 6).Value = task.TaskName;
            ws.Cell(row, 7).Value = task.ListName ?? string.Empty;
            ws.Cell(row, 8).Value = task.Priority ?? string.Empty;

            if (task.DateCreated.HasValue)
            {
                ws.Cell(row, 9).Value = DateTimeOffset.FromUnixTimeMilliseconds(task.DateCreated.Value).LocalDateTime;
                ws.Cell(row, 9).Style.DateFormat.Format = "MMM d, yyyy";
            }

            if (task.DueDate.HasValue)
            {
                ws.Cell(row, 10).Value = DateTimeOffset.FromUnixTimeMilliseconds(task.DueDate.Value).LocalDateTime;
                ws.Cell(row, 10).Style.DateFormat.Format = "MMM d, yyyy";
            }

            ws.Cell(row, 11).Value = task.Status ?? string.Empty;
            ws.Cell(row, 12).Value = task.IsCompleted ? "Completed" : "In Progress";

            if (task.DateDone.HasValue)
            {
                ws.Cell(row, 13).Value = DateTimeOffset.FromUnixTimeMilliseconds(task.DateDone.Value).LocalDateTime;
                ws.Cell(row, 13).Style.DateFormat.Format = "MMM d, yyyy";
            }

            if (task.CompletionDays.HasValue)
            {
                ws.Cell(row, 14).Value = task.CompletionDays.Value;
                ws.Cell(row, 14).Style.NumberFormat.Format = "0.0";
            }

            ws.Cell(row, 15).Value = task.ParentTaskName ?? string.Empty;
            ws.Cell(row, 16).Value = task.ParentTaskId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(task.Url))
            {
                ws.Cell(row, 17).Value = task.Url;
                ws.Cell(row, 6).SetHyperlink(new XLHyperlink(task.Url));
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#0563C1");
                ws.Cell(row, 6).Style.Font.Underline = XLFontUnderlineValues.Single;
            }

            StyleDataRow(ws.Range(row, 1, row, headers.Length), i % 2 == 1);
        }

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 20;
        ws.Column(3).Width = 22;
        ws.Column(4).Width = 20;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 36;
        ws.Column(7).Width = 22;
        ws.Column(8).Width = 12;
        ws.Column(9).Width = 14;
        ws.Column(10).Width = 14;
        ws.Column(11).Width = 16;
        ws.Column(12).Width = 14;
        ws.Column(13).Width = 16;
        ws.Column(14).Width = 16;
        ws.Column(15).Width = 28;
        ws.Column(16).Width = 16;
        ws.Column(17).Width = 40;
        ws.Range(1, 1, Math.Max(1, tasks.Count + 1), headers.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
    }

    private static string FormatPeriod(DateOnly fromDate, DateOnly toDate)
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

    private static void StyleTitleRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 16;
        range.Style.Font.FontColor = HeaderText;
        range.Style.Fill.BackgroundColor = TitleBackground;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Worksheet.Row(range.FirstRow().RowNumber()).Height = 30;
    }

    private static void StyleSectionHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = HeaderText;
        range.Style.Fill.BackgroundColor = HeaderBackground;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ApplyBorder(range);
    }

    private static void StyleHeaderRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = HeaderText;
        range.Style.Fill.BackgroundColor = HeaderBackground;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        ApplyBorder(range);
        range.Worksheet.Row(range.FirstRow().RowNumber()).Height = 22;
    }

    private static void StyleLabelColumn(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = AltRowBackground;
    }

    private static void StyleMetricRow(IXLRange range, bool alternate)
    {
        if (alternate)
        {
            range.Style.Fill.BackgroundColor = AltRowBackground;
        }

        range.Cell(1, 1).Style.Font.Bold = true;
        ApplyBorder(range);
    }

    private static void StyleDataRow(IXLRange range, bool alternate)
    {
        if (alternate)
        {
            range.Style.Fill.BackgroundColor = AltRowBackground;
        }

        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ApplyBorder(range);
    }

    private static void ApplyBorder(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = BorderColor;
        range.Style.Border.InsideBorderColor = BorderColor;
    }
}
