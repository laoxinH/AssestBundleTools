using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewImageDisplayOptionsViewModelTests
{
    [Fact]
    public void ApplyImageState_ShouldShowCheckerboardByDefault_WhenImagePreviewExists()
    {
        var imageViewModel = new InspectorPreviewImageViewModel();

        imageViewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 32,
            imagePixelHeight: 32,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);
        var viewModel = imageViewModel.DisplayOptions;

        Assert.True(viewModel.IsCheckerboardBackgroundSelected);
        Assert.False(viewModel.IsSolidBackgroundSelected);
        Assert.Equal(Visibility.Visible, viewModel.BackgroundModeSwitchVisibility);
        Assert.Equal(Visibility.Visible, viewModel.CheckerboardBackgroundVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.SolidBackgroundVisibility);
        Assert.Equal(Visibility.Visible, viewModel.FullImagePreviewVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.SpriteModeSwitchVisibility);
    }

    [Fact]
    public void UseSolidBackground_ShouldSwitchBackgroundVisibility()
    {
        var imageViewModel = new InspectorPreviewImageViewModel();
        imageViewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 32,
            imagePixelHeight: 32,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);
        var viewModel = imageViewModel.DisplayOptions;

        viewModel.UseSolidBackground();

        Assert.False(viewModel.IsCheckerboardBackgroundSelected);
        Assert.True(viewModel.IsSolidBackgroundSelected);
        Assert.Equal("\uE566", viewModel.BackgroundModeGlyph);
        Assert.Equal(Visibility.Collapsed, viewModel.CheckerboardBackgroundVisibility);
        Assert.Equal(Visibility.Visible, viewModel.SolidBackgroundVisibility);
    }

    [Fact]
    public void ApplyImageState_ShouldPreferSpriteCropPreview_WhenSpriteFocusPreviewExists()
    {
        var imageViewModel = new InspectorPreviewImageViewModel();

        imageViewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 32,
            imagePixelHeight: 32,
            overlayDisplayState: null,
            focusPreviewDisplayState: new SpriteFocusPreviewDisplayState(
                CreateImageSourceStub(),
                "Crop Result",
                "32 x 32 px"));
        var viewModel = imageViewModel.DisplayOptions;

        Assert.False(viewModel.IsSpriteCropResultSelected);
        Assert.True(viewModel.IsSpriteCropPreviewSelected);
        Assert.Equal(Visibility.Visible, viewModel.SpriteModeSwitchVisibility);
        Assert.Equal(Visibility.Visible, viewModel.FullImagePreviewVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.SpriteCropResultVisibility);
    }

    [Fact]
    public void UseSpriteCropPreview_ShouldRestoreFullImagePreview()
    {
        var imageViewModel = new InspectorPreviewImageViewModel();
        imageViewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 32,
            imagePixelHeight: 32,
            overlayDisplayState: null,
            focusPreviewDisplayState: new SpriteFocusPreviewDisplayState(
                CreateImageSourceStub(),
                "Crop Result",
                "32 x 32 px"));
        var viewModel = imageViewModel.DisplayOptions;

        viewModel.UseSpriteCropPreview();

        Assert.False(viewModel.IsSpriteCropResultSelected);
        Assert.True(viewModel.IsSpriteCropPreviewSelected);
        Assert.Equal("\uE9C1", viewModel.SpriteDisplayModeGlyph);
        Assert.Equal(Visibility.Visible, viewModel.FullImagePreviewVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.SpriteCropResultVisibility);
    }

    [Fact]
    public void ToggleModes_ShouldCycleBackgroundAndSpriteDisplay()
    {
        var imageViewModel = new InspectorPreviewImageViewModel();
        imageViewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 32,
            imagePixelHeight: 32,
            overlayDisplayState: null,
            focusPreviewDisplayState: new SpriteFocusPreviewDisplayState(
                CreateImageSourceStub(),
                "Crop Result",
                "32 x 32 px"));
        var viewModel = imageViewModel.DisplayOptions;

        viewModel.ToggleBackgroundMode();
        viewModel.ToggleSpriteDisplayMode();

        Assert.True(viewModel.IsSolidBackgroundSelected);
        Assert.Equal("\uE566", viewModel.BackgroundModeGlyph);
        Assert.True(viewModel.IsSpriteCropResultSelected);
        Assert.Equal("\uEA6A", viewModel.SpriteDisplayModeGlyph);

        viewModel.ToggleBackgroundMode();
        viewModel.ToggleSpriteDisplayMode();

        Assert.True(viewModel.IsCheckerboardBackgroundSelected);
        Assert.Equal("\uE9C1", viewModel.BackgroundModeGlyph);
        Assert.True(viewModel.IsSpriteCropPreviewSelected);
        Assert.Equal("\uE9C1", viewModel.SpriteDisplayModeGlyph);
    }

    [Fact]
    public void UseSameModeAgain_ShouldNotifyGlyphState()
    {
        var imageViewModel = new InspectorPreviewImageViewModel();
        imageViewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 32,
            imagePixelHeight: 32,
            overlayDisplayState: null,
            focusPreviewDisplayState: new SpriteFocusPreviewDisplayState(
                CreateImageSourceStub(),
                "Crop Result",
                "32 x 32 px"));
        var viewModel = imageViewModel.DisplayOptions;
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.UseCheckerboardBackground();
        viewModel.UseSpriteCropPreview();

        Assert.Contains(nameof(InspectorPreviewImageDisplayOptionsViewModel.IsCheckerboardBackgroundSelected), changedProperties);
        Assert.Contains(nameof(InspectorPreviewImageDisplayOptionsViewModel.IsSpriteCropResultSelected), changedProperties);
        Assert.Contains(nameof(InspectorPreviewImageDisplayOptionsViewModel.BackgroundModeGlyph), changedProperties);
        Assert.Contains(nameof(InspectorPreviewImageDisplayOptionsViewModel.SpriteDisplayModeGlyph), changedProperties);
    }

    [Fact]
    public void RestoreDisplayOptions_ShouldApplyPersistedModes()
    {
        var viewModel = new InspectorPreviewImageDisplayOptionsViewModel();

        viewModel.RestoreDisplayOptions("Solid", "CropResult");

        Assert.True(viewModel.IsSolidBackgroundSelected);
        Assert.True(viewModel.IsSpriteCropResultSelected);
        Assert.Equal("Solid", viewModel.BackgroundModePersistenceKey);
        Assert.Equal("CropResult", viewModel.SpriteDisplayModePersistenceKey);
    }

    [Fact]
    public void RestoreDisplayOptions_ShouldFallbackToDefaults_WhenPersistedModesAreInvalid()
    {
        var viewModel = new InspectorPreviewImageDisplayOptionsViewModel();
        viewModel.UseSolidBackground();
        viewModel.UseSpriteCropResult();

        viewModel.RestoreDisplayOptions("99", "99");

        Assert.True(viewModel.IsCheckerboardBackgroundSelected);
        Assert.True(viewModel.IsSpriteCropPreviewSelected);
        Assert.Equal("Checkerboard", viewModel.BackgroundModePersistenceKey);
        Assert.Equal("CropPreview", viewModel.SpriteDisplayModePersistenceKey);
    }

    [Fact]
    public void ToggleModes_ShouldRaiseDisplayOptionsChanged_WhenModesChange()
    {
        var viewModel = new InspectorPreviewImageDisplayOptionsViewModel();
        var changeCount = 0;
        viewModel.DisplayOptionsChanged += (_, _) => changeCount++;

        viewModel.ToggleBackgroundMode();
        viewModel.ToggleSpriteDisplayMode();

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void RestoreDisplayOptions_ShouldNotRaiseDisplayOptionsChanged()
    {
        var viewModel = new InspectorPreviewImageDisplayOptionsViewModel();
        var changeCount = 0;
        viewModel.DisplayOptionsChanged += (_, _) => changeCount++;

        viewModel.RestoreDisplayOptions("Solid", "CropResult");

        Assert.Equal(0, changeCount);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }
}
