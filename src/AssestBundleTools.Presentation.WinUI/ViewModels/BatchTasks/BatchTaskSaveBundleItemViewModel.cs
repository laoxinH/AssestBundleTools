namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示保存前摘要中的单个资源包输出计划行。
/// </summary>
public sealed record BatchTaskSaveBundleItemViewModel(
    string BundleDisplayName,
    string OutputPath,
    string SummaryText);
