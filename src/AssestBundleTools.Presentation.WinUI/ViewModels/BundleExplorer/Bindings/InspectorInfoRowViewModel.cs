using Microsoft.UI.Xaml;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器信息卡中的单行键值信息。
/// </summary>
public sealed class InspectorInfoRowViewModel
{
    public InspectorInfoRowViewModel(string label, string value)
    {
        Label = string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : label.Trim();
        Value = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    public string Label { get; }

    public string Value { get; }

    public Visibility LabelVisibility =>
        string.IsNullOrWhiteSpace(Label)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public string CopyText =>
        string.IsNullOrWhiteSpace(Label)
            ? Value
            : $"{Label}: {Value}";
}
