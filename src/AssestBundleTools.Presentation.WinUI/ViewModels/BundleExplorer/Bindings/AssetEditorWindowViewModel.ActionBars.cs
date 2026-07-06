using Microsoft.UI.Xaml;
using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    public Visibility NormalActionBarVisibility =>
        IsSpriteEditModeActive || IsSpineEditModeActive || IsTextEditModeActive
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility SpriteEditActionBarVisibility =>
        IsSpriteEditModeActive ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpineEditPanelVisibility =>
        DocumentGroup.ActiveSpineGroupTab?.SpineEditPanelVisibility ?? Visibility.Collapsed;

    public SpineAssetGroupEditDraftViewModel? ActiveSpineEditDraft =>
        DocumentGroup.ActiveSpineGroupTab?.SpineEditDraft;

    public Visibility SpineEditActionBarVisibility =>
        IsSpineEditModeActive ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TextEditPanelVisibility =>
        IsTextEditModeActive ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewSurfaceVisibility =>
        IsTextEditModeActive ? Visibility.Collapsed : Visibility.Visible;

    public TextAssetEditDraftViewModel? ActiveTextEditDraft =>
        DocumentGroup.ActiveAssetTab?.TextEditDraft;

    public Visibility TextEditActionBarVisibility =>
        IsTextEditModeActive ? Visibility.Visible : Visibility.Collapsed;

    public bool IsTextEditLoading =>
        DocumentGroup.ActiveAssetTab?.TextEditDraft?.IsFullEditLoading == true;

    public Visibility TextEditLoadingVisibility =>
        IsTextEditLoading ? Visibility.Visible : Visibility.Collapsed;

    public string SaveTextEditButtonContentKey =>
        IsTextEditLoading
            ? ResourceKeys.TextAssetEditorLoadingEditButton
            : ResourceKeys.TextAssetEditorApplyButton;

    public string SaveTextEditButtonToolTipKey =>
        IsTextEditLoading
            ? ResourceKeys.TextAssetEditorLoadingEditButtonToolTip
            : ResourceKeys.TextAssetEditorApplyButton;

    public bool CanSaveTextEdit =>
        DocumentGroup.ActiveAssetTab?.TextEditDraft?.CanSave == true &&
        !IsTextEditLoading &&
        !IsBusy;

    public bool CanSaveSpineEdit =>
        DocumentGroup.ActiveSpineGroupTab?.CanSaveSpineEdit == true;

    public bool CanUndoSpineEdit =>
        DocumentGroup.ActiveSpineGroupTab?.CanUndoSpineEdit == true;

    public bool CanRedoSpineEdit =>
        DocumentGroup.ActiveSpineGroupTab?.CanRedoSpineEdit == true;

    private bool IsSpriteEditModeActive =>
        DocumentGroup.ActiveAssetTab?.Inspector.Preview.Image.IsSpriteEditModeActive == true;

    private bool IsSpineEditModeActive =>
        DocumentGroup.ActiveSpineGroupTab?.IsSpineEditModeActive == true;

    private bool IsTextEditModeActive =>
        DocumentGroup.ActiveAssetTab?.IsTextEditModeActive == true;

    private void NotifyActionBarStates()
    {
        OnPropertyChanged(nameof(NormalActionBarVisibility));
        OnPropertyChanged(nameof(SpriteEditActionBarVisibility));
        OnPropertyChanged(nameof(SpineEditPanelVisibility));
        OnPropertyChanged(nameof(ActiveSpineEditDraft));
        OnPropertyChanged(nameof(SpineEditActionBarVisibility));
        OnPropertyChanged(nameof(TextEditPanelVisibility));
        OnPropertyChanged(nameof(PreviewSurfaceVisibility));
        OnPropertyChanged(nameof(ActiveTextEditDraft));
        OnPropertyChanged(nameof(TextEditActionBarVisibility));
        OnPropertyChanged(nameof(IsTextEditLoading));
        OnPropertyChanged(nameof(TextEditLoadingVisibility));
        OnPropertyChanged(nameof(SaveTextEditButtonContentKey));
        OnPropertyChanged(nameof(SaveTextEditButtonToolTipKey));
        OnPropertyChanged(nameof(CanSaveTextEdit));
        OnPropertyChanged(nameof(CanSaveSpineEdit));
        OnPropertyChanged(nameof(CanUndoSpineEdit));
        OnPropertyChanged(nameof(CanRedoSpineEdit));
    }

    private void ObserveActiveSpriteEditImage()
    {
        var nextImage = DocumentGroup.ActiveAssetTab?.Inspector.Preview.Image;
        if (ReferenceEquals(_observedSpriteEditImage, nextImage))
        {
            return;
        }

        if (_observedSpriteEditImage is not null)
        {
            _observedSpriteEditImage.PropertyChanged -= OnObservedSpriteEditImagePropertyChanged;
        }

        _observedSpriteEditImage = nextImage;
        if (_observedSpriteEditImage is not null)
        {
            _observedSpriteEditImage.PropertyChanged += OnObservedSpriteEditImagePropertyChanged;
        }
    }

    private void ClearObservedSpriteEditImage()
    {
        if (_observedSpriteEditImage is null)
        {
            return;
        }

        _observedSpriteEditImage.PropertyChanged -= OnObservedSpriteEditImagePropertyChanged;
        _observedSpriteEditImage = null;
    }

    private void OnObservedSpriteEditImagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InspectorPreviewImageViewModel.IsSpriteEditModeActive) or
            nameof(InspectorPreviewImageViewModel.CanSaveSpriteEdit) or
            nameof(InspectorPreviewImageViewModel.CanUndoSpriteEdit) or
            nameof(InspectorPreviewImageViewModel.CanRedoSpriteEdit) or
            nameof(InspectorPreviewImageViewModel.HasUnsavedSpriteEditChanges) or
            nameof(InspectorPreviewImageViewModel.SpriteEditActionBarEditVisibility))
        {
            NotifyActionBarStates();
        }
    }

    private void ObserveActiveTextEditDraft()
    {
        var nextDraft = DocumentGroup.ActiveAssetTab?.TextEditDraft;
        if (ReferenceEquals(_observedTextEditDraft, nextDraft))
        {
            return;
        }

        if (_observedTextEditDraft is not null)
        {
            _observedTextEditDraft.PropertyChanged -= OnObservedTextEditDraftPropertyChanged;
        }

        _observedTextEditDraft = nextDraft;
        if (_observedTextEditDraft is not null)
        {
            _observedTextEditDraft.PropertyChanged += OnObservedTextEditDraftPropertyChanged;
        }
    }

    private void ClearObservedTextEditDraft()
    {
        if (_observedTextEditDraft is null)
        {
            return;
        }

        _observedTextEditDraft.PropertyChanged -= OnObservedTextEditDraftPropertyChanged;
        _observedTextEditDraft = null;
    }

    private void OnObservedTextEditDraftPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TextAssetEditDraftViewModel.HasUnsavedChanges) or
            nameof(TextAssetEditDraftViewModel.CanSave) or
            nameof(TextAssetEditDraftViewModel.IsFullEditLoading))
        {
            NotifyActionBarStates();
        }
    }
}
