using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public enum InspectorPreviewImageBackgroundMode
{
    Checkerboard,
    Solid
}

public enum InspectorPreviewSpriteDisplayMode
{
    CropResult,
    CropPreview
}

/// <summary>
/// 承载图片预览的纯展示选项，避免把背景和 Sprite 模式切换状态散落在控件层。
/// </summary>
public sealed class InspectorPreviewImageDisplayOptionsViewModel : ObservableObject
{
    private const string CheckerboardBackgroundGlyphValue = "\uE9C1";
    private const string SolidBackgroundGlyphValue = "\uE566";
    private const string SpriteCropResultGlyphValue = "\uEA6A";
    private const string SpriteCropPreviewGlyphValue = "\uE9C1";

    private InspectorPreviewImageBackgroundMode _backgroundMode = InspectorPreviewImageBackgroundMode.Checkerboard;
    private InspectorPreviewSpriteDisplayMode _spriteDisplayMode = InspectorPreviewSpriteDisplayMode.CropPreview;
    private bool _hasImagePreview;
    private bool _hasSpriteFocusPreview;

    public event EventHandler? DisplayOptionsChanged;

    public bool IsCheckerboardBackgroundSelected => _backgroundMode == InspectorPreviewImageBackgroundMode.Checkerboard;

    public bool IsSolidBackgroundSelected => _backgroundMode == InspectorPreviewImageBackgroundMode.Solid;

    public bool IsSpriteCropResultSelected => _spriteDisplayMode == InspectorPreviewSpriteDisplayMode.CropResult;

    public bool IsSpriteCropPreviewSelected => _spriteDisplayMode == InspectorPreviewSpriteDisplayMode.CropPreview;

    public string BackgroundModeGlyph => IsCheckerboardBackgroundSelected
        ? CheckerboardBackgroundGlyphValue
        : SolidBackgroundGlyphValue;

    public string SpriteDisplayModeGlyph => IsSpriteCropResultSelected
        ? SpriteCropResultGlyphValue
        : SpriteCropPreviewGlyphValue;

    public Visibility BackgroundModeSwitchVisibility => _hasImagePreview ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpriteModeSwitchVisibility =>
        _hasImagePreview && _hasSpriteFocusPreview ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CheckerboardBackgroundVisibility =>
        IsCheckerboardBackgroundSelected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SolidBackgroundVisibility =>
        IsSolidBackgroundSelected ? Visibility.Visible : Visibility.Collapsed;

    public string BackgroundModePersistenceKey => _backgroundMode.ToString();

    public string SpriteDisplayModePersistenceKey => _spriteDisplayMode.ToString();

    public Visibility FullImagePreviewVisibility =>
        _hasImagePreview && (!_hasSpriteFocusPreview || IsSpriteCropPreviewSelected)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SpriteCropResultVisibility =>
        _hasImagePreview && _hasSpriteFocusPreview && IsSpriteCropResultSelected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public void UseCheckerboardBackground()
    {
        SetBackgroundMode(InspectorPreviewImageBackgroundMode.Checkerboard);
    }

    public void UseSolidBackground()
    {
        SetBackgroundMode(InspectorPreviewImageBackgroundMode.Solid);
    }

    public void ToggleBackgroundMode()
    {
        SetBackgroundMode(IsCheckerboardBackgroundSelected
            ? InspectorPreviewImageBackgroundMode.Solid
            : InspectorPreviewImageBackgroundMode.Checkerboard);
    }

    public void UseSpriteCropResult()
    {
        SetSpriteDisplayMode(InspectorPreviewSpriteDisplayMode.CropResult);
    }

    public void UseSpriteCropPreview()
    {
        SetSpriteDisplayMode(InspectorPreviewSpriteDisplayMode.CropPreview);
    }

    public void ToggleSpriteDisplayMode()
    {
        SetSpriteDisplayMode(IsSpriteCropResultSelected
            ? InspectorPreviewSpriteDisplayMode.CropPreview
            : InspectorPreviewSpriteDisplayMode.CropResult);
    }

    public void RestoreDisplayOptions(string? backgroundMode, string? spriteDisplayMode)
    {
        SetBackgroundMode(ParseBackgroundMode(backgroundMode), notifyDisplayOptionsChanged: false);
        SetSpriteDisplayMode(ParseSpriteDisplayMode(spriteDisplayMode), notifyDisplayOptionsChanged: false);
    }

    internal void UpdateImageState(bool hasImagePreview, bool hasSpriteFocusPreview)
    {
        if (_hasImagePreview == hasImagePreview &&
            _hasSpriteFocusPreview == hasSpriteFocusPreview)
        {
            return;
        }

        _hasImagePreview = hasImagePreview;
        _hasSpriteFocusPreview = hasSpriteFocusPreview;
        NotifyImageStateChanged();
    }

    private void SetBackgroundMode(
        InspectorPreviewImageBackgroundMode mode,
        bool notifyDisplayOptionsChanged = true)
    {
        var changed = SetProperty(ref _backgroundMode, mode, nameof(IsCheckerboardBackgroundSelected));
        NotifyBackgroundModeChanged();
        if (changed && notifyDisplayOptionsChanged)
        {
            DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetSpriteDisplayMode(
        InspectorPreviewSpriteDisplayMode mode,
        bool notifyDisplayOptionsChanged = true)
    {
        var changed = SetProperty(ref _spriteDisplayMode, mode, nameof(IsSpriteCropResultSelected));
        NotifySpriteDisplayModeChanged();
        if (changed && notifyDisplayOptionsChanged)
        {
            DisplayOptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyBackgroundModeChanged()
    {
        OnPropertyChanged(nameof(IsCheckerboardBackgroundSelected));
        OnPropertyChanged(nameof(IsSolidBackgroundSelected));
        OnPropertyChanged(nameof(BackgroundModeGlyph));
        OnPropertyChanged(nameof(CheckerboardBackgroundVisibility));
        OnPropertyChanged(nameof(SolidBackgroundVisibility));
        OnPropertyChanged(nameof(BackgroundModePersistenceKey));
    }

    private void NotifySpriteDisplayModeChanged()
    {
        OnPropertyChanged(nameof(IsSpriteCropResultSelected));
        OnPropertyChanged(nameof(IsSpriteCropPreviewSelected));
        OnPropertyChanged(nameof(SpriteDisplayModeGlyph));
        OnPropertyChanged(nameof(FullImagePreviewVisibility));
        OnPropertyChanged(nameof(SpriteCropResultVisibility));
        OnPropertyChanged(nameof(SpriteDisplayModePersistenceKey));
    }

    private void NotifyImageStateChanged()
    {
        OnPropertyChanged(nameof(BackgroundModeSwitchVisibility));
        OnPropertyChanged(nameof(SpriteModeSwitchVisibility));
        OnPropertyChanged(nameof(FullImagePreviewVisibility));
        OnPropertyChanged(nameof(SpriteCropResultVisibility));
    }

    private static InspectorPreviewImageBackgroundMode ParseBackgroundMode(string? mode)
    {
        return Enum.TryParse<InspectorPreviewImageBackgroundMode>(
            mode,
            ignoreCase: true,
            out var parsedMode) &&
            Enum.IsDefined(parsedMode)
            ? parsedMode
            : InspectorPreviewImageBackgroundMode.Checkerboard;
    }

    private static InspectorPreviewSpriteDisplayMode ParseSpriteDisplayMode(string? mode)
    {
        return Enum.TryParse<InspectorPreviewSpriteDisplayMode>(
            mode,
            ignoreCase: true,
            out var parsedMode) &&
            Enum.IsDefined(parsedMode)
            ? parsedMode
            : InspectorPreviewSpriteDisplayMode.CropPreview;
    }
}
