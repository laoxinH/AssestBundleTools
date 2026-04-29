using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 默认的应用级轻量通知服务实现。
/// </summary>
/// <remarks>
/// 当前统一在 Shell 层显示 toast，因此这里仅维护通知状态与生命周期，
/// 不直接依赖具体页面、控件或导航结构。
/// </remarks>
public sealed class AppNotificationService : ObservableObject, IAppNotificationService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);
    private bool _isOpen;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private AppNotificationSeverity _severity = AppNotificationSeverity.Informational;
    private CancellationTokenSource? _notificationLifetimeCts;

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public AppNotificationSeverity Severity
    {
        get => _severity;
        private set => SetProperty(ref _severity, value);
    }

    public void Show(string title, string message, AppNotificationSeverity severity, TimeSpan? duration = null)
    {
        _ = ShowCoreAsync(title, message, severity, duration ?? DefaultDuration);
    }

    public void Dismiss()
    {
        CancelNotificationLifetime();
        IsOpen = false;
    }

    private async Task ShowCoreAsync(
        string title,
        string message,
        AppNotificationSeverity severity,
        TimeSpan duration)
    {
        CancelNotificationLifetime();

        var cancellationSource = new CancellationTokenSource();
        _notificationLifetimeCts = cancellationSource;

        Title = title;
        Message = message;
        Severity = severity;
        IsOpen = true;

        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.Delay(duration, cancellationSource.Token);
            if (!cancellationSource.IsCancellationRequested)
            {
                IsOpen = false;
            }
        }
        catch (OperationCanceledException)
        {
            // 新通知接管显示周期时，取消旧通知属于预期行为。
        }
        finally
        {
            if (ReferenceEquals(_notificationLifetimeCts, cancellationSource))
            {
                _notificationLifetimeCts = null;
            }

            cancellationSource.Dispose();
        }
    }

    private void CancelNotificationLifetime()
    {
        if (_notificationLifetimeCts is null)
        {
            return;
        }

        _notificationLifetimeCts.Cancel();
        _notificationLifetimeCts.Dispose();
        _notificationLifetimeCts = null;
    }
}
