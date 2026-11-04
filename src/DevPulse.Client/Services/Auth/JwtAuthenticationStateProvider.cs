using System.Security.Claims;

namespace DevPulse.Client.Services.Auth;

public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AccessTokenStore _tokenStore;
    private readonly TokenRefreshCoordinator _refreshCoordinator;
    private readonly SessionState _session;

    public JwtAuthenticationStateProvider(
        AccessTokenStore tokenStore,
        TokenRefreshCoordinator refreshCoordinator,
        SessionState session)
    {
        _tokenStore = tokenStore;
        _refreshCoordinator = refreshCoordinator;
        _session = session;
        _tokenStore.Changed += HandleSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var login = await _tokenStore.GetAsync();
        if (!AccessTokenStore.IsValid(login))
        {
            var refreshed = await _refreshCoordinator.TryRefreshAsync();
            login = refreshed ? await _tokenStore.GetAsync() : null;
        }

        return CreateState(login);
    }

    public void Dispose() => _tokenStore.Changed -= HandleSessionChanged;

    private void HandleSessionChanged(LoginResponse? login)
    {
        var state = CreateState(login);
        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    private AuthenticationState CreateState(LoginResponse? login)
    {
        if (!AccessTokenStore.IsValid(login))
        {
            _session.MarkAnonymous();
            return Anonymous();
        }

        var user = login!.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        _session.MarkAuthenticated();
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
    }

    private static AuthenticationState Anonymous() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));
}
