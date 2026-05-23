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

    [Fact]
    public void SpriteEditGeometryInput_ShouldBeIgnoredWhileDragIsActive()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);
        var startViewportX = MapSourceXToViewport(viewModel, 10d);
        var startViewportY = MapSourceYToViewport(viewModel, 20d);
        var endViewportX = MapSourceXToViewport(viewModel, 15d);
        var endViewportY = MapSourceYToViewport(viewModel, 25d);

        Assert.True(viewModel.TryBeginSpriteEditDrag(startViewportX, startViewportY));
        Assert.True(viewModel.IsSpriteEditDragActive);

        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 99d,
            cropY: 88d,
            cropWidth: 77d,
            cropHeight: 66d,
            pivotX: 55d,
            pivotY: 44d,
            inputKind: SpriteEditGeometryInputKind.Crop);
        viewModel.UpdateSpriteEditDrag(endViewportX, endViewportY);
        viewModel.CompleteSpriteEditDrag();

        Assert.False(viewModel.IsSpriteEditDragActive);
        Assert.Equal(15d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(25d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(25d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(35d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(23.333d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(33.75d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditResize_ShouldKeepPivotAtRelativeCropPosition()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        DragFromSource(viewModel, 40d, 60d, 55d, 80d);

        Assert.Equal(10d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(20d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(45d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(60d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(25d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(35d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditPivotDrag_ShouldRefreshRelativePositionForNextCropResize()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        DragFromSource(viewModel, 20d, 30d, 30d, 50d);
        DragFromSource(viewModel, 40d, 60d, 50d, 80d);

        Assert.Equal(10d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(20d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(60d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(36.667d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(65d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Theory]
    [InlineData(10d, 20d, 15d, 25d, false, false, true, true)]
    [InlineData(40d, 20d, 45d, 25d, true, false, false, true)]
    [InlineData(10d, 60d, 15d, 65d, false, true, true, false)]
    [InlineData(40d, 60d, 45d, 65d, true, true, false, false)]
    [InlineData(10d, 40d, 15d, 45d, false, true, true, true)]
    [InlineData(40d, 40d, 45d, 45d, true, true, false, true)]
    [InlineData(25d, 20d, 30d, 25d, true, false, true, true)]
    [InlineData(25d, 60d, 30d, 65d, true, true, true, false)]
    public void SpriteEditResize_ShouldKeepExpectedOppositeEdgesFixed(
        double startSourceX,
        double startSourceY,
        double endSourceX,
        double endSourceY,
        bool keepLeft,
        bool keepTop,
        bool keepRight,
        bool keepBottom)
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);
        var originalLeft = viewModel.SpriteEditCropX;
        var originalTop = viewModel.SpriteEditCropY;
        var originalRight = viewModel.SpriteEditCropX + viewModel.SpriteEditCropWidth;
        var originalBottom = viewModel.SpriteEditCropY + viewModel.SpriteEditCropHeight;
        var originalViewportLeft = viewModel.OverlayX;
        var originalViewportTop = viewModel.OverlayY;
        var originalViewportRight = viewModel.OverlayRightX;
        var originalViewportBottom = viewModel.OverlayBottomY;

        Assert.True(viewModel.TryBeginSpriteEditDrag(
            MapSourceXToViewport(viewModel, startSourceX),
            MapSourceYToViewport(viewModel, startSourceY)));
        viewModel.UpdateSpriteEditDrag(
            MapSourceXToViewport(viewModel, endSourceX),
            MapSourceYToViewport(viewModel, endSourceY));

        AssertEdge(keepLeft, originalLeft, viewModel.SpriteEditCropX);
        AssertEdge(keepTop, originalTop, viewModel.SpriteEditCropY);
        AssertEdge(keepRight, originalRight, viewModel.SpriteEditCropX + viewModel.SpriteEditCropWidth);
        AssertEdge(keepBottom, originalBottom, viewModel.SpriteEditCropY + viewModel.SpriteEditCropHeight);
        AssertEdge(keepLeft, originalViewportLeft, viewModel.OverlayX);
        AssertEdge(keepTop, originalViewportTop, viewModel.OverlayY);
        AssertEdge(keepRight, originalViewportRight, viewModel.OverlayRightX);
        AssertEdge(keepBottom, originalViewportBottom, viewModel.OverlayBottomY);
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

    private static void DragFromSource(
        InspectorPreviewImageViewModel viewModel,
        double startSourceX,
        double startSourceY,
        double endSourceX,
        double endSourceY)
    {
        Assert.True(viewModel.TryBeginSpriteEditDrag(
            MapSourceXToViewport(viewModel, startSourceX),
            MapSourceYToViewport(viewModel, startSourceY)));
        viewModel.UpdateSpriteEditDrag(
            MapSourceXToViewport(viewModel, endSourceX),
            MapSourceYToViewport(viewModel, endSourceY));
        viewModel.CompleteSpriteEditDrag();
    }

    private static double MapSourceXToViewport(InspectorPreviewImageViewModel viewModel, double sourceX)
    {
        return viewModel.FullImageX + (sourceX * (viewModel.FullImageWidth / viewModel.CanvasWidth));
    }

    private static double MapSourceYToViewport(InspectorPreviewImageViewModel viewModel, double sourceY)
    {
        return viewModel.FullImageY + (sourceY * (viewModel.FullImageHeight / viewModel.CanvasHeight));
    }

    private static void AssertEdge(bool shouldKeep, double expected, double actual)
    {
        if (shouldKeep)
        {
            Assert.Equal(expected, actual, precision: 3);
        }
        else
        {
            Assert.NotEqual(expected, actual, precision: 3);
        }
    }
}
