namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(AttendanceMeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var me = await _attendanceService.GetMeAsync(GetUserEmail(), cancellationToken);
        return Ok(me);
    }

    [HttpPost("punch")]
    [ProducesResponseType(typeof(AttendancePunchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Punch(CancellationToken cancellationToken)
    {
        var result = await _attendanceService.PunchAsync(GetUserEmail(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpGet("my-history")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyHistory([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        var history = await _attendanceService.GetMyHistoryAsync(GetUserEmail(), from, to, cancellationToken);
        return Ok(history);
    }

    [HttpPost("correction-requests")]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitCorrectionRequest(
        [FromBody] CreateAttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _attendanceService.SubmitCorrectionRequestAsync(GetUserEmail(), request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpGet("correction-requests/mine")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceCorrectionRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCorrectionRequests(CancellationToken cancellationToken)
    {
        var requests = await _attendanceService.GetMyCorrectionRequestsAsync(GetUserEmail(), cancellationToken);
        return Ok(requests);
    }

    [HttpDelete("correction-requests/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelCorrectionRequest(Guid id, CancellationToken cancellationToken)
    {
        var result = await _attendanceService.CancelCorrectionRequestAsync(GetUserEmail(), id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpGet("records")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRecords(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? developerId,
        CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        var records = await _attendanceService.GetRecordsAsync(from, to, developerId, cancellationToken);
        return Ok(records);
    }

    [HttpGet("analytics")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AttendanceAnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAnalytics([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        var analytics = await _attendanceService.GetAnalyticsAsync(from, to, cancellationToken);
        return Ok(analytics);
    }

    [HttpGet("correction-requests/pending")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceCorrectionRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingCorrectionRequests(CancellationToken cancellationToken)
    {
        var requests = await _attendanceService.GetPendingCorrectionRequestsAsync(cancellationToken);
        return Ok(requests);
    }

    [HttpPost("correction-requests/{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveCorrectionRequest(
        Guid id,
        [FromBody] ReviewAttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _attendanceService.ApproveCorrectionRequestAsync(id, GetUserId(), request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpPost("correction-requests/{id:guid}/reject")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AttendanceCorrectionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectCorrectionRequest(
        Guid id,
        [FromBody] RejectAttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _attendanceService.RejectCorrectionRequestAsync(id, GetUserId(), request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpPut("records/{developerId:guid}/{workDate}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AttendanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminUpsertRecord(
        Guid developerId,
        DateOnly workDate,
        [FromBody] AdminUpsertAttendanceRecordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _attendanceService.AdminUpsertRecordAsync(developerId, workDate, request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    private string GetUserEmail() =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private Guid GetUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : Guid.Empty;
    }
}
