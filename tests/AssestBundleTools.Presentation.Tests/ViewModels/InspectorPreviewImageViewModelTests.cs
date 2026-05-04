using System.Runtime.CompilerServices;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewImageViewModelTests
{
    [Fact]
    public void RestoreViewState_ShouldSurviveNextImagePreviewApply()
    {
        var viewModel = new InspectorPreviewImageViewModel();

        viewModel.RestoreViewState(1.75d, 18d, -12d);
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);

        Assert.Equal(1.75d, viewModel.Zoom);
        Assert.Equal(18d, viewModel.PanX);
        Assert.Equal(-12d, viewModel.PanY);
    }

    [Fact]
    public void InfoBadgeVisibility_ShouldFollowFullImageDisplayMode()
    {
        var viewModel = new InspectorPreviewImageViewModel();

        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: new SpriteFocusPreviewDisplayState(
                CreateImageSourceStub(),
                "Crop",
                "32 x 32 px"),
            infoDisplayState: new ImagePreviewInfoDisplayState(
                "Size 100 x 50 px",
                "Encoding RGBA32",
                "Format ID 47"));

        Assert.Equal(Visibility.Visible, viewModel.InfoBadgeVisibility);

        viewModel.DisplayOptions.UseSpriteCropResult();

        Assert.Equal(Visibility.Collapsed, viewModel.InfoBadgeVisibility);

        viewModel.DisplayOptions.UseSpriteCropPreview();

        Assert.Equal(Visibility.Visible, viewModel.InfoBadgeVisibility);
    }

    [Fact]
    public void ZoomBy_ShouldPreserveAnchoredSourcePoint()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);
        viewModel.UpdateViewport(300d, 200d);

        viewModel.ZoomBy(2d, anchorX: 150d, anchorY: 100d);

        Assert.Equal(2d, viewModel.Zoom);
        Assert.Equal(50d, viewModel.FullImageX, precision: 3);
        Assert.Equal(50d, viewModel.FullImageY, precision: 3);
        Assert.Equal(200d, viewModel.FullImageWidth, precision: 3);
        Assert.Equal(100d, viewModel.FullImageHeight, precision: 3);
    }

    [Fact]
    public void PanByAndResetView_ShouldRaiseViewStateChanged()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        var changeCount = 0;
        viewModel.ViewStateChanged += (_, _) => changeCount++;
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);
        viewModel.UpdateViewport(200d, 100d);

        viewModel.ZoomBy(1.25d);
        viewModel.PanBy(12d, -8d);
        viewModel.ResetView();

        Assert.Equal(3, changeCount);
        Assert.Equal(1d, viewModel.Zoom);
        Assert.Equal(0d, viewModel.PanX);
        Assert.Equal(0d, viewModel.PanY);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }
}
