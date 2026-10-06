namespace DevPulse.Client.Services.Auth;

public interface IAuthService
{
    Task<UserDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(SelfChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly CookieAuthenticationStateProvider _authStateProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthService(
        HttpClient httpClient,
        AuthenticationStateProvider authStateProvider,
        JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _authStateProvider = (CookieAuthenticationStateProvider)authStateProvider;
        _jsonOptions = jsonOptions;
    }

    public async Task<UserDto?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, _jsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseError(error));
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions, cancellationToken);
        await _authStateProvider.RefreshAuthenticationStateAsync();
        return loginResponse?.User;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _httpClient.PostAsync("api/auth/logout", null, cancellationToken);
        _authStateProvider.NotifyAuthenticationStateChanged();
    }

    public async Task ChangePasswordAsync(SelfChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/auth/change-password", request, _jsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ParseError(error, "Failed to change password."));
        }
    }

    private static string ParseError(string raw, string fallback = "Login failed.")
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? fallback;
            }

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
        }
        catch (JsonException)
        {
            // Fall through.
        }

        return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
    }
}
