namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/hubstaff/organizations")]
[Authorize]
public sealed class HubstaffOrganizationsController : ControllerBase
{
    private readonly IHubstaffOrganizationService _organizationService;

    public HubstaffOrganizationsController(IHubstaffOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    [Authorize(Policy = "CanViewReports")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var organizations = await _organizationService.GetAllAsync(cancellationToken);
        return Ok(organizations);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanViewReports")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateHubstaffOrganizationRequest request, CancellationToken cancellationToken)
    {
        var result = await _organizationService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHubstaffOrganizationRequest request, CancellationToken cancellationToken)
    {
        var result = await _organizationService.UpdateAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateHubstaffOrganizationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _organizationService.UpdateStatusAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error });
    }

    [HttpGet("{id:guid}/test")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationService.TestConnectionAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost("discover")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Discover([FromBody] DiscoverHubstaffOrganizationsRequest request, CancellationToken cancellationToken)
    {
        var result = await _organizationService.DiscoverOrganizationsAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetMembersAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
