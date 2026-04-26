using AssestBundleTools.Application.Replacement;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 表示批量替换预览中的来源筛选项。
/// </summary>
public sealed record BatchReplacementSourceOptionViewModel(
    string DisplayName,
    BatchReplacementSourceKind SourceKind);
