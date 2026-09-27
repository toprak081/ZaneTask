namespace ZaneTask.Web.Services;

public enum ToastKind { Success, Error }

public sealed record Toast(Guid Id, string Message, ToastKind Kind);

/// <summary>Short, auto-dismissing status messages announced to screen readers.</summary>
public sealed class ToastService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(4);
    private readonly List<Toast> _toasts = [];

    public event Action? Changed;

    public IReadOnlyList<Toast> Toasts => _toasts;

    public void Success(string message) => Show(message, ToastKind.Success);

    public void Error(string message) => Show(message, ToastKind.Error);

    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
            Changed?.Invoke();
    }

    private void Show(string message, ToastKind kind)
    {
        var toast = new Toast(Guid.NewGuid(), message, kind);
        _toasts.Add(toast);
        Changed?.Invoke();
        _ = Task.Delay(Lifetime).ContinueWith(_ => Dismiss(toast.Id), TaskScheduler.Default);
    }
}
