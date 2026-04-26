namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示批量提取预览中的单个资源包聚合项。
/// </summary>
public sealed record BatchTaskExtractionBundleItemViewModel(
    string BundleDisplayName,
    string SummaryText);
