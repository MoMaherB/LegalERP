namespace LegalERP.Web.Services.Toast;

public enum ToastLevel
{
    Success,
    Error,
    Warning,
    Info
}

public class ToastMessage
{
    public Guid Id { get; } = Guid.NewGuid();
    public ToastLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public interface IToastService
{
    event Action? OnChanged;
    List<ToastMessage> GetToasts();
    void ShowSuccess(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);
    void RemoveToast(Guid id);
}

public class ToastService : IToastService
{
    private readonly List<ToastMessage> _toasts = new();
    public event Action? OnChanged;
    
    // Auto-dismiss timer (6 seconds as requested)
    private readonly int _timeoutMs = 6000;

    public List<ToastMessage> GetToasts() => _toasts;

    public void ShowSuccess(string message) => AddToast(ToastLevel.Success, message);
    public void ShowError(string message) => AddToast(ToastLevel.Error, message);
    public void ShowWarning(string message) => AddToast(ToastLevel.Warning, message);
    public void ShowInfo(string message) => AddToast(ToastLevel.Info, message);

    private void AddToast(ToastLevel level, string message)
    {
        var toast = new ToastMessage { Level = level, Message = message };
        _toasts.Add(toast);
        NotifyStateChanged();

        // Start auto-dismiss timer
        Task.Run(async () =>
        {
            await Task.Delay(_timeoutMs);
            if (_toasts.Contains(toast))
            {
                RemoveToast(toast.Id);
            }
        });
    }

    public void RemoveToast(Guid id)
    {
        var toast = _toasts.FirstOrDefault(t => t.Id == id);
        if (toast != null)
        {
            _toasts.Remove(toast);
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChanged?.Invoke();
}
