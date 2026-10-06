namespace DevPulse.Application.Services.Hubstaff;

public sealed class HubstaffOrganizationService : IHubstaffOrganizationService
{
    private readonly IHubstaffOrganizationRepository _repository;
    private readonly IHubstaffTokenProvider _tokenProvider;
    private readonly IHubstaffApiClient _apiClient;
    private readonly IHubstaffTokenProtector _tokenProtector;
    private readonly ILogger<HubstaffOrganizationService> _logger;

    public HubstaffOrganizationService(
        IHubstaffOrganizationRepository repository,
        IHubstaffTokenProvider tokenProvider,
        IHubstaffApiClient apiClient,
        IHubstaffTokenProtector tokenProtector,
        ILogger<HubstaffOrganizationService> logger)
    {
        _repository = repository;
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;
        _tokenProtector = tokenProtector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HubstaffOrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _repository.GetAllAsync(cancellationToken);
        return organizations.Select(MapToDto).ToList();
    }

    public async Task<Result<HubstaffOrganizationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        return organization is null
            ? Result<HubstaffOrganizationDto>.Failure("Hubstaff organization was not found.")
            : Result<HubstaffOrganizationDto>.Success(MapToDto(organization));
    }

    public async Task<Result<HubstaffOrganizationDto>> CreateAsync(
        CreateHubstaffOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<HubstaffOrganizationDto>.Failure("Organization name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            return Result<HubstaffOrganizationDto>.Failure("Personal access token is required.");
        }

        var exchange = await _tokenProvider.ExchangePatAsync(request.PersonalAccessToken.Trim(), cancellationToken);
        var organizations = await _apiClient.GetOrganizationsAsync(exchange.AccessToken, cancellationToken);
        if (organizations.Count == 0)
        {
            return Result<HubstaffOrganizationDto>.Failure("No Hubstaff organizations were found for this PAT.");
        }

        var selected = request.OrganizationId.HasValue
            ? organizations.FirstOrDefault(o => o.OrganizationId == request.OrganizationId.Value)
            : organizations.Count == 1 ? organizations[0] : null;

        if (selected is null)
        {
            return request.OrganizationId.HasValue
                ? Result<HubstaffOrganizationDto>.Failure("The selected Hubstaff organization was not found for this PAT.")
                : Result<HubstaffOrganizationDto>.Failure("Multiple Hubstaff organizations found. Select one before saving.");
        }

        if (await _repository.ExistsByOrganizationIdAsync(selected.OrganizationId, cancellationToken: cancellationToken))
        {
            return Result<HubstaffOrganizationDto>.Failure("This Hubstaff organization is already configured.");
        }

        var members = await _apiClient.GetMembersAsync(selected.OrganizationId, exchange.AccessToken, cancellationToken: cancellationToken);

        var organization = new HubstaffOrganization
        {
            Name = request.Name.Trim(),
            OrganizationId = selected.OrganizationId,
            HubstaffOrganizationName = selected.Name,
            EncryptedPersonalAccessToken = _tokenProtector.Protect(exchange.RefreshToken),
            PatExpiresAtUtc = DateTime.UtcNow.AddDays(90),
            IsActive = true,
            LastValidatedAtUtc = DateTime.UtcNow,
            LastValidationMessage = $"Connected successfully. {members.Count} member(s) accessible."
        };

        await _repository.AddAsync(organization, cancellationToken);
        _logger.LogInformation("Created Hubstaff organization {Name} ({OrganizationId})", organization.Name, organization.OrganizationId);

        return Result<HubstaffOrganizationDto>.Success(MapToDto(organization));
    }

    public async Task<Result<HubstaffOrganizationDto>> UpdateAsync(
        Guid id,
        UpdateHubstaffOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return Result<HubstaffOrganizationDto>.Failure("Hubstaff organization was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<HubstaffOrganizationDto>.Failure("Organization name is required.");
        }

        if (await _repository.ExistsByOrganizationIdAsync(request.OrganizationId, id, cancellationToken))
        {
            return Result<HubstaffOrganizationDto>.Failure("Another record is already linked to this Hubstaff organization ID.");
        }

        if (!string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            var exchange = await _tokenProvider.ExchangePatAsync(request.PersonalAccessToken.Trim(), cancellationToken);
            var orgs = await _apiClient.GetOrganizationsAsync(exchange.AccessToken, cancellationToken);
            if (!orgs.Any(o => o.OrganizationId == request.OrganizationId))
            {
                return Result<HubstaffOrganizationDto>.Failure("The PAT does not have access to the specified Hubstaff organization.");
            }

            organization.EncryptedPersonalAccessToken = _tokenProtector.Protect(exchange.RefreshToken);
            organization.PatExpiresAtUtc = DateTime.UtcNow.AddDays(90);
            organization.LastValidatedAtUtc = DateTime.UtcNow;
            organization.LastValidationMessage = "PAT updated and validated successfully.";
            _tokenProvider.InvalidateCache(id);
        }

        organization.Name = request.Name.Trim();
        organization.OrganizationId = request.OrganizationId;
        var matched = await DiscoverNameForOrgAsync(organization, cancellationToken);
        if (matched is not null)
        {
            organization.HubstaffOrganizationName = matched;
        }

        await _repository.UpdateAsync(organization, cancellationToken);
        return Result<HubstaffOrganizationDto>.Success(MapToDto(organization));
    }

    public async Task<Result<HubstaffOrganizationDto>> UpdateStatusAsync(
        Guid id,
        UpdateHubstaffOrganizationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return Result<HubstaffOrganizationDto>.Failure("Hubstaff organization was not found.");
        }

        organization.IsActive = request.IsActive;
        await _repository.UpdateAsync(organization, cancellationToken);
        return Result<HubstaffOrganizationDto>.Success(MapToDto(organization));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return Result.Failure("Hubstaff organization was not found.");
        }

        if (await _repository.HasDailyActivitiesAsync(id, cancellationToken))
        {
            return Result.Failure("Cannot delete this organization while synced activity data exists.");
        }

        await _repository.DeleteAsync(id, cancellationToken);
        _tokenProvider.InvalidateCache(id);
        return Result.Success();
    }

    public async Task<Result<HubstaffConnectionTestDto>> TestConnectionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return Result<HubstaffConnectionTestDto>.Failure("Hubstaff organization was not found.");
        }

        try
        {
            _tokenProvider.InvalidateCache(id);
            var accessToken = await _tokenProvider.GetAccessTokenAsync(id, cancellationToken);
            var orgs = await _apiClient.GetOrganizationsAsync(accessToken, cancellationToken);
            var matched = orgs.FirstOrDefault(o => o.OrganizationId == organization.OrganizationId);

            organization.LastValidatedAtUtc = DateTime.UtcNow;
            organization.LastValidationMessage = matched is null
                ? "Connected, but configured organization ID was not returned by Hubstaff."
                : "Connection successful.";
            if (matched is not null)
            {
                organization.HubstaffOrganizationName = matched.Name;
            }

            await _repository.UpdateAsync(organization, cancellationToken);

            return Result<HubstaffConnectionTestDto>.Success(new HubstaffConnectionTestDto(
                organization.Id,
                organization.Name,
                matched is not null,
                matched is not null ? "Connected" : "Warning",
                organization.HubstaffOrganizationName,
                organization.LastValidationMessage));
        }
        catch (Exception ex)
        {
            organization.LastValidatedAtUtc = DateTime.UtcNow;
            organization.LastValidationMessage = ex.Message;
            await _repository.UpdateAsync(organization, cancellationToken);

            return Result<HubstaffConnectionTestDto>.Success(new HubstaffConnectionTestDto(
                organization.Id,
                organization.Name,
                false,
                "Failed",
                organization.HubstaffOrganizationName,
                ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<HubstaffOrganizationDiscoveryDto>>> DiscoverOrganizationsAsync(
        DiscoverHubstaffOrganizationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            return Result<IReadOnlyList<HubstaffOrganizationDiscoveryDto>>.Failure("Personal access token is required.");
        }

        var exchange = await _tokenProvider.ExchangePatAsync(request.PersonalAccessToken.Trim(), cancellationToken);
        var organizations = await _apiClient.GetOrganizationsAsync(exchange.AccessToken, cancellationToken);
        var dtos = organizations
            .Select(o => new HubstaffOrganizationDiscoveryDto(o.OrganizationId, o.Name))
            .ToList();

        return Result<IReadOnlyList<HubstaffOrganizationDiscoveryDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<HubstaffMemberDto>>> GetMembersAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var organization = await _repository.GetByIdAsync(id, cancellationToken);
        if (organization is null)
        {
            return Result<IReadOnlyList<HubstaffMemberDto>>.Failure("Hubstaff organization was not found.");
        }

        var accessToken = await _tokenProvider.GetAccessTokenAsync(id, cancellationToken);
        var members = await _apiClient.GetMembersAsync(organization.OrganizationId, accessToken, cancellationToken: cancellationToken);
        var dtos = members
            .Select(m => new HubstaffMemberDto(m.UserId, m.Name, m.Email))
            .OrderBy(m => m.Name)
            .ToList();

        return Result<IReadOnlyList<HubstaffMemberDto>>.Success(dtos);
    }

    private async Task<string?> DiscoverNameForOrgAsync(HubstaffOrganization organization, CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(organization.Id, cancellationToken);
            var orgs = await _apiClient.GetOrganizationsAsync(accessToken, cancellationToken);
            return orgs.FirstOrDefault(o => o.OrganizationId == organization.OrganizationId)?.Name;
        }
        catch
        {
            return organization.HubstaffOrganizationName;
        }
    }

    private static HubstaffOrganizationDto MapToDto(HubstaffOrganization organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.OrganizationId,
            organization.HubstaffOrganizationName,
            organization.IsActive,
            !string.IsNullOrWhiteSpace(organization.EncryptedPersonalAccessToken),
            organization.PatExpiresAtUtc,
            organization.LastSyncedToDate,
            organization.CreatedAtUtc,
            organization.LastValidatedAtUtc,
            organization.LastValidationMessage);
}
