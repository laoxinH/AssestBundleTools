namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示批量替换预览中的单个资源包聚合项。
/// </summary>
public sealed record BatchTaskReplacementBundleItemViewModel(
    string BundleDisplayName,
    string SummaryText);
