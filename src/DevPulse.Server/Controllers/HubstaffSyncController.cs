namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/hubstaff/sync")]
[Authorize(Policy = "CanViewReports")]
public sealed class HubstaffSyncController : ControllerBase
{
    private readonly IHubstaffSyncService _syncService;

    public HubstaffSyncController(IHubstaffSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _syncService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("trigger")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Trigger([FromBody] HubstaffSyncTriggerRequest? request, CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncAsync(triggeredManually: true, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
