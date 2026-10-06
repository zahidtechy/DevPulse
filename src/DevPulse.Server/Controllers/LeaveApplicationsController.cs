namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/leave")]
[Authorize]
public sealed class LeaveApplicationsController : ControllerBase
{
    private readonly ILeaveService _leaveService;

    public LeaveApplicationsController(ILeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(LeaveMeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var email = GetUserEmail();
        var me = await _leaveService.GetMeAsync(email, cancellationToken);
        return Ok(me);
    }

    [HttpGet("colleagues")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveColleagueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetColleagues(CancellationToken cancellationToken)
    {
        var colleagues = await _leaveService.GetColleaguesAsync(cancellationToken);
        return Ok(colleagues);
    }

    [HttpGet("balances")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBalances([FromQuery] int year, CancellationToken cancellationToken)
    {
        if (year < 1900 || year > 2100)
        {
            return BadRequest(new { error = "Year must be between 1900 and 2100." });
        }

        var email = GetUserEmail();
        var balances = await _leaveService.GetBalancesAsync(email, year, cancellationToken);
        return Ok(balances);
    }

    [HttpGet("analytics")]
    [Authorize(Policy = "CanViewReports")]
    [ProducesResponseType(typeof(LeaveAnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAnalytics([FromQuery] int year, CancellationToken cancellationToken)
    {
        if (year < 1900 || year > 2100)
        {
            return BadRequest(new { error = "Year must be between 1900 and 2100." });
        }

        var analytics = await _leaveService.GetTeamAnalyticsAsync(year, cancellationToken);
        return Ok(analytics);
    }

    [HttpPost("calculate-days")]
    [ProducesResponseType(typeof(LeaveDayCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CalculateDays([FromBody] LeaveDayCountRequest request, CancellationToken cancellationToken)
    {
        var result = await _leaveService.CalculateDaysAsync(request, cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    [HttpPost("applications")]
    [ProducesResponseType(typeof(LeaveApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] CreateLeaveApplicationRequest request, CancellationToken cancellationToken)
    {
        var result = await _leaveService.SubmitApplicationAsync(GetUserEmail(), request, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetMyApplications), result.Value);
    }

    [HttpGet("applications/mine")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyApplications(CancellationToken cancellationToken)
    {
        var applications = await _leaveService.GetMyApplicationsAsync(GetUserEmail(), cancellationToken);
        return Ok(applications);
    }

    [HttpGet("applications/pending-approval")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingForApproval(CancellationToken cancellationToken)
    {
        var applications = await _leaveService.GetPendingForApproverAsync(GetUserEmail(), cancellationToken);
        return Ok(applications);
    }

    [HttpGet("applications")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var applications = await _leaveService.GetAllApplicationsAsync(cancellationToken);
        return Ok(applications);
    }

    [HttpPost("applications/{id:guid}/approve")]
    [ProducesResponseType(typeof(LeaveApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewLeaveApplicationRequest request, CancellationToken cancellationToken)
    {
        var result = await _leaveService.ApproveAsync(GetUserEmail(), id, request, cancellationToken);
        return ToReviewResult(result);
    }

    [HttpPost("applications/{id:guid}/reject")]
    [ProducesResponseType(typeof(LeaveApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewLeaveApplicationRequest request, CancellationToken cancellationToken)
    {
        var result = await _leaveService.RejectAsync(GetUserEmail(), id, request, cancellationToken);
        return ToReviewResult(result);
    }

    [HttpPost("applications/{id:guid}/cancel")]
    [ProducesResponseType(typeof(LeaveApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _leaveService.CancelAsync(GetUserEmail(), id, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return Ok(result.Value);
    }

    private IActionResult ToReviewResult(Result<LeaveApplicationDto> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error = result.Error })
            : BadRequest(new { error = result.Error, errors = result.Errors });
    }

    private string GetUserEmail() =>
        User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
}
