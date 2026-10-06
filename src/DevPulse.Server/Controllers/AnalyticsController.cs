namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "CanViewReports")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ICachedAnalyticsService _cachedAnalyticsService;
    private readonly IKpiSyncService _kpiSyncService;

    public AnalyticsController(
        ICachedAnalyticsService cachedAnalyticsService,
        IKpiSyncService kpiSyncService)
    {
        _cachedAnalyticsService = cachedAnalyticsService;
        _kpiSyncService = kpiSyncService;
    }

    /// <summary>
    /// Generates KPI analytics from synced database data (not live ClickUp).
    /// </summary>
    [HttpPost("from-database")]
    [ProducesResponseType(typeof(CachedAnalyticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFromDatabase(
        [FromBody] CachedAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DeveloperIds is null || request.DeveloperIds.Count == 0)
        {
            return BadRequest(new { error = "Select at least one developer." });
        }

        try
        {
            var result = await _cachedAnalyticsService.GetAnalyticsFromDatabaseAsync(request, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generates a developer report from synced database tasks.
    /// </summary>
    [HttpPost("report-from-database")]
    [ProducesResponseType(typeof(DeveloperReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportFromDatabase(
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
            return result.IsSuccess
                ? Ok(result.Value)
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("sync/status")]
    [ProducesResponseType(typeof(KpiSyncStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSyncStatus(CancellationToken cancellationToken)
    {
        var status = await _kpiSyncService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("sync")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(KpiSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunSync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _kpiSyncService.SyncAsync(triggeredManually: true, cancellationToken);
            return result.IsSuccess
                ? Ok(result.Value)
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
