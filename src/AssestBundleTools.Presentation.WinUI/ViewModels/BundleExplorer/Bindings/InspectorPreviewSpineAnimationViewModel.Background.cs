using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel
{
    private const string CheckerboardBackgroundGlyphValue = "\uECA5";
    private const string SolidBackgroundGlyphValue = "\uE790";

    public bool IsCheckerboardBackgroundSelected =>
        _backgroundMode == InspectorPreviewImageBackgroundMode.Checkerboard;

    public bool IsSolidBackgroundSelected =>
        _backgroundMode == InspectorPreviewImageBackgroundMode.Solid;

    public string BackgroundModeGlyph => IsCheckerboardBackgroundSelected
        ? CheckerboardBackgroundGlyphValue
        : SolidBackgroundGlyphValue;

    public Visibility CheckerboardBackgroundVisibility =>
        IsCheckerboardBackgroundSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SolidBackgroundVisibility =>
        IsSolidBackgroundSelected ? Visibility.Visible : Visibility.Collapsed;

    public void ToggleBackgroundMode()
    {
        SetBackgroundMode(IsCheckerboardBackgroundSelected
            ? InspectorPreviewImageBackgroundMode.Solid
            : InspectorPreviewImageBackgroundMode.Checkerboard);
    }

    private void SetBackgroundMode(InspectorPreviewImageBackgroundMode mode)
    {
        if (EqualityComparer<InspectorPreviewImageBackgroundMode>.Default.Equals(_backgroundMode, mode))
        {
            return;
        }

        _backgroundMode = mode;
        OnPropertyChanged(nameof(IsCheckerboardBackgroundSelected));
        OnPropertyChanged(nameof(IsSolidBackgroundSelected));
        OnPropertyChanged(nameof(BackgroundModeGlyph));
        OnPropertyChanged(nameof(CheckerboardBackgroundVisibility));
        OnPropertyChanged(nameof(SolidBackgroundVisibility));
        RaisePreviewStateChanged();
    }
}
