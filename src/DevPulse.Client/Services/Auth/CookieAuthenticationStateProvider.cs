using System.Security.Claims;

namespace DevPulse.Client.Services.Auth;

public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private UserDto? _cachedUser;
    private bool _cacheValid;

    public CookieAuthenticationStateProvider(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
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
                _cachedUser = null;
                _cacheValid = true;
                return Anonymous();
            }

            var user = await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
            _cachedUser = user;
            _cacheValid = true;
            return user is null ? Anonymous() : CreateAuthenticatedState(user);
        }
        catch (HttpRequestException)
        {
            _cachedUser = null;
            _cacheValid = true;
            return Anonymous();
        }
        catch (JsonException)
        {
            _cachedUser = null;
            _cacheValid = true;
            return Anonymous();
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        _cacheValid = false;
        _cachedUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task RefreshAuthenticationStateAsync()
    {
        _cacheValid = false;
        _cachedUser = null;
        var state = await GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(state));
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
