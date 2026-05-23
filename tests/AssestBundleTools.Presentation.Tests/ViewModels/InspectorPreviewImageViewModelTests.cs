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

    [Fact]
    public void ZoomAndPan_ShouldRaiseVisualLayoutPropertyChanges()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);
        viewModel.UpdateViewport(100d, 50d);
        changedProperties.Clear();

        viewModel.ZoomBy(2d);
        viewModel.PanBy(10d, 5d);

        Assert.Contains(nameof(InspectorPreviewImageViewModel.FullImageX), changedProperties);
        Assert.Contains(nameof(InspectorPreviewImageViewModel.FullImageY), changedProperties);
        Assert.Contains(nameof(InspectorPreviewImageViewModel.FullImageWidth), changedProperties);
        Assert.Contains(nameof(InspectorPreviewImageViewModel.FullImageHeight), changedProperties);
    }

    [Fact]
    public void UpdateViewport_ShouldIgnoreCollapsedViewportAfterValidViewport()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: null);
        viewModel.UpdateViewport(200d, 100d);
        viewModel.ZoomBy(2d);

        viewModel.UpdateViewport(0d, 0d);

        Assert.Equal(200d, viewModel.FullImageWidth, precision: 3);
        Assert.Equal(100d, viewModel.FullImageHeight, precision: 3);
    }

    [Fact]
    public void RegionOverlay_ShouldProjectMultipleRegionsIntoCurrentViewport()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: null,
            focusPreviewDisplayState: null,
            regionOverlayDisplayState: new ImageRegionOverlayDisplayState(
                CanvasWidth: 100,
                CanvasHeight: 50,
                Regions:
                [
                    new ImageRegionOverlayItemDisplayState(4, 5, 20, 10, "head"),
                    new ImageRegionOverlayItemDisplayState(40, 10, 30, 15, "body")
                ],
                BadgeText: "2 regions"));

        viewModel.UpdateViewport(200d, 100d);

        Assert.Equal(2, viewModel.RegionOverlays.Count);
        Assert.Equal(54d, viewModel.RegionOverlays[0].X, precision: 3);
        Assert.Equal(30d, viewModel.RegionOverlays[0].Y, precision: 3);
        Assert.Equal(20d, viewModel.RegionOverlays[0].Width, precision: 3);
        Assert.Equal(Visibility.Visible, viewModel.RegionOverlayBadgeVisibility);
    }

    [Fact]
    public void RequestSpriteEditMode_ShouldLockImageToViewportFit_AndHideViewportTools()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        viewModel.Apply(
            CreateImageSourceStub(),
            imagePixelWidth: 100,
            imagePixelHeight: 50,
            overlayDisplayState: new SpritePreviewOverlayDisplayState(
                CanvasWidth: 100,
                CanvasHeight: 50,
                RegionX: 10,
                RegionY: 8,
                RegionWidth: 20,
                RegionHeight: 12,
                PivotX: 20,
                PivotY: 14,
                BorderLeft: null,
                BorderTop: null,
                BorderRight: null,
                BorderBottom: null,
                BadgeText: "sprite"),
            focusPreviewDisplayState: null,
            infoDisplayState: new ImagePreviewInfoDisplayState(
                "Size 100 x 50 px",
                "Encoding RGBA32",
                "Format ID 47"));
        viewModel.UpdateViewport(400d, 400d);

        viewModel.RequestSpriteEditMode();
        viewModel.ZoomBy(2d);
        viewModel.PanBy(30d, 20d);

        Assert.True(viewModel.IsSpriteEditModeActive);
        Assert.Equal(Visibility.Collapsed, viewModel.ImageViewportControlsVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.SpriteDisplayModeControlVisibility);
        Assert.Equal(Visibility.Visible, viewModel.SpriteEditHandleVisibility);
        Assert.Equal(Visibility.Visible, viewModel.SpriteEditGeometryPanelVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.InfoBadgeVisibility);
        Assert.Equal(400d, viewModel.FullImageWidth, precision: 3);
        Assert.Equal(200d, viewModel.FullImageHeight, precision: 3);
        Assert.Equal(0d, viewModel.FullImageX, precision: 3);
        Assert.Equal(100d, viewModel.FullImageY, precision: 3);
        Assert.Equal(1d, viewModel.Zoom);
        Assert.Equal(0d, viewModel.PanX);
        Assert.Equal(0d, viewModel.PanY);
    }

    [Fact]
    public void SpriteEditDrag_ShouldMoveCropBoxAndPivotTogether()
    {
        var viewModel = new InspectorPreviewImageViewModel();
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

        Assert.True(viewModel.TryBeginSpriteEditDrag(60d, 100d));
        viewModel.UpdateSpriteEditDrag(80d, 120d);
        viewModel.CompleteSpriteEditDrag();

        Assert.Equal(40d, viewModel.OverlayX, precision: 3);
        Assert.Equal(60d, viewModel.OverlayY, precision: 3);
        Assert.Equal(60d, viewModel.OverlayWidth, precision: 3);
        Assert.Equal(80d, viewModel.OverlayHeight, precision: 3);
        Assert.Equal(60d, viewModel.PivotX, precision: 3);
        Assert.Equal(80d, viewModel.PivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditGeometryInput_ShouldUpdateCropBoxAndPivot()
    {
        var viewModel = new InspectorPreviewImageViewModel();
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

        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 20d,
            cropY: 10d,
            cropWidth: 40d,
            cropHeight: 50d,
            pivotX: 35d,
            pivotY: 45d);

        Assert.Equal(20d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(10d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(50d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(35d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(45d, viewModel.SpriteEditPivotY, precision: 3);
        Assert.Equal(40d, viewModel.OverlayX, precision: 3);
        Assert.Equal(20d, viewModel.OverlayY, precision: 3);
        Assert.Equal(80d, viewModel.OverlayWidth, precision: 3);
        Assert.Equal(100d, viewModel.OverlayHeight, precision: 3);
        Assert.Equal(70d, viewModel.PivotX, precision: 3);
        Assert.Equal(90d, viewModel.PivotY, precision: 3);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }
}
