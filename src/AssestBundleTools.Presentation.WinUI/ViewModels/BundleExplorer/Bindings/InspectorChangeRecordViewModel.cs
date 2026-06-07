using Microsoft.UI.Xaml;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示检查器“变更”页中的一条独立操作记录。
/// </summary>
public sealed class InspectorChangeRecordViewModel : ObservableObject
{
    private bool _isPending;

    public InspectorChangeRecordViewModel(
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
        bool CanExecuteAction,
        bool IsPending,
        string UndoButtonText,
        string RedoButtonText,
        string DeleteButtonText,
        string Glyph)
    {
        this.OperationId = OperationId;
        this.TargetBundleId = TargetBundleId;
        this.TargetAssetId = TargetAssetId;
        this.TargetAssetName = TargetAssetName;
        this.TitleText = TitleText;
        this.BadgeText = BadgeText;
        this.SourceText = SourceText;
        this.TimeText = TimeText;
        this.SecondaryText = SecondaryText;
        this.ShowsUndoReplacement = ShowsUndoReplacement;
        this.ShowsRedoReplacement = ShowsRedoReplacement;
        this.ShowsDeleteRecord = ShowsDeleteRecord;
        this.CanExecuteAction = CanExecuteAction;
        _isPending = IsPending;
        this.UndoButtonText = UndoButtonText;
        this.RedoButtonText = RedoButtonText;
        this.DeleteButtonText = DeleteButtonText;
        this.Glyph = Glyph;
    }

    public string OperationId { get; }

    public string TargetBundleId { get; }

    public string TargetAssetId { get; }

    public string TargetAssetName { get; }

    public string TitleText { get; }

    public string BadgeText { get; }

    public string SourceText { get; }

    public string TimeText { get; }

    public string SecondaryText { get; }

    public bool ShowsUndoReplacement { get; }

    public bool ShowsRedoReplacement { get; }

    public bool ShowsDeleteRecord { get; }

    public bool CanExecuteAction { get; }

    public bool IsPending
    {
        get => _isPending;
        private set
        {
            if (SetProperty(ref _isPending, value))
            {
                OnPropertyChanged(nameof(IsActionEnabled));
                OnPropertyChanged(nameof(PendingVisibility));
            }
        }
    }

    public string UndoButtonText { get; }

    public string RedoButtonText { get; }

    public string DeleteButtonText { get; }

    public string Glyph { get; }

    public bool HasOperationId => !string.IsNullOrWhiteSpace(OperationId);

    public bool HasTargetAsset =>
        !string.IsNullOrWhiteSpace(TargetBundleId) &&
        !string.IsNullOrWhiteSpace(TargetAssetId);

    public bool IsActionEnabled => CanExecuteAction && !IsPending;

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

    public Visibility PendingVisibility =>
        IsPending ? Visibility.Visible : Visibility.Collapsed;

    public void SetPending(bool isPending)
    {
        IsPending = isPending;
    }
}
