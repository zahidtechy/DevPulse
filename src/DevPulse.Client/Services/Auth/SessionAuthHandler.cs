using System.Net;

namespace DevPulse.Client.Services.Auth;

/// <summary>
/// Watches every API response for a 401 and replays the request once to confirm that the
/// response was not transient. If the replay is also unauthorized, the session is treated
/// as expired and the app signs out.
/// </summary>
public sealed class SessionAuthHandler : DelegatingHandler
{
    private readonly SessionState _session;

    public SessionAuthHandler(SessionState session)
    {
        _session = session;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (!ShouldHandle(request, response))
        {
            return response;
        }

        HttpRequestMessage? retry = null;

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
            // The request body cannot be replayed; treat the 401 as final.
        }

        if (retry is null)
        {
            _session.NotifyExpired();
            return response;
        }

        using (retry)
        {
            HttpResponseMessage retryResponse;

            try
            {
                retryResponse = await base.SendAsync(retry, cancellationToken);
            }
            catch (HttpRequestException)
            {
                // The replay did not produce a response. Preserve the known 401 rather than
                // replacing it with a network error, and do not infer session expiry.
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }

            response.Dispose();

            if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                _session.NotifyExpired();
            }

            return retryResponse;
        }
    }

    private bool ShouldHandle(HttpRequestMessage request, HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized
            || !_session.IsAuthenticated
            || _session.IsExpired)
        {
            return false;
        }

        // Authentication challenges occur before controller actions, while authorization
        // failures are 403. Therefore replaying a current DevPulse 401 cannot duplicate a
        // completed write. Revisit this if an API action ever returns a business-level 401.
        // A rejected sign-in is a credential problem, and sign-out must not recurse.
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        return !path.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
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
