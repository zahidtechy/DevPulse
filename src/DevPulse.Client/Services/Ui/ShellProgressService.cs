namespace DevPulse.Client.Services.Ui;

/// <summary>
/// Tracks user-visible UI work and exposes the indeterminate shell progress bar.
/// Short operations never show the bar, and once shown it remains visible long enough
/// to avoid a distracting flash on quick responses.
/// </summary>
public sealed class ShellProgressService : IDisposable
{
    private const int ShowDelayMs = 150;
    private const int MinimumVisibleMs = 300;

    private readonly HashSet<Guid> _operations = new();
    private CancellationTokenSource? _showDelay;
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

        if (!IsVisible && _operations.Count == 1)
        {
            ScheduleShow();
        }

        return new Scope(this, id);
    }

    private void End(Guid id)
    {
        if (!_operations.Remove(id) || _operations.Count > 0)
        {
            return;
        }

        CancelShow();

        if (IsVisible)
        {
            ScheduleHide();
        }
    }

    private void ScheduleShow()
    {
        CancelShow();

        var cancellation = new CancellationTokenSource();
        _showDelay = cancellation;
        _ = ShowAfterDelayAsync(cancellation);
    }

    private async Task ShowAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(ShowDelayMs, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_showDelay, cancellation))
            {
                _showDelay = null;
            }

            cancellation.Dispose();
        }

        if (_operations.Count == 0)
        {
            return;
        }

        IsVisible = true;
        _visibleSinceTick = Environment.TickCount64;
        NotifyChanged();
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

    private void CancelShow()
    {
        var cancellation = _showDelay;
        _showDelay = null;
        cancellation?.Cancel();
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
        CancelShow();
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
