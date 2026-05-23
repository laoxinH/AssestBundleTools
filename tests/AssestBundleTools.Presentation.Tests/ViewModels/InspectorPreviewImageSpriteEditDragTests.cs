using System.Runtime.CompilerServices;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewImageSpriteEditDragTests
{
    [Fact]
    public void SpriteEditDrag_ShouldDeferViewportResizeUntilDragCompletes()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);
        var originalFullImageX = viewModel.FullImageX;
        var originalFullImageY = viewModel.FullImageY;
        var originalOverlayRightX = viewModel.OverlayRightX;
        var originalOverlayBottomY = viewModel.OverlayBottomY;
        var startViewportX = MapSourceXToViewport(viewModel, 10d);
        var startViewportY = MapSourceYToViewport(viewModel, 20d);
        var endViewportX = MapSourceXToViewport(viewModel, 15d);
        var endViewportY = MapSourceYToViewport(viewModel, 25d);

        Assert.True(viewModel.TryBeginSpriteEditDrag(startViewportX, startViewportY));
        viewModel.UpdateViewport(260d, 200d);
        viewModel.UpdateSpriteEditDrag(endViewportX, endViewportY);

        Assert.Equal(originalFullImageX, viewModel.FullImageX, precision: 3);
        Assert.Equal(originalFullImageY, viewModel.FullImageY, precision: 3);
        Assert.Equal(15d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(25d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(25d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(35d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(originalOverlayRightX, viewModel.OverlayRightX, precision: 3);
        Assert.Equal(originalOverlayBottomY, viewModel.OverlayBottomY, precision: 3);

        viewModel.CompleteSpriteEditDrag();

        Assert.Equal(54d, viewModel.FullImageX, precision: 3);
        Assert.Equal(24d, viewModel.FullImageY, precision: 3);
        Assert.Equal(152d, viewModel.FullImageWidth, precision: 3);
        Assert.Equal(152d, viewModel.FullImageHeight, precision: 3);
    }

    private static void ApplySpriteEditPreview(InspectorPreviewImageViewModel viewModel)
    {
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 100,
            overlayDisplayState: new SpritePreviewOverlayDisplayState(
                CanvasWidth: 100,
                CanvasHeight: 100,
                RegionX: 10,
                RegionY: 20,
                RegionWidth: 30,
                RegionHeight: 40,
                PivotX: 20,
                PivotY: 30,
                BorderLeft: null,
                BorderTop: null,
                BorderRight: null,
                BorderBottom: null,
                BadgeText: "sprite"),
            focusPreviewDisplayState: null);
        viewModel.UpdateViewport(200d, 200d);
        viewModel.RequestSpriteEditMode();
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }

    private static double MapSourceXToViewport(InspectorPreviewImageViewModel viewModel, double sourceX)
    {
        return viewModel.FullImageX + (sourceX * (viewModel.FullImageWidth / viewModel.CanvasWidth));
    }

    private static double MapSourceYToViewport(InspectorPreviewImageViewModel viewModel, double sourceY)
    {
        return viewModel.FullImageY + (sourceY * (viewModel.FullImageHeight / viewModel.CanvasHeight));
    }
}
