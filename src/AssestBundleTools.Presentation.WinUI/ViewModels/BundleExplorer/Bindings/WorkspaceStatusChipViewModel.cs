using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示工作区顶部摘要区中的一个状态标签。
/// </summary>
/// <remarks>
/// 这里刻意保持为轻量只读模型，
/// 方便把原本拥挤的一整行状态文本拆成可组合、可扩展的摘要标签。
/// </remarks>
public sealed class WorkspaceStatusChipViewModel
{
    public WorkspaceStatusChipViewModel(string label, string value, string glyph)
    {
        Label = label;
        Value = value;
        Glyph = glyph;
    }

    public string Label { get; }

    public string Value { get; }

    public string Glyph { get; }
}
