using System.Security.Claims;

namespace DevPulse.Client.Services.Auth;

public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SessionState _session;
    private UserDto? _cachedUser;
    private bool _cacheValid;

    public CookieAuthenticationStateProvider(
        HttpClient httpClient,
        JsonSerializerOptions jsonOptions,
        SessionState session)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
        _session = session;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cacheValid)
        {
            return _cachedUser is null ? Anonymous() : CreateAuthenticatedState(_cachedUser);
        }

        try
        {
            var response = await _httpClient.GetAsync("api/auth/me");
            if (!response.IsSuccessStatusCode)
            {
                return MarkAnonymous();
            }

            var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
            if (user is null)
            {
                return MarkAnonymous();
            }

            _cachedUser = user;
            _cacheValid = true;
            _session.MarkAuthenticated();
            return CreateAuthenticatedState(user);
        }
        catch (HttpRequestException)
        {
            return MarkAnonymous();
        }
        catch (JsonException)
        {
            return MarkAnonymous();
        }
    }

    /// <summary>
    /// Clears the cached principal after a sign-out. Unlike notifying the provider with a
    /// fresh <c>api/auth/me</c> call, this does not send another request with the cookie that
    /// has just been deleted.
    /// </summary>
    public void NotifySignedOut()
    {
        var alreadyAnonymous = _cacheValid && _cachedUser is null;

        _cachedUser = null;
        _cacheValid = true;
        _session.MarkAnonymous();

        if (!alreadyAnonymous)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
        }
    }

    public async Task RefreshAuthenticationStateAsync()
    {
        _cacheValid = false;
        _cachedUser = null;
        var state = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    private AuthenticationState MarkAnonymous()
    {
        _cachedUser = null;
        _cacheValid = true;
        _session.MarkAnonymous();
        return Anonymous();
    }

    private static AuthenticationState CreateAuthenticatedState(UserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static AuthenticationState Anonymous() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));
}
