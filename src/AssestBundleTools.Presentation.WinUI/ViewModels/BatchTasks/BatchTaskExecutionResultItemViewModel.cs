namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示批处理页右侧结果面板中的一条结构化执行结果。
/// </summary>
public sealed record BatchTaskExecutionResultItemViewModel(
    string Title,
    string SummaryText,
    string DetailText,
    string StatusText,
    string TimeText);
