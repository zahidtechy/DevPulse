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
    int DurationMs);

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

    public void Success(string message, string? title = null, int? durationMs = null)
        => Show(message, ToastLevel.Success, title, durationMs);

    public void Error(string message, string? title = null, int? durationMs = null)
        => Show(message, ToastLevel.Error, title, durationMs);

    public void Warning(string message, string? title = null, int? durationMs = null)
        => Show(message, ToastLevel.Warning, title, durationMs);

    public void Info(string message, string? title = null, int? durationMs = null)
        => Show(message, ToastLevel.Info, title, durationMs);

    public void Show(string message, ToastLevel level, string? title = null, int? durationMs = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalized = message.Trim();

        // Collapse duplicates so double-clicks or repeated failures do not stack up.
        if (_toasts.Any(t => t.Level == level && string.Equals(t.Message, normalized, StringComparison.Ordinal)))
        {
            return;
        }

        var toast = new ToastMessage(
            Guid.NewGuid(),
            level,
            string.IsNullOrWhiteSpace(title) ? GetDefaultTitle(level) : title!,
            normalized,
            durationMs ?? GetDefaultDuration(level));

        _toasts.Add(toast);

        while (_toasts.Count > MaxVisible)
        {
            _toasts.RemoveAt(0);
        }

        OnChange?.Invoke();
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
