using System.ComponentModel;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 提供应用级的非阻断轻量通知能力。
/// </summary>
/// <remarks>
/// 该服务只承载“无需用户立即决策”的即时反馈，
/// 例如成功提示、后台失败提示和入口级说明。
/// 需要用户确认的流程仍应使用对话框或页面内联校验。
/// </remarks>
public interface IAppNotificationService : INotifyPropertyChanged
{
    bool IsOpen { get; }

    string Title { get; }

    string Message { get; }

    AppNotificationSeverity Severity { get; }

    void Show(string title, string message, AppNotificationSeverity severity, TimeSpan? duration = null);

    void Dismiss();
}
