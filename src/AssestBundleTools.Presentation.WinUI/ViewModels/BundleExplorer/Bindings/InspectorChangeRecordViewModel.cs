using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器“变更”页中的一条独立操作记录。
/// </summary>
public sealed record InspectorChangeRecordViewModel(
    string PrimaryText,
    string SecondaryText,
    bool ShowsUndoReplacement,
    bool ShowsRedoReplacement,
    string UndoButtonText,
    string RedoButtonText)
{
    public Visibility SecondaryTextVisibility =>
        string.IsNullOrWhiteSpace(SecondaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility UndoButtonVisibility =>
        ShowsUndoReplacement ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RedoButtonVisibility =>
        ShowsRedoReplacement ? Visibility.Visible : Visibility.Collapsed;
}
