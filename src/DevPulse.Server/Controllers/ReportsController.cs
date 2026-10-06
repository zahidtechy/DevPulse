namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "CanViewReports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ICachedAnalyticsService _cachedAnalyticsService;
    private readonly IReportExportService _reportExportService;

    public ReportsController(
        ICachedAnalyticsService cachedAnalyticsService,
        IReportExportService reportExportService)
    {
        _cachedAnalyticsService = cachedAnalyticsService;
        _reportExportService = reportExportService;
    }

    [HttpPost("developer-tasks")]
    [ProducesResponseType(typeof(DeveloperReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateDeveloperReport(
        [FromBody] DeveloperReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DeveloperIds is null || request.DeveloperIds.Count == 0)
        {
            return BadRequest(new { error = "Select at least one developer." });
        }

        try
        {
            var result = await _cachedAnalyticsService.GenerateReportFromDatabaseAsync(request, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error, errors = result.Errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("developer-tasks/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ExportDeveloperReport([FromBody] DeveloperReportResponse report)
    {
        if (report is null)
        {
            return BadRequest(new { error = "Report data is required." });
        }

        try
        {
            var fileBytes = _reportExportService.ExportDeveloperReportToExcel(report);
            var fileName = $"{AppBranding.CompanyName}-Developer-Work-{report.FromDate:yyyy-MM-dd}_to_{report.ToDate:yyyy-MM-dd}.xlsx";
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
