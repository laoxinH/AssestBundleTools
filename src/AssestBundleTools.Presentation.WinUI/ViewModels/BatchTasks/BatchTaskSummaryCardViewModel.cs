namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示批处理页左侧摘要卡片的一项统计数据。
/// </summary>
public sealed record BatchTaskSummaryCardViewModel(
    string Title,
    string ValueText,
    string Description);
