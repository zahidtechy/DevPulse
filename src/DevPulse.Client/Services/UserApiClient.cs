namespace DevPulse.Client.Services;

public interface IUserApiClient
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserDto?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserApiClient(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/users", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to load users.");
        return await response.Content.ReadFromJsonAsync<List<UserDto>>(_jsonOptions, cancellationToken) ?? [];
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to create user.");
        return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions, cancellationToken);
    }

    public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{id}", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to update user.");
        return await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{id}/password", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to change password.");
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Failed to delete user.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken, string fallback)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ParseApiError(error, fallback);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("You do not have permission to perform this action. Try signing out and back in.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Your session has expired. Please sign in again.");
        }

        throw new InvalidOperationException(message);
    }

    private static string ParseApiError(string rawError, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(rawError);
            if (document.RootElement.TryGetProperty("errors", out var errorsProperty)
                && errorsProperty.ValueKind == JsonValueKind.Array
                && errorsProperty.GetArrayLength() > 0)
            {
                var messages = errorsProperty.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (messages.Count > 0)
                {
                    return string.Join(" ", messages);
                }
            }

            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? fallback;
            }

            if (document.RootElement.TryGetProperty("title", out var titleProperty))
            {
                return titleProperty.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
            // Use raw body below.
        }

        return string.IsNullOrWhiteSpace(rawError) ? fallback : rawError;
    }
}
