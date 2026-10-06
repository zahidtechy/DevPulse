namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/leave/settings")]
[Authorize(Policy = "AdminOnly")]
public sealed class LeaveSettingsController : ControllerBase
{
    private readonly ILeaveService _leaveService;

    public LeaveSettingsController(ILeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(LeaveSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await _leaveService.GetSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    [ProducesResponseType(typeof(LeaveSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateLeaveSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _leaveService.UpdateSettingsAsync(request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpPost("test-telegram")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestTelegram(CancellationToken cancellationToken)
    {
        var result = await _leaveService.SendTestTelegramAsync(cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error, errors = result.Errors });
    }
}
