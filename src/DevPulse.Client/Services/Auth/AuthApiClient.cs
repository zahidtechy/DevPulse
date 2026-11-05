using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace DevPulse.Client.Services.Auth;

public sealed class AuthApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthApiClient(Uri baseAddress, JsonSerializerOptions jsonOptions)
    {
        _httpClient = new HttpClient { BaseAddress = baseAddress };
        _jsonOptions = jsonOptions;
    }

    public Task<HttpResponseMessage> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        SendJsonAsync(HttpMethod.Post, "api/auth/login", request, cancellationToken);

    public Task<HttpResponseMessage> RefreshAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/auth/refresh", cancellationToken);

    public Task<HttpResponseMessage> LogoutAsync(CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "api/auth/logout", cancellationToken);

    public void Dispose() => _httpClient.Dispose();

    private async Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpMethod method,
        string uri,
        T body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(body, options: _jsonOptions)
        };
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string uri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return await _httpClient.SendAsync(request, cancellationToken);
    }
}
