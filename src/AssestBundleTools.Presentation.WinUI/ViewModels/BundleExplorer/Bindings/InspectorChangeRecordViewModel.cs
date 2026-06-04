using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器“变更”页中的一条独立操作记录。
/// </summary>
public sealed record InspectorChangeRecordViewModel(
    string OperationId,
    string TargetBundleId,
    string TargetAssetId,
    string TargetAssetName,
    string TitleText,
    string BadgeText,
    string SourceText,
    string TimeText,
    string SecondaryText,
    bool ShowsUndoReplacement,
    bool ShowsRedoReplacement,
    bool ShowsDeleteRecord,
    bool IsActionEnabled,
    string UndoButtonText,
    string RedoButtonText,
    string DeleteButtonText,
    string Glyph)
{
    public bool HasOperationId => !string.IsNullOrWhiteSpace(OperationId);

    public bool HasTargetAsset =>
        !string.IsNullOrWhiteSpace(TargetBundleId) &&
        !string.IsNullOrWhiteSpace(TargetAssetId);

    public Visibility SourceTextVisibility =>
        string.IsNullOrWhiteSpace(SourceText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility BadgeVisibility =>
        string.IsNullOrWhiteSpace(BadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SecondaryTextVisibility =>
        string.IsNullOrWhiteSpace(SecondaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility UndoButtonVisibility =>
        ShowsUndoReplacement ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RedoButtonVisibility =>
        ShowsRedoReplacement ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DeleteButtonVisibility =>
        ShowsDeleteRecord ? Visibility.Visible : Visibility.Collapsed;
}
