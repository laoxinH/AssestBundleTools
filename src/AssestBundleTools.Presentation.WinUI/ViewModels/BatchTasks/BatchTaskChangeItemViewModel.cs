namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示批处理页中的单个变更摘要项。
/// </summary>
public sealed record BatchTaskChangeItemViewModel(
    string AssetName,
    string BundleDisplayName,
    string AssetTypeText,
    string StateText,
    string DetailText,
    string TimeText);
