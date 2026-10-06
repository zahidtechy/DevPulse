namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/attendance/settings")]
[Authorize(Policy = "AdminOnly")]
public sealed class AttendanceSettingsController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceSettingsController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AttendanceSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await _attendanceService.GetSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    [ProducesResponseType(typeof(AttendanceSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateAttendanceSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _attendanceService.UpdateSettingsAsync(request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }
}
