namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示详细日志面板中的一条展示记录。
/// </summary>
public sealed record LogEntryViewModel(
    string TimestampText,
    string Message);
