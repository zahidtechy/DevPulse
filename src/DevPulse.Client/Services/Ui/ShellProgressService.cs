namespace DevPulse.Client.Services.Ui;

/// <summary>
/// Tracks user-visible UI work and exposes the indeterminate shell progress bar.
/// Visibility is reference-counted through <see cref="Begin"/> scopes, so overlapping
/// operations cannot hide the bar early. Once shown, the bar stays visible for a short
/// minimum duration to avoid a distracting flash on very fast responses.
/// </summary>
public sealed class ShellProgressService : IDisposable
{
    private const int MinimumVisibleMs = 450;

    private readonly HashSet<Guid> _operations = new();
    private CancellationTokenSource? _hideDelay;
    private long _visibleSinceTick;

    public event Action? Changed;

    public bool IsVisible { get; private set; }

    /// <summary>
    /// Marks the start of a user-visible operation. Dispose the returned scope when it completes.
    /// </summary>
    public IDisposable Begin()
    {
        var id = Guid.NewGuid();
        _operations.Add(id);

        CancelHide();

        if (!IsVisible)
        {
            IsVisible = true;
            _visibleSinceTick = Environment.TickCount64;
            NotifyChanged();
        }

        return new Scope(this, id);
    }

    private void End(Guid id)
    {
        if (!_operations.Remove(id) || _operations.Count > 0)
        {
            return;
        }

        if (IsVisible)
        {
            ScheduleHide();
        }
    }

    private void ScheduleHide()
    {
        CancelHide();

        var remaining = Math.Max(
            0,
            MinimumVisibleMs - (int)(Environment.TickCount64 - _visibleSinceTick));

        var cancellation = new CancellationTokenSource();
        _hideDelay = cancellation;
        _ = HideAfterDelayAsync(remaining, cancellation);
    }

    private async Task HideAfterDelayAsync(int delayMs, CancellationTokenSource cancellation)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_hideDelay, cancellation))
            {
                _hideDelay = null;
            }

            cancellation.Dispose();
        }

        if (_operations.Count > 0)
        {
            return;
        }

        IsVisible = false;
        NotifyChanged();
    }

    private void CancelHide()
    {
        var cancellation = _hideDelay;
        _hideDelay = null;
        cancellation?.Cancel();
    }

    private void NotifyChanged() => Changed?.Invoke();

    public void Dispose()
    {
        CancelHide();
        _operations.Clear();
        Changed = null;
    }

    private sealed class Scope : IDisposable
    {
        private ShellProgressService? _owner;
        private readonly Guid _id;

        public Scope(ShellProgressService owner, Guid id)
        {
            _owner = owner;
            _id = id;
        }

        public void Dispose()
        {
            _owner?.End(_id);
            _owner = null;
        }
    }
}
