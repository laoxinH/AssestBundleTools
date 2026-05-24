namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private void NotifyOverlayVisibilityChanged()
    {
        OnPropertyChanged(nameof(OverlayVisibility));
        OnPropertyChanged(nameof(OverlayFrameVisibility));
        OnPropertyChanged(nameof(OverlayBadgeVisibility));
        OnPropertyChanged(nameof(SpriteEditFrameVisibility));
        OnPropertyChanged(nameof(SpriteEditHandleVisibility));
        OnPropertyChanged(nameof(SpriteEditActionControlsVisibility));
        OnPropertyChanged(nameof(SpriteEditMeasurementVisibility));
        OnPropertyChanged(nameof(PivotVisibility));
        OnPropertyChanged(nameof(BorderVisibility));
    }

    private void NotifyOverlayGeometryChanged()
    {
        OnPropertyChanged(nameof(OverlayRightX));
        OnPropertyChanged(nameof(OverlayBottomY));
        NotifySpriteEditGeometryChanged();
        NotifySpriteEditMeasurementChanged();
    }

    private void NotifyRegionOverlayVisibilityChanged()
    {
        OnPropertyChanged(nameof(RegionOverlayVisibility));
        OnPropertyChanged(nameof(RegionOverlayBadgeVisibility));
    }

    private void NotifyInfoBadgeVisibilityChanged()
    {
        OnPropertyChanged(nameof(InfoBadgeVisibility));
        OnPropertyChanged(nameof(InfoBadgeSecondaryVisibility));
        OnPropertyChanged(nameof(InfoBadgeDetailVisibility));
    }

    private void NotifyViewStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(Zoom));
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }

    private bool HasInfoBadgeText =>
        !string.IsNullOrWhiteSpace(InfoBadgePrimaryText) ||
        !string.IsNullOrWhiteSpace(InfoBadgeSecondaryText) ||
        !string.IsNullOrWhiteSpace(InfoBadgeDetailText);
}
