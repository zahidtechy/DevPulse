namespace DevPulse.Client.Services;

public interface IClickUpAccountApiClient
{
    Task<IReadOnlyList<ClickUpAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<ClickUpAccountDto?> CreateAccountAsync(CreateClickUpAccountRequest request, CancellationToken cancellationToken = default);

    Task<ClickUpAccountDto?> UpdateAccountStatusAsync(Guid accountId, bool isActive, CancellationToken cancellationToken = default);

    Task<ClickUpConnectionTestDto?> TestConnectionAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClickUpMemberDto>> GetMembersAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<ClickUpUserLookupDto?> GetUserByEmailAsync(
        string workspaceId,
        string email,
        CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
}

public sealed class ClickUpAccountApiClient : IClickUpAccountApiClient
{
    private readonly HttpClient _httpClient;

    public ClickUpAccountApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ClickUpAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _httpClient.GetFromJsonAsync<List<ClickUpAccountDto>>("api/clickup/accounts", cancellationToken);
        return accounts ?? [];
    }

    public async Task<ClickUpAccountDto?> CreateAccountAsync(CreateClickUpAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/clickup/accounts", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to create account: {error}");
        }

        return await response.Content.ReadFromJsonAsync<ClickUpAccountDto>(cancellationToken);
    }

    public async Task<ClickUpAccountDto?> UpdateAccountStatusAsync(
        Guid accountId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync(
            $"api/clickup/accounts/{accountId}/status",
            new UpdateClickUpAccountStatusRequest(isActive),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to update account status"));
        }

        return await response.Content.ReadFromJsonAsync<ClickUpAccountDto>(cancellationToken);
    }

    public async Task<ClickUpConnectionTestDto?> TestConnectionAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<ClickUpConnectionTestDto>(
            $"api/clickup/accounts/{accountId}/test",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ClickUpMemberDto>> GetMembersAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/clickup/accounts/{accountId}/members", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to load members"));
        }

        var members = await response.Content.ReadFromJsonAsync<List<ClickUpMemberDto>>(cancellationToken);
        return members ?? [];
    }

    public async Task<ClickUpUserLookupDto?> GetUserByEmailAsync(
        string workspaceId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var response = await _httpClient.GetAsync(
            $"api/clickup/workspaces/{Uri.EscapeDataString(workspaceId)}/users/by-email?email={encodedEmail}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseApiError(error, "Failed to look up ClickUp user"));
        }

        return await response.Content.ReadFromJsonAsync<ClickUpUserLookupDto>(cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/clickup/accounts/{accountId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string ParseApiError(string rawError, string fallback)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(rawError);
            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? fallback;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Use raw body below.
        }

        return string.IsNullOrWhiteSpace(rawError) ? fallback : rawError;
    }
}
