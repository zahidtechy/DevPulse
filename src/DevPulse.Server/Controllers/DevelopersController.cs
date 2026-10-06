namespace DevPulse.Server.Controllers;

[ApiController]
[Route("api/developers")]
[Authorize]
public sealed class DevelopersController : ControllerBase
{
    private readonly IDeveloperService _developerService;

    public DevelopersController(IDeveloperService developerService)
    {
        _developerService = developerService;
    }

    [HttpGet]
    [Authorize(Policy = "CanViewReports")]
    [ProducesResponseType(typeof(IReadOnlyList<DeveloperDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var developers = await _developerService.GetAllAsync(cancellationToken);
        return Ok(developers);
    }

    [HttpGet("registry")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<DeveloperDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegistry(
        [FromQuery] DeveloperRegistryQuery query,
        CancellationToken cancellationToken)
    {
        var developers = await _developerService.GetRegistryAsync(query, cancellationToken);
        return Ok(developers);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanViewReports")]
    [ProducesResponseType(typeof(DeveloperDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _developerService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(DeveloperDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDeveloperRequest request, CancellationToken cancellationToken)
    {
        var result = await _developerService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(DeveloperDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeveloperRequest request, CancellationToken cancellationToken)
    {
        var result = await _developerService.UpdateAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _developerService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : NotFound(new { error = result.Error });
    }

    [HttpPost("{id:guid}/mappings")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(DeveloperDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMapping(Guid id, [FromBody] AddDeveloperMappingRequest request, CancellationToken cancellationToken)
    {
        var result = await _developerService.AddMappingAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/mappings/by-email")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(DeveloperDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMappingByEmail(
        Guid id,
        [FromBody] AddDeveloperMappingByEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _developerService.AddMappingByEmailAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error, errors = result.Errors });
        }

        return Ok(result.Value);
    }

    [HttpPost("sync")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(SyncDevelopersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SyncFromClickUp(
        [FromBody] SyncFromClickUpRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _developerService.SyncFromClickUpAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("{id:guid}/hubstaff-mappings")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AddHubstaffMapping(
        Guid id,
        [FromBody] AddDeveloperHubstaffMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _developerService.AddHubstaffMappingAsync(id, request, cancellationToken);
        return result.IsFailure ? BadRequest(new { error = result.Error }) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/hubstaff-mappings/by-email")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AddHubstaffMappingByEmail(
        Guid id,
        [FromBody] AddDeveloperHubstaffMappingByEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _developerService.AddHubstaffMappingByEmailAsync(id, request, cancellationToken);
        return result.IsFailure ? BadRequest(new { error = result.Error }) : Ok(result.Value);
    }

    [HttpDelete("{id:guid}/hubstaff-mappings/{mappingId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemoveHubstaffMapping(Guid id, Guid mappingId, CancellationToken cancellationToken)
    {
        var result = await _developerService.RemoveHubstaffMappingAsync(id, mappingId, cancellationToken);
        return result.IsFailure ? BadRequest(new { error = result.Error }) : Ok(result.Value);
    }

    [HttpPost("sync/hubstaff")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SyncFromHubstaff(
        [FromBody] SyncFromHubstaffRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _developerService.SyncFromHubstaffAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
