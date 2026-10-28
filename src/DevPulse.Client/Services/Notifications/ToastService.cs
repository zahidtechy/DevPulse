namespace DevPulse.Client.Services.Notifications;

public enum ToastLevel
{
    Success,
    Error,
    Warning,
    Info
}

public sealed record ToastMessage(
    Guid Id,
    ToastLevel Level,
    string Title,
    string Message,
    int DurationMs,
    string? Key = null);

/// <summary>
/// Client-side notification bus used to surface transient success, error, warning,
/// and info messages in the bottom-right toast stack.
/// </summary>
public sealed class ToastService
{
    private const int MaxVisible = 5;

    private readonly List<ToastMessage> _toasts = new();

    public event Action? OnChange;

    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    /// <summary>
    /// While true, error toasts are dropped. Used between detecting an expired session and
    /// the user signing in again, so failing requests cannot cover the login page in stale
    /// "session expired" / "Failed to fetch" messages.
    /// </summary>
    public bool SuppressErrors { get; set; }

    public void Success(string message, string? title = null, int? durationMs = null, string? key = null)
        => Show(message, ToastLevel.Success, title, durationMs, key);

    public void Error(string message, string? title = null, int? durationMs = null, string? key = null)
        => Show(message, ToastLevel.Error, title, durationMs, key);

    public void Warning(string message, string? title = null, int? durationMs = null, string? key = null)
        => Show(message, ToastLevel.Warning, title, durationMs, key);

    public void Info(string message, string? title = null, int? durationMs = null, string? key = null)
        => Show(message, ToastLevel.Info, title, durationMs, key);

    /// <summary>
    /// Shows a toast. When <paramref name="key"/> is supplied, the toast is correlated with
    /// that key: a later toast using the same key replaces it, and <see cref="Dismiss(string)"/>
    /// can remove it. Use a key for problems that can be resolved later (for example a failed
    /// operation that is retried) so the stale sticky error disappears once the retry succeeds.
    /// </summary>
    public void Show(string message, ToastLevel level, string? title = null, int? durationMs = null, string? key = null)
    {
        if (string.IsNullOrWhiteSpace(message)
            || (SuppressErrors && level == ToastLevel.Error))
        {
            return;
        }

        var normalized = message.Trim();
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();

        if (normalizedKey is not null)
        {
            var existing = _toasts.FirstOrDefault(t => string.Equals(t.Key, normalizedKey, StringComparison.Ordinal));
            if (existing is not null)
            {
                // An identical repeat keeps the toast (and its remaining timer) as-is.
                if (existing.Level == level && string.Equals(existing.Message, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                // A correlated update supersedes the earlier toast, so resolving the problem
                // replaces the stale error instead of leaving it on screen.
                _toasts.Remove(existing);
            }
        }
        else if (_toasts.Any(t => t.Level == level && string.Equals(t.Message, normalized, StringComparison.Ordinal)))
        {
            // Collapse duplicates so double-clicks or repeated failures do not stack up.
            return;
        }

        var toast = new ToastMessage(
            Guid.NewGuid(),
            level,
            string.IsNullOrWhiteSpace(title) ? GetDefaultTitle(level) : title!,
            normalized,
            durationMs ?? GetDefaultDuration(level),
            normalizedKey);

        _toasts.Add(toast);

        while (_toasts.Count > MaxVisible)
        {
            _toasts.RemoveAt(0);
        }

        OnChange?.Invoke();
    }

    /// <summary>
    /// Removes the toast associated with <paramref name="key"/>, if it is still visible.
    /// Intended for "the problem is gone now" signals so a sticky error can clear itself.
    /// </summary>
    public void Dismiss(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedKey = key.Trim();
        var removed = _toasts.RemoveAll(t => string.Equals(t.Key, normalizedKey, StringComparison.Ordinal));
        if (removed > 0)
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Whether a toast with <paramref name="key"/> is currently visible.</summary>
    public bool IsVisible(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalizedKey = key.Trim();
        return _toasts.Any(t => string.Equals(t.Key, normalizedKey, StringComparison.Ordinal));
    }

    public void Remove(Guid id)
    {
        var index = _toasts.FindIndex(t => t.Id == id);
        if (index < 0)
        {
            return;
        }

        _toasts.RemoveAt(index);
        OnChange?.Invoke();
    }

    public void Clear()
    {
        if (_toasts.Count == 0)
        {
            return;
        }

        _toasts.Clear();
        OnChange?.Invoke();
    }

    private static int GetDefaultDuration(ToastLevel level) => level switch
    {
        // Errors stay until the user acknowledges them.
        ToastLevel.Error => 0,
        ToastLevel.Warning => 6000,
        _ => 4000
    };

    private static string GetDefaultTitle(ToastLevel level) => level switch
    {
        ToastLevel.Success => "Success",
        ToastLevel.Error => "Something went wrong",
        ToastLevel.Warning => "Warning",
        _ => "Notice"
    };
}
