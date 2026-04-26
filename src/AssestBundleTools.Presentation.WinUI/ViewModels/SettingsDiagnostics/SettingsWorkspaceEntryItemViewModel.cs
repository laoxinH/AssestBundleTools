using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

/// <summary>
/// 表示“设置与诊断”页中一条工作区目录摘要。
/// </summary>
/// <remarks>
/// 该类型只承载已经格式化好的展示文本，
/// 避免 XAML 模板直接感知工作区上下文服务的内部结构。
/// </remarks>
public sealed record SettingsWorkspaceEntryItemViewModel(
    string DisplayName,
    string PrimaryBadgeText,
    string? SecondaryBadgeText,
    string SourceSummary,
    string LastActivatedSummary,
    string AvailabilitySummary)
{
    public Visibility SecondaryBadgeVisibility =>
        string.IsNullOrWhiteSpace(SecondaryBadgeText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility AvailabilitySummaryVisibility =>
        string.IsNullOrWhiteSpace(AvailabilitySummary) ? Visibility.Collapsed : Visibility.Visible;
}
