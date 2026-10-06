namespace DevPulse.Application.Services.ClickUp;

/// <summary>
/// Application service orchestrating ClickUp account lifecycle and API operations.
/// </summary>
public sealed class ClickUpAccountService : IClickUpAccountService
{
    private readonly IClickUpAccountRepository _repository;
    private readonly IClickUpApiClient _apiClient;
    private readonly ITokenProtector _tokenProtector;
    private readonly ILogger<ClickUpAccountService> _logger;

    public ClickUpAccountService(
        IClickUpAccountRepository repository,
        IClickUpApiClient apiClient,
        ITokenProtector tokenProtector,
        ILogger<ClickUpAccountService> logger)
    {
        _repository = repository;
        _apiClient = apiClient;
        _tokenProtector = tokenProtector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClickUpAccountDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllAsync(cancellationToken);
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<Result<ClickUpAccountDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(id, cancellationToken);
        return account is null
            ? Result<ClickUpAccountDto>.Failure("ClickUp account was not found.")
            : Result<ClickUpAccountDto>.Success(MapToDto(account));
    }

    public async Task<Result<ClickUpAccountDto>> CreateAsync(CreateClickUpAccountRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request.Name, request.WorkspaceId, request.AccessToken);
        if (validation.IsFailure)
        {
            return Result<ClickUpAccountDto>.Failure(validation.Error!);
        }

        if (await _repository.ExistsByWorkspaceIdAsync(request.WorkspaceId, cancellationToken: cancellationToken))
        {
            return Result<ClickUpAccountDto>.Failure("A ClickUp account for this workspace already exists.");
        }

        var testResult = await ValidateTokenAgainstWorkspaceAsync(request.AccessToken, request.WorkspaceId, cancellationToken);
        if (testResult.IsFailure)
        {
            return Result<ClickUpAccountDto>.Failure(testResult.Error!);
        }

        var account = new ClickUpAccount
        {
            Name = request.Name.Trim(),
            WorkspaceId = request.WorkspaceId.Trim(),
            EncryptedAccessToken = _tokenProtector.Protect(request.AccessToken.Trim()),
            IsActive = true,
            LastValidatedAtUtc = DateTime.UtcNow,
            LastValidationMessage = testResult.Value!.Message
        };

        await _repository.AddAsync(account, cancellationToken);
        _logger.LogInformation("Created ClickUp account {AccountName} for workspace {WorkspaceId}", account.Name, account.WorkspaceId);

        return Result<ClickUpAccountDto>.Success(MapToDto(account));
    }

    public async Task<Result<ClickUpAccountDto>> UpdateAsync(Guid id, UpdateClickUpAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return Result<ClickUpAccountDto>.Failure("ClickUp account was not found.");
        }

        var validation = ValidateRequest(request.Name, request.WorkspaceId, request.AccessToken ?? "placeholder", requireToken: request.AccessToken is not null);
        if (validation.IsFailure)
        {
            return Result<ClickUpAccountDto>.Failure(validation.Error!);
        }

        if (await _repository.ExistsByWorkspaceIdAsync(request.WorkspaceId, id, cancellationToken))
        {
            return Result<ClickUpAccountDto>.Failure("Another account is already linked to this workspace.");
        }

        var tokenToValidate = request.AccessToken?.Trim();
        if (!string.IsNullOrWhiteSpace(tokenToValidate))
        {
            var testResult = await ValidateTokenAgainstWorkspaceAsync(tokenToValidate, request.WorkspaceId, cancellationToken);
            if (testResult.IsFailure)
            {
                return Result<ClickUpAccountDto>.Failure(testResult.Error!);
            }

            account.EncryptedAccessToken = _tokenProtector.Protect(tokenToValidate);
            account.LastValidatedAtUtc = DateTime.UtcNow;
            account.LastValidationMessage = testResult.Value!.Message;
        }

        account.Name = request.Name.Trim();
        account.WorkspaceId = request.WorkspaceId.Trim();

        await _repository.UpdateAsync(account, cancellationToken);
        return Result<ClickUpAccountDto>.Success(MapToDto(account));
    }

    public async Task<Result<ClickUpAccountDto>> UpdateStatusAsync(
        Guid id,
        UpdateClickUpAccountStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return Result<ClickUpAccountDto>.Failure("ClickUp account was not found.");
        }

        account.IsActive = request.IsActive;
        await _repository.UpdateAsync(account, cancellationToken);

        _logger.LogInformation(
            "Updated ClickUp account {AccountName} registry status to {Status}",
            account.Name,
            account.IsActive ? "active" : "inactive");

        return Result<ClickUpAccountDto>.Success(MapToDto(account));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return Result.Failure("ClickUp account was not found.");
        }

        await _repository.DeleteAsync(id, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ClickUpConnectionTestDto>> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return Result<ClickUpConnectionTestDto>.Failure("ClickUp account was not found.");
        }

        if (DemoSeedData.IsDemoWorkspace(account.WorkspaceId))
        {
            account.LastValidatedAtUtc = DateTime.UtcNow;
            account.LastValidationMessage = "Demo workspace — seeded for developer work reporting.";
            await _repository.UpdateAsync(account, cancellationToken);

            return Result<ClickUpConnectionTestDto>.Success(new ClickUpConnectionTestDto(
                account.Id,
                account.Name,
                true,
                ConnectionStatus.Connected.ToString(),
                account.Name,
                "Demo workspace is ready for developer work reports."));
        }

        var token = _tokenProtector.Unprotect(account.EncryptedAccessToken);
        var testResult = await ValidateTokenAgainstWorkspaceAsync(token, account.WorkspaceId, cancellationToken);

        account.LastValidatedAtUtc = DateTime.UtcNow;
        account.LastValidationMessage = testResult.IsSuccess ? testResult.Value!.Message : testResult.Error;
        await _repository.UpdateAsync(account, cancellationToken);

        if (testResult.IsFailure)
        {
            return Result<ClickUpConnectionTestDto>.Failure(testResult.Error!);
        }

        var dto = testResult.Value! with
        {
            AccountId = account.Id,
            AccountName = account.Name
        };

        return Result<ClickUpConnectionTestDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ClickUpMemberDto>>> GetMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accountResult = await GetAccountWithTokenAsync(id, cancellationToken);
        if (accountResult.IsFailure)
        {
            return Result<IReadOnlyList<ClickUpMemberDto>>.Failure(accountResult.Error!);
        }

        var (account, token) = accountResult.Value!;

        try
        {
            var members = await _apiClient.GetWorkspaceMembersAsync(token, account.WorkspaceId, cancellationToken);
            return Result<IReadOnlyList<ClickUpMemberDto>>.Success(members);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to load ClickUp members for workspace {WorkspaceId}", account.WorkspaceId);
            return Result<IReadOnlyList<ClickUpMemberDto>>.Failure(ex.Message);
        }
    }

    public async Task<Result<ClickUpUserLookupDto>> GetMemberByEmailAsync(
        string workspaceId,
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Result<ClickUpUserLookupDto>.Failure("Workspace ID is required.");
        }

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return Result<ClickUpUserLookupDto>.Failure("Email is required.");
        }

        var account = await _repository.GetByWorkspaceIdAsync(workspaceId.Trim(), cancellationToken);
        if (account is null)
        {
            return Result<ClickUpUserLookupDto>.Failure("No ClickUp account is connected for this workspace.");
        }

        if (DemoSeedData.IsDemoWorkspace(account.WorkspaceId))
        {
            return Result<ClickUpUserLookupDto>.Failure("Member lookup is not available for the demo workspace.");
        }

        try
        {
            var token = _tokenProtector.Unprotect(account.EncryptedAccessToken);
            var member = await _apiClient.FindWorkspaceMemberByEmailAsync(
                token,
                account.WorkspaceId,
                normalizedEmail,
                cancellationToken);

            if (member is null)
            {
                return Result<ClickUpUserLookupDto>.Failure(
                    $"No workspace member found with email '{normalizedEmail}' in workspace '{account.WorkspaceId}'.");
            }

            return Result<ClickUpUserLookupDto>.Success(new ClickUpUserLookupDto(
                member.ClickUpUserId,
                member.Username,
                member.Email,
                member.ProfilePicture,
                account.WorkspaceId,
                account.Id,
                account.Name));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to look up ClickUp member by email for workspace {WorkspaceId}", account.WorkspaceId);
            return Result<ClickUpUserLookupDto>.Failure(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<ClickUpWorkspaceDto>>> GetAuthorizedWorkspacesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accountResult = await GetAccountWithTokenAsync(id, cancellationToken);
        if (accountResult.IsFailure)
        {
            return Result<IReadOnlyList<ClickUpWorkspaceDto>>.Failure(accountResult.Error!);
        }

        var (_, token) = accountResult.Value!;
        var workspaces = await _apiClient.GetAuthorizedWorkspacesAsync(token, cancellationToken);
        return Result<IReadOnlyList<ClickUpWorkspaceDto>>.Success(workspaces);
    }

    public async Task<Result<ClickUpTaskQueryResponse>> GetTasksAsync(Guid id, ClickUpTaskQueryRequest query, CancellationToken cancellationToken = default)
    {
        var accountResult = await GetAccountWithTokenAsync(id, cancellationToken);
        if (accountResult.IsFailure)
        {
            return Result<ClickUpTaskQueryResponse>.Failure(accountResult.Error!);
        }

        var (account, token) = accountResult.Value!;
        var tasks = await _apiClient.GetFilteredTasksAsync(
            token,
            account.WorkspaceId,
            account.Name,
            account.Id,
            query,
            cancellationToken);

        return Result<ClickUpTaskQueryResponse>.Success(tasks);
    }

    private async Task<Result<(ClickUpAccount Account, string Token)>> GetAccountWithTokenAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return Result<(ClickUpAccount, string)>.Failure("ClickUp account was not found.");
        }

        return Result<(ClickUpAccount, string)>.Success((account, _tokenProtector.Unprotect(account.EncryptedAccessToken)));
    }

    private async Task<Result<ClickUpConnectionTestDto>> ValidateTokenAgainstWorkspaceAsync(
        string accessToken,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var workspaces = await _apiClient.GetAuthorizedWorkspacesAsync(accessToken, cancellationToken);
            var matched = workspaces.FirstOrDefault(w => w.Id == workspaceId);

            if (matched is null)
            {
                return Result<ClickUpConnectionTestDto>.Success(new ClickUpConnectionTestDto(
                    Guid.Empty,
                    string.Empty,
                    false,
                    ConnectionStatus.WorkspaceNotFound.ToString(),
                    null,
                    "Token is valid, but the configured workspace was not found for this token."));
            }

            return Result<ClickUpConnectionTestDto>.Success(new ClickUpConnectionTestDto(
                Guid.Empty,
                string.Empty,
                true,
                ConnectionStatus.Connected.ToString(),
                matched.Name,
                $"Connected successfully to workspace '{matched.Name}'."));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Result<ClickUpConnectionTestDto>.Failure("ClickUp rejected the access token (401 Unauthorized).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate ClickUp token for workspace {WorkspaceId}", workspaceId);
            return Result<ClickUpConnectionTestDto>.Failure($"Connection test failed: {ex.Message}");
        }
    }

    private static Result ValidateRequest(string name, string workspaceId, string accessToken, bool requireToken = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Account name is required.");
        }

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Result.Failure("Workspace ID is required.");
        }

        if (requireToken && string.IsNullOrWhiteSpace(accessToken))
        {
            return Result.Failure("Access token is required.");
        }

        return Result.Success();
    }

    private static ClickUpAccountDto MapToDto(ClickUpAccount account) =>
        new(
            account.Id,
            account.Name,
            account.WorkspaceId,
            account.IsActive,
            account.CreatedAtUtc,
            account.LastValidatedAtUtc,
            account.LastValidationMessage);

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
