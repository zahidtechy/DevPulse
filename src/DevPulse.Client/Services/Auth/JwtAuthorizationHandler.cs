using System.Net;
using System.Net.Http.Headers;

namespace DevPulse.Client.Services.Auth;

public sealed class JwtAuthorizationHandler : DelegatingHandler
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(1);
    private readonly AccessTokenStore _tokenStore;
    private readonly TokenRefreshCoordinator _refreshCoordinator;
    private readonly SessionState _session;
    private readonly Uri _applicationBaseUri;

    public JwtAuthorizationHandler(
        AccessTokenStore tokenStore,
        TokenRefreshCoordinator refreshCoordinator,
        SessionState session,
        Uri applicationBaseUri)
    {
        _tokenStore = tokenStore;
        _refreshCoordinator = refreshCoordinator;
        _session = session;
        _applicationBaseUri = applicationBaseUri;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!ShouldAuthorize(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var login = await _tokenStore.GetAsync();
        if (login is not null && !AccessTokenStore.IsValid(login, RefreshBuffer))
        {
            await _refreshCoordinator.TryRefreshAsync(cancellationToken: cancellationToken);
            login = await _tokenStore.GetAsync();
        }

        if (AccessTokenStore.IsValid(login))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || login is null || _session.IsExpired)
        {
            return response;
        }

        HttpRequestMessage retry;
        try
        {
            retry = await CloneAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            response.Dispose();
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or IOException
                                   or InvalidOperationException
                                   or NotSupportedException)
        {
            return response;
        }

        using (retry)
        {
            bool refreshed;
            try
            {
                refreshed = await _refreshCoordinator.TryRefreshAsync(login.AccessToken, cancellationToken);
            }
            catch
            {
                response.Dispose();
                throw;
            }

            if (!refreshed)
            {
                // A failed refresh is not necessarily an expired session. The coordinator
                // raises SessionState.Expired only for a definitive 401; transient failures
                // leave the existing token and refresh cookie in place for a later retry.
                return response;
            }

            var refreshedLogin = await _tokenStore.GetAsync();
            if (!AccessTokenStore.IsValid(refreshedLogin))
            {
                // The refresh endpoint reported success but returned an unusable token.
                // Treat it as a transient server problem rather than forcing a sign-out.
                return response;
            }

            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedLogin!.AccessToken);
            response.Dispose();
            var retryResponse = await base.SendAsync(retry, cancellationToken);

            if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                _session.NotifyExpired();
            }

            return retryResponse;
        }
    }

    private bool ShouldAuthorize(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri is null)
        {
            return false;
        }

        var absoluteUri = uri.IsAbsoluteUri ? uri : new Uri(_applicationBaseUri, uri);
        if (!string.Equals(absoluteUri.Scheme, _applicationBaseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(absoluteUri.Authority, _applicationBaseUri.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = absoluteUri.AbsolutePath;
        return !path.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        try
        {
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var option in request.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
            }

            if (request.Content is not null)
            {
                var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                clone.Content = new ByteArrayContent(body);
                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
        catch
        {
            clone.Dispose();
            throw;
        }
    }
}
