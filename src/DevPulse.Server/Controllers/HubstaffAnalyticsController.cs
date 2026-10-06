namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/hubstaff/analytics")]
[Authorize(Policy = "CanViewReports")]
public sealed class HubstaffAnalyticsController : ControllerBase
{
    private readonly IHubstaffAnalyticsService _analyticsService;

    public HubstaffAnalyticsController(IHubstaffAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] Guid hubstaffOrganizationId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid[]? developerIds,
        [FromQuery] bool includeUnmapped = false,
        CancellationToken cancellationToken = default)
    {
        var request = new HubstaffAnalyticsRequest(
            hubstaffOrganizationId,
            fromDate,
            toDate,
            developerIds,
            includeUnmapped);

        var result = await _analyticsService.GetAnalyticsAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
