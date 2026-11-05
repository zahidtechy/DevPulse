using System.Net;
using Microsoft.JSInterop;

namespace DevPulse.Client.Services.Auth;

public sealed class TokenRefreshCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(1);
    private readonly AccessTokenStore _tokenStore;
    private readonly AuthApiClient _authApiClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IJSRuntime _js;
    private readonly SessionState _session;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _localLock = new(1, 1);
    private IJSObjectReference? _module;
    private DotNetObjectReference<TokenRefreshCoordinator>? _dotNetReference;

    public TokenRefreshCoordinator(
        AccessTokenStore tokenStore,
        AuthApiClient authApiClient,
        JsonSerializerOptions jsonOptions,
        IJSRuntime js,
        SessionState session)
    {
        _tokenStore = tokenStore;
        _authApiClient = authApiClient;
        _jsonOptions = jsonOptions;
        _js = js;
        _session = session;
    }

    public async Task<bool> TryRefreshAsync(
        string? rejectedAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        return await _module!.InvokeAsync<bool>(
            "runWithRefreshLock",
            cancellationToken,
            _dotNetReference,
            rejectedAccessToken);
    }

    [JSInvokable]
    public async Task<bool> RefreshUnderLockAsync(string? rejectedAccessToken)
    {
        await _localLock.WaitAsync();
        try
        {
            var current = await _tokenStore.GetAsync();
            var anotherTabAlreadyRefreshed = !string.IsNullOrWhiteSpace(rejectedAccessToken)
                && AccessTokenStore.IsValid(current)
                && !string.Equals(current!.AccessToken, rejectedAccessToken, StringComparison.Ordinal);
            if (anotherTabAlreadyRefreshed
                || (string.IsNullOrWhiteSpace(rejectedAccessToken)
                    && AccessTokenStore.IsValid(current, RefreshBuffer)))
            {
                return true;
            }

            try
            {
                using var response = await _authApiClient.RefreshAsync();
                if (!response.IsSuccessStatusCode)
                {
                    // Only a 401 proves the refresh session is gone. Server errors, throttling,
                    // and gateway failures are transient and must not sign the user out.
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _session.NotifyExpired();
                        await _tokenStore.ClearAsync();
                    }

                    return false;
                }

                var refreshed = await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
                if (refreshed is null || !AccessTokenStore.IsValid(refreshed))
                {
                    // A malformed success response is a server-side problem, not proof that
                    // the session ended. Keep the current tokens and let the caller retry.
                    return false;
                }

                await _tokenStore.SetAsync(refreshed);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                // Network and serialization failures are transient. The existing token
                // envelope is left in place so a later request can refresh again.
                return false;
            }
        }
        finally
        {
            _localLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetReference?.Dispose();
        _initializationLock.Dispose();
        _localLock.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser runtime has already been torn down.
            }
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_module is not null)
            {
                return;
            }

            _module = await _js.InvokeAsync<IJSObjectReference>(
                "import",
                cancellationToken,
                "./js/authSession.js");
            _dotNetReference = DotNetObjectReference.Create(this);
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
