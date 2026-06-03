using Microsoft.UI.Xaml;
using System.ComponentModel;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    public Visibility NormalActionBarVisibility =>
        IsSpriteEditModeActive || IsSpineEditModeActive
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

    private void NotifyActionBarStates()
    {
        OnPropertyChanged(nameof(NormalActionBarVisibility));
        OnPropertyChanged(nameof(SpriteEditActionBarVisibility));
        OnPropertyChanged(nameof(SpineEditPanelVisibility));
        OnPropertyChanged(nameof(ActiveSpineEditDraft));
        OnPropertyChanged(nameof(SpineEditActionBarVisibility));
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
}
