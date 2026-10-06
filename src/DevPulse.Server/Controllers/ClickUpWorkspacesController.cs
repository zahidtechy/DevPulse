namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/clickup/workspaces")]
[Authorize]
public sealed class ClickUpWorkspacesController : ControllerBase
{
    private readonly IClickUpAccountService _accountService;

    public ClickUpWorkspacesController(IClickUpAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("{workspaceId}/users/by-email")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ClickUpUserLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByEmail(
        string workspaceId,
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.GetMemberByEmailAsync(workspaceId, email, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               result.Error.Contains("No workspace member", StringComparison.OrdinalIgnoreCase) ||
               result.Error.Contains("No ClickUp account", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { error = result.Error })
            : BadRequest(new { error = result.Error });
    }
}
