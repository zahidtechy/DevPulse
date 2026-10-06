namespace DevPulse.Application.Services.Developers;

public sealed class DeveloperService : IDeveloperService
{
    private readonly IDeveloperRepository _developerRepository;
    private readonly IClickUpAccountRepository _accountRepository;
    private readonly IClickUpAccountService _accountService;
    private readonly IClickUpApiClient _apiClient;
    private readonly IHubstaffOrganizationRepository _hubstaffOrganizationRepository;
    private readonly IHubstaffOrganizationService _hubstaffOrganizationService;
    private readonly IHubstaffDailyActivityRepository _hubstaffDailyActivityRepository;
    private readonly ITokenProtector _tokenProtector;
    private readonly ILogger<DeveloperService> _logger;

    public DeveloperService(
        IDeveloperRepository developerRepository,
        IClickUpAccountRepository accountRepository,
        IClickUpAccountService accountService,
        IClickUpApiClient apiClient,
        IHubstaffOrganizationRepository hubstaffOrganizationRepository,
        IHubstaffOrganizationService hubstaffOrganizationService,
        IHubstaffDailyActivityRepository hubstaffDailyActivityRepository,
        ITokenProtector tokenProtector,
        ILogger<DeveloperService> logger)
    {
        _developerRepository = developerRepository;
        _accountRepository = accountRepository;
        _accountService = accountService;
        _apiClient = apiClient;
        _hubstaffOrganizationRepository = hubstaffOrganizationRepository;
        _hubstaffOrganizationService = hubstaffOrganizationService;
        _hubstaffDailyActivityRepository = hubstaffDailyActivityRepository;
        _tokenProtector = tokenProtector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DeveloperDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var developers = await _developerRepository.GetAllAsync(cancellationToken);
        return developers.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<DeveloperDto>> GetRegistryAsync(
        DeveloperRegistryQuery query,
        CancellationToken cancellationToken = default)
    {
        bool? isActive = query.Status?.Trim().ToLowerInvariant() switch
        {
            "active" => true,
            "inactive" => false,
            _ => null
        };

        Domain.Enums.WorkRole? workRole = query.WorkRole.HasValue
            ? (Domain.Enums.WorkRole)(int)query.WorkRole.Value
            : null;

        var developers = await _developerRepository.GetRegistryAsync(
            query.ClickUpAccountId,
            isActive,
            workRole,
            query.Search,
            cancellationToken);

        return developers.Select(MapToDto).ToList();
    }

    public async Task<Result<DeveloperDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdWithMappingsAsync(id, cancellationToken);
        return developer is null
            ? Result<DeveloperDto>.Failure("Developer was not found.")
            : Result<DeveloperDto>.Success(MapToDto(developer));
    }

    public async Task<Result<DeveloperDto>> CreateAsync(CreateDeveloperRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateDeveloperRequest(request.Name, request.Email);
        if (validation.IsFailure)
        {
            return Result<DeveloperDto>.Failure(validation.Error!);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (normalizedEmail is not null)
        {
            var existing = await _developerRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existing is not null)
            {
                return Result<DeveloperDto>.Failure("A developer with this email already exists.");
            }
        }

        var managerValidation = await ValidateReportingManagerAsync(null, request.ReportingManagerDeveloperId, cancellationToken);
        if (managerValidation.IsFailure)
        {
            return Result<DeveloperDto>.Failure(managerValidation.Error!);
        }

        var developer = new Developer
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            WorkRole = (Domain.Enums.WorkRole)(int)request.WorkRole,
            ReportingManagerDeveloperId = request.ReportingManagerDeveloperId
        };

        await _developerRepository.AddAsync(developer, cancellationToken);
        _logger.LogInformation("Created developer {DeveloperName}", developer.Name);

        var created = await _developerRepository.GetByIdWithMappingsAsync(developer.Id, cancellationToken);
        return Result<DeveloperDto>.Success(MapToDto(created!));
    }

    public async Task<Result<DeveloperDto>> UpdateAsync(Guid id, UpdateDeveloperRequest request, CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdAsync(id, cancellationToken);
        if (developer is null)
        {
            return Result<DeveloperDto>.Failure("Developer was not found.");
        }

        var validation = ValidateDeveloperRequest(request.Name, request.Email);
        if (validation.IsFailure)
        {
            return Result<DeveloperDto>.Failure(validation.Error!);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        if (normalizedEmail is not null)
        {
            var existing = await _developerRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existing is not null && existing.Id != id)
            {
                return Result<DeveloperDto>.Failure("Another developer with this email already exists.");
            }
        }

        var managerValidation = await ValidateReportingManagerAsync(id, request.ReportingManagerDeveloperId, cancellationToken);
        if (managerValidation.IsFailure)
        {
            return Result<DeveloperDto>.Failure(managerValidation.Error!);
        }

        developer.Name = request.Name.Trim();
        developer.Email = normalizedEmail;
        developer.IsActive = request.IsActive;
        developer.WorkRole = (Domain.Enums.WorkRole)(int)request.WorkRole;
        developer.ReportingManagerDeveloperId = request.ReportingManagerDeveloperId;

        await _developerRepository.UpdateAsync(developer, cancellationToken);

        var updated = await _developerRepository.GetByIdWithMappingsAsync(id, cancellationToken);
        return Result<DeveloperDto>.Success(MapToDto(updated!));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdAsync(id, cancellationToken);
        if (developer is null)
        {
            return Result.Failure("Developer was not found.");
        }

        await _developerRepository.DeleteAsync(id, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<DeveloperDto>> AddMappingAsync(Guid developerId, AddDeveloperMappingRequest request, CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        if (developer is null)
        {
            return Result<DeveloperDto>.Failure("Developer was not found.");
        }

        var account = await _accountRepository.GetByIdAsync(request.ClickUpAccountId, cancellationToken);
        if (account is null)
        {
            return Result<DeveloperDto>.Failure("ClickUp account was not found.");
        }

        if (await _developerRepository.MappingExistsAsync(developerId, request.ClickUpAccountId, cancellationToken))
        {
            return Result<DeveloperDto>.Failure("This developer is already mapped to the selected workspace.");
        }

        var mapping = new DeveloperClickUpMapping
        {
            DeveloperId = developerId,
            ClickUpAccountId = request.ClickUpAccountId,
            ClickUpUserId = request.ClickUpUserId
        };

        await _developerRepository.AddMappingAsync(mapping, cancellationToken);

        var updated = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        return Result<DeveloperDto>.Success(MapToDto(updated!));
    }

    public async Task<Result<DeveloperDto>> AddMappingByEmailAsync(
        Guid developerId,
        AddDeveloperMappingByEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        if (developer is null)
        {
            return Result<DeveloperDto>.Failure("Developer was not found.");
        }

        var email = NormalizeEmail(request.Email) ?? NormalizeEmail(developer.Email);
        if (email is null)
        {
            return Result<DeveloperDto>.Failure("Email is required to resolve the ClickUp user.");
        }

        var lookup = await _accountService.GetMemberByEmailAsync(request.WorkspaceId, email, cancellationToken);
        if (lookup.IsFailure)
        {
            return Result<DeveloperDto>.Failure(lookup.Error!);
        }

        var member = lookup.Value!;
        if (await _developerRepository.MappingExistsAsync(developerId, member.AccountId, cancellationToken))
        {
            return Result<DeveloperDto>.Failure("This developer is already mapped to the selected workspace.");
        }

        await _developerRepository.AddMappingAsync(new DeveloperClickUpMapping
        {
            DeveloperId = developerId,
            ClickUpAccountId = member.AccountId,
            ClickUpUserId = member.ClickUpUserId
        }, cancellationToken);

        _logger.LogInformation(
            "Mapped developer {DeveloperId} to ClickUp user {ClickUpUserId} in workspace {WorkspaceId} via email lookup",
            developerId,
            member.ClickUpUserId,
            member.WorkspaceId);

        var updated = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        return Result<DeveloperDto>.Success(MapToDto(updated!));
    }

    public async Task<Result<SyncDevelopersResult>> SyncFromClickUpAsync(
        SyncFromClickUpRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClickUpAccount> accounts;
        if (request?.ClickUpAccountId is Guid accountId)
        {
            var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
            if (account is null)
            {
                return Result<SyncDevelopersResult>.Failure("ClickUp account was not found.");
            }

            accounts = [account];
        }
        else
        {
            accounts = await _accountRepository.GetAllAsync(cancellationToken);
            if (accounts.Count == 0)
            {
                return Result<SyncDevelopersResult>.Failure("No ClickUp accounts configured.");
            }
        }

        var created = 0;
        var updated = 0;
        var mappingsAdded = 0;

        foreach (var account in accounts)
        {
            if (DemoSeedData.IsDemoWorkspace(account.WorkspaceId))
            {
                continue;
            }

            var token = _tokenProtector.Unprotect(account.EncryptedAccessToken);
            var members = await _apiClient.GetWorkspaceMembersAsync(token, account.WorkspaceId, cancellationToken);

            foreach (var member in members)
            {
                var normalizedEmail = NormalizeEmail(member.Email);
                if (normalizedEmail is null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(member.Username))
                {
                    continue;
                }
                var developer = await _developerRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
                if (developer is null)
                {

                    developer = new Developer
                    {
                        Name = member.Username.Trim(),
                        Email = normalizedEmail
                    };
                    await _developerRepository.AddAsync(developer, cancellationToken);
                    created++;
                }
                else if (!string.Equals(developer.Name, member.Username.Trim(), StringComparison.Ordinal))
                {
                    developer.Name = member.Username.Trim();
                    await _developerRepository.UpdateAsync(developer, cancellationToken);
                    updated++;
                }

                if (!await _developerRepository.MappingExistsAsync(developer.Id, account.Id, cancellationToken))
                {
                    await _developerRepository.AddMappingAsync(new DeveloperClickUpMapping
                    {
                        DeveloperId = developer.Id,
                        ClickUpAccountId = account.Id,
                        ClickUpUserId = member.ClickUpUserId
                    }, cancellationToken);
                    mappingsAdded++;
                }
            }
        }

        _logger.LogInformation(
            "Synced developers from ClickUp: {Created} created, {Updated} updated, {MappingsAdded} mappings added",
            created, updated, mappingsAdded);

        return Result<SyncDevelopersResult>.Success(new SyncDevelopersResult(created, updated, mappingsAdded));
    }

    public async Task<Result<DeveloperDto>> AddHubstaffMappingAsync(
        Guid developerId,
        AddDeveloperHubstaffMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        if (developer is null)
        {
            return Result<DeveloperDto>.Failure("Developer was not found.");
        }

        var organization = await _hubstaffOrganizationRepository.GetByIdAsync(request.HubstaffOrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<DeveloperDto>.Failure("Hubstaff organization was not found.");
        }

        if (await _developerRepository.HubstaffMappingExistsAsync(developerId, request.HubstaffOrganizationId, cancellationToken))
        {
            return Result<DeveloperDto>.Failure("This developer is already mapped to the selected Hubstaff organization.");
        }

        await _developerRepository.AddHubstaffMappingAsync(new DeveloperHubstaffMapping
        {
            DeveloperId = developerId,
            HubstaffOrganizationId = request.HubstaffOrganizationId,
            HubstaffUserId = request.HubstaffUserId
        }, cancellationToken);

        await BackfillHubstaffActivitiesAsync(
            request.HubstaffOrganizationId,
            new Dictionary<int, Guid> { [request.HubstaffUserId] = developerId },
            cancellationToken);

        var updated = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        return Result<DeveloperDto>.Success(MapToDto(updated!));
    }

    public async Task<Result<DeveloperDto>> AddHubstaffMappingByEmailAsync(
        Guid developerId,
        AddDeveloperHubstaffMappingByEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result<DeveloperDto>.Failure("Email is required to resolve the Hubstaff user.");
        }

        var membersResult = await _hubstaffOrganizationService.GetMembersAsync(request.HubstaffOrganizationId, cancellationToken);
        if (membersResult.IsFailure || membersResult.Value is null)
        {
            return Result<DeveloperDto>.Failure(membersResult.Error ?? "Failed to load Hubstaff members.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var member = membersResult.Value.FirstOrDefault(m =>
            NormalizeEmail(m.Email) == normalizedEmail);

        if (member is null)
        {
            return Result<DeveloperDto>.Failure("No Hubstaff member matched the provided email.");
        }

        return await AddHubstaffMappingAsync(
            developerId,
            new AddDeveloperHubstaffMappingRequest(request.HubstaffOrganizationId, member.HubstaffUserId),
            cancellationToken);
    }

    public async Task<Result<DeveloperDto>> RemoveHubstaffMappingAsync(
        Guid developerId,
        Guid mappingId,
        CancellationToken cancellationToken = default)
    {
        var developer = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        if (developer is null)
        {
            return Result<DeveloperDto>.Failure("Developer was not found.");
        }

        if (developer.HubstaffMappings.All(m => m.Id != mappingId))
        {
            return Result<DeveloperDto>.Failure("Hubstaff mapping was not found for this developer.");
        }

        await _developerRepository.RemoveHubstaffMappingAsync(mappingId, cancellationToken);
        var updated = await _developerRepository.GetByIdWithMappingsAsync(developerId, cancellationToken);
        return Result<DeveloperDto>.Success(MapToDto(updated!));
    }

    public async Task<Result<SyncFromHubstaffResult>> SyncFromHubstaffAsync(
        SyncFromHubstaffRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<HubstaffOrganization> organizations;
        if (request?.HubstaffOrganizationId is Guid orgId)
        {
            var organization = await _hubstaffOrganizationRepository.GetByIdAsync(orgId, cancellationToken);
            if (organization is null)
            {
                return Result<SyncFromHubstaffResult>.Failure("Hubstaff organization was not found.");
            }

            organizations = [organization];
        }
        else
        {
            organizations = await _hubstaffOrganizationRepository.GetActiveAsync(cancellationToken);
            if (organizations.Count == 0)
            {
                return Result<SyncFromHubstaffResult>.Failure("No Hubstaff organizations configured.");
            }
        }

        var created = 0;
        var updated = 0;
        var mappingsAdded = 0;

        foreach (var organization in organizations)
        {
            var membersResult = await _hubstaffOrganizationService.GetMembersAsync(organization.Id, cancellationToken);
            if (membersResult.IsFailure || membersResult.Value is null)
            {
                continue;
            }

            foreach (var member in membersResult.Value)
            {
                var normalizedEmail = NormalizeEmail(member.Email);
                if (normalizedEmail is null || string.IsNullOrWhiteSpace(member.Name))
                {
                    continue;
                }

                var developer = await _developerRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
                if (developer is null)
                {
                    developer = new Developer
                    {
                        Name = member.Name.Trim(),
                        Email = normalizedEmail
                    };
                    await _developerRepository.AddAsync(developer, cancellationToken);
                    created++;
                }
                else if (!string.Equals(developer.Name, member.Name.Trim(), StringComparison.Ordinal))
                {
                    developer.Name = member.Name.Trim();
                    await _developerRepository.UpdateAsync(developer, cancellationToken);
                    updated++;
                }

                if (!await _developerRepository.HubstaffMappingExistsAsync(developer.Id, organization.Id, cancellationToken))
                {
                    await _developerRepository.AddHubstaffMappingAsync(new DeveloperHubstaffMapping
                    {
                        DeveloperId = developer.Id,
                        HubstaffOrganizationId = organization.Id,
                        HubstaffUserId = member.HubstaffUserId
                    }, cancellationToken);
                    mappingsAdded++;
                }
            }

            var developersWithMappings = await _developerRepository.GetAllWithHubstaffMappingsAsync(cancellationToken);
            var userIdToDeveloper = developersWithMappings
                .SelectMany(d => d.HubstaffMappings.Where(m => m.HubstaffOrganizationId == organization.Id))
                .ToDictionary(m => m.HubstaffUserId, m => m.DeveloperId);

            await BackfillHubstaffActivitiesAsync(organization.Id, userIdToDeveloper, cancellationToken);
        }

        return Result<SyncFromHubstaffResult>.Success(new SyncFromHubstaffResult(created, updated, mappingsAdded));
    }

    private async Task BackfillHubstaffActivitiesAsync(
        Guid hubstaffOrganizationId,
        IReadOnlyDictionary<int, Guid> userIdToDeveloper,
        CancellationToken cancellationToken)
    {
        var backfilled = await _hubstaffDailyActivityRepository.BackfillDeveloperIdsAsync(
            hubstaffOrganizationId,
            userIdToDeveloper,
            cancellationToken);

        if (backfilled > 0)
        {
            _logger.LogInformation(
                "Backfilled developer mapping on {Count} Hubstaff daily activity row(s) for organization {OrganizationId}",
                backfilled,
                hubstaffOrganizationId);
        }
    }

    private static Result ValidateDeveloperRequest(string name, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Developer name is required.");
        }

        if (email is not null && string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure("Email cannot be empty when provided.");
        }

        return Result.Success();
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private async Task<Result> ValidateReportingManagerAsync(
        Guid? developerId,
        Guid? reportingManagerDeveloperId,
        CancellationToken cancellationToken)
    {
        if (reportingManagerDeveloperId is null)
        {
            return Result.Success();
        }

        if (developerId.HasValue && reportingManagerDeveloperId.Value == developerId.Value)
        {
            return Result.Failure("A developer cannot be their own reporting manager.");
        }

        var manager = await _developerRepository.GetByIdAsync(reportingManagerDeveloperId.Value, cancellationToken);
        if (manager is null || !manager.IsActive)
        {
            return Result.Failure("Reporting manager was not found or is inactive.");
        }

        if (!developerId.HasValue)
        {
            return Result.Success();
        }

        var currentManagerId = manager.ReportingManagerDeveloperId;
        while (currentManagerId.HasValue)
        {
            if (currentManagerId.Value == developerId.Value)
            {
                return Result.Failure("This reporting manager assignment would create a circular hierarchy.");
            }

            var next = await _developerRepository.GetByIdAsync(currentManagerId.Value, cancellationToken);
            if (next is null)
            {
                break;
            }

            currentManagerId = next.ReportingManagerDeveloperId;
        }

        return Result.Success();
    }

    private static DeveloperDto MapToDto(Developer developer) =>
        new(
            developer.Id,
            developer.Name,
            developer.Email,
            developer.IsActive,
            developer.CreatedAtUtc,
            developer.ClickUpMappings
                .Where(m => m.ClickUpAccount?.IsActive == true)
                .Select(m => new DeveloperClickUpMappingDto(
                    m.Id,
                    m.ClickUpAccountId,
                    m.ClickUpAccount?.Name ?? "Unknown",
                    m.ClickUpUserId))
                .OrderBy(m => m.AccountName)
                .ToList(),
            (DevPulse.Shared.Contracts.Developers.WorkRole)(int)developer.WorkRole,
            developer.ReportingManagerDeveloperId,
            developer.ReportingManager?.Name,
            developer.HubstaffMappings
                .Where(m => m.HubstaffOrganization?.IsActive == true)
                .Select(m => new DeveloperHubstaffMappingDto(
                    m.Id,
                    m.HubstaffOrganizationId,
                    m.HubstaffOrganization?.Name ?? "Unknown",
                    m.HubstaffUserId))
                .OrderBy(m => m.OrganizationName)
                .ToList());
}
