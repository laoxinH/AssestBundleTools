namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示 Bundle 外部依赖列表中的展示项。
/// </summary>
public sealed record BundleExternalDependencyViewModel(
    string DisplayName,
    string KindText,
    string ReferencePath);
