namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 定义应用级轻量通知的语义级别。
/// </summary>
/// <remarks>
/// 这里保持为 Presentation 层自己的枚举，
/// 避免让通用通知服务直接依赖某个具体控件的枚举类型。
/// </remarks>
public enum AppNotificationSeverity
{
    Informational,
    Success,
    Warning,
    Error
}
