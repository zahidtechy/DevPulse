namespace DevPulse.Client.Services.Auth;

/// <summary>
/// Tracks whether the browser currently holds an authenticated JWT session and raises a
/// single event when access can no longer be renewed.
/// </summary>
public sealed class SessionState
{
    private int _expired;

    /// <summary>True while the browser holds a valid access-token envelope.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>True after a 401 survived the retry, until the next successful sign-in.</summary>
    public bool IsExpired => Volatile.Read(ref _expired) == 1;

    /// <summary>Raised once per expired session, before the app signs out and redirects.</summary>
    public event Action? Expired;

    public void MarkAuthenticated()
    {
        IsAuthenticated = true;
        Interlocked.Exchange(ref _expired, 0);
    }

    public void MarkAnonymous() => IsAuthenticated = false;

    public void NotifyExpired()
    {
        if (!IsAuthenticated || Interlocked.CompareExchange(ref _expired, 1, 0) != 0)
        {
            return;
        }

        Expired?.Invoke();
    }
}
