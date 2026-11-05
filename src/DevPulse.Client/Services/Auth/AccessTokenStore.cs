using Microsoft.JSInterop;

namespace DevPulse.Client.Services.Auth;

public sealed class AccessTokenStore : IAsyncDisposable
{
    private const string StorageKey = "devpulse.auth.access.v1";
    private readonly IJSRuntime _js;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private IJSObjectReference? _module;
    private string? _storageListenerId;
    private DotNetObjectReference<AccessTokenStore>? _dotNetReference;
    private LoginResponse? _current;
    private bool _initialized;

    public AccessTokenStore(IJSRuntime js, JsonSerializerOptions jsonOptions)
    {
        _js = js;
        _jsonOptions = jsonOptions;
    }

    public event Action<LoginResponse?>? Changed;

    public async ValueTask<LoginResponse?> GetAsync()
    {
        await EnsureInitializedAsync();
        return _current;
    }

    public async Task SetAsync(LoginResponse response)
    {
        await EnsureInitializedAsync();
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await _module!.InvokeVoidAsync("setSession", StorageKey, json);
        SetCurrent(response);
    }

    public async Task ClearAsync()
    {
        await EnsureInitializedAsync();
        await _module!.InvokeVoidAsync("removeSession", StorageKey);
        SetCurrent(null);
    }

    public static bool IsValid(LoginResponse? response, TimeSpan? minimumRemaining = null) =>
        response is not null
        && !string.IsNullOrWhiteSpace(response.AccessToken)
        && response.ExpiresAtUtc > DateTime.UtcNow.Add(minimumRemaining ?? TimeSpan.Zero);

    [JSInvokable]
    public void OnStorageChanged(string? json)
    {
        LoginResponse? response = null;

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                response = JsonSerializer.Deserialize<LoginResponse>(json, _jsonOptions);
            }
            catch (JsonException)
            {
                // Treat malformed cross-tab state as signed out.
            }
        }

        SetCurrent(response);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null && _storageListenerId is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("unregisterStorageListener", _storageListenerId);
            }
            catch (JSDisconnectedException)
            {
                // The browser runtime has already been torn down.
            }
        }

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

        _dotNetReference?.Dispose();
        _initializationLock.Dispose();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/authSession.js");
            _dotNetReference = DotNetObjectReference.Create(this);
            _storageListenerId = await _module.InvokeAsync<string>(
                "registerStorageListener",
                StorageKey,
                _dotNetReference);

            var json = await _module.InvokeAsync<string?>("getSession", StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    _current = JsonSerializer.Deserialize<LoginResponse>(json, _jsonOptions);
                }
                catch (JsonException)
                {
                    await _module.InvokeVoidAsync("removeSession", StorageKey);
                }
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private void SetCurrent(LoginResponse? response)
    {
        _current = response;
        Changed?.Invoke(response);
    }
}
