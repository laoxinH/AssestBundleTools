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
        Assert.Equal(352d, viewModel.FullImageWidth, precision: 3);
        Assert.Equal(176d, viewModel.FullImageHeight, precision: 3);
        Assert.Equal(24d, viewModel.FullImageX, precision: 3);
        Assert.Equal(112d, viewModel.FullImageY, precision: 3);
        Assert.Equal(1d, viewModel.Zoom);
        Assert.Equal(0d, viewModel.PanX);
        Assert.Equal(0d, viewModel.PanY);
        Assert.Equal(Visibility.Visible, viewModel.SpriteEditMeasurementVisibility);
    }

    [Fact]
    public void SpriteEditDrag_ShouldMoveCropBoxAndPivotTogether()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        DragFromSource(viewModel, 30d, 40d, 40d, 50d);

        Assert.Equal(20d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditOrigin_ShouldAllowPositiveValuesWhenCropMatchesCanvas()
    {
        var inputViewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(inputViewModel);
        inputViewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 0d,
            cropY: 0d,
            cropWidth: 100d,
            cropHeight: 100d,
            pivotX: 50d,
            pivotY: 50d,
            inputKind: SpriteEditGeometryInputKind.Pivot);

        inputViewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 5d,
            cropY: 7d,
            cropWidth: 100d,
            cropHeight: 100d,
            pivotX: 50d,
            pivotY: 50d,
            inputKind: SpriteEditGeometryInputKind.Crop);

        Assert.Equal(5d, inputViewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(7d, inputViewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(100d, inputViewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(100d, inputViewModel.SpriteEditCropHeight, precision: 3);

        var dragViewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(dragViewModel);
        dragViewModel.UpdateSpriteEditGeometryFromInput(
            0d,
            0d,
            100d,
            100d,
            50d,
            50d,
            SpriteEditGeometryInputKind.Pivot);
        DragFromSource(dragViewModel, 20d, 20d, 30d, 40d);

        Assert.Equal(10d, dragViewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(20d, dragViewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(100d, dragViewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(100d, dragViewModel.SpriteEditCropHeight, precision: 3);
    }

    [Fact]
    public void SpriteEditResize_ShouldKeepOppositeEdgeOrCornerAnchoredWhenCropExtendsPastCanvas()
    {
        var leftEdgeViewModel = CreateExtendedSpriteEditRegion();
        DragFromSource(leftEdgeViewModel, 5d, 50d, 0d, 50d);

        Assert.Equal(0d, leftEdgeViewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(7d, leftEdgeViewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(105d, leftEdgeViewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(100d, leftEdgeViewModel.SpriteEditCropHeight, precision: 3);

        var topEdgeViewModel = CreateExtendedSpriteEditRegion();
        DragFromSource(topEdgeViewModel, 50d, 7d, 50d, 0d);

        Assert.Equal(5d, topEdgeViewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(0d, topEdgeViewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(100d, topEdgeViewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(107d, topEdgeViewModel.SpriteEditCropHeight, precision: 3);

        var topLeftViewModel = CreateExtendedSpriteEditRegion();
        DragFromSource(topLeftViewModel, 5d, 7d, 0d, 0d);

        Assert.Equal(0d, topLeftViewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(0d, topLeftViewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(105d, topLeftViewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(107d, topLeftViewModel.SpriteEditCropHeight, precision: 3);

        var rightEdgeViewModel = CreateExtendedSpriteEditRegion();
        DragFromSource(rightEdgeViewModel, 105d, 50d, 110d, 50d);

        Assert.Equal(5d, rightEdgeViewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(7d, rightEdgeViewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(105d, rightEdgeViewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(100d, rightEdgeViewModel.SpriteEditCropHeight, precision: 3);
    }

    [Theory]
    [InlineData(10d, 40d, 15d, 40d, 15d, 20d, 25d, 40d)]
    [InlineData(40d, 40d, 35d, 40d, 10d, 20d, 25d, 40d)]
    [InlineData(25d, 20d, 25d, 25d, 10d, 25d, 30d, 35d)]
    [InlineData(25d, 60d, 25d, 55d, 10d, 20d, 30d, 35d)]
    [InlineData(10d, 20d, 15d, 25d, 15d, 25d, 25d, 35d)]
    [InlineData(40d, 20d, 35d, 25d, 10d, 25d, 25d, 35d)]
    [InlineData(10d, 60d, 15d, 55d, 15d, 20d, 25d, 35d)]
    [InlineData(40d, 60d, 35d, 55d, 10d, 20d, 25d, 35d)]
    public void SpriteEditDrag_ShouldResizeCropBoxFromEdgesAndCorners(
        double startSourceX,
        double startSourceY,
        double endSourceX,
        double endSourceY,
        double expectedCropX,
        double expectedCropY,
        double expectedCropWidth,
        double expectedCropHeight)
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        DragFromSource(viewModel, startSourceX, startSourceY, endSourceX, endSourceY);

        Assert.Equal(expectedCropX, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(expectedCropY, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(expectedCropWidth, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(expectedCropHeight, viewModel.SpriteEditCropHeight, precision: 3);
    }

    [Fact]
    public void SpriteEditDrag_ShouldKeepPivotRelativeWhenResizeCrossesPreviousPivot()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        DragFromSource(viewModel, 10d, 20d, 35d, 45d);

        Assert.Equal(35d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(45d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(5d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(15d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(36.667d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(48.75d, viewModel.SpriteEditPivotY, precision: 3);
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
            pivotY: 45d,
            inputKind: SpriteEditGeometryInputKind.Pivot);

        Assert.Equal(20d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(10d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(50d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(35d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(45d, viewModel.SpriteEditPivotY, precision: 3);
        Assert.Equal(MapSourceXToViewport(viewModel, 20d), viewModel.OverlayX, precision: 3);
        Assert.Equal(MapSourceYToViewport(viewModel, 10d), viewModel.OverlayY, precision: 3);
        Assert.Equal(MapSourceWidthToViewport(viewModel, 40d), viewModel.OverlayWidth, precision: 3);
        Assert.Equal(MapSourceHeightToViewport(viewModel, 50d), viewModel.OverlayHeight, precision: 3);
        Assert.Equal(MapSourceXToViewport(viewModel, 35d), viewModel.PivotX, precision: 3);
        Assert.Equal(MapSourceYToViewport(viewModel, 45d), viewModel.PivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditGeometryInput_ShouldClampPivotInsideCropBox()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 40d,
            cropY: 50d,
            cropWidth: 10d,
            cropHeight: 10d,
            pivotX: 20d,
            pivotY: 30d,
            inputKind: SpriteEditGeometryInputKind.Pivot);

        Assert.Equal(40d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(50d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(10d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(10d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(50d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditGeometryInput_ShouldKeepPivotRelativeWhenCropChanges()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 20d,
            cropY: 30d,
            cropWidth: 60d,
            cropHeight: 80d,
            pivotX: 20d,
            pivotY: 30d,
            inputKind: SpriteEditGeometryInputKind.Crop);

        Assert.Equal(20d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(60d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(80d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(50d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditGeometryInput_ShouldRefreshPivotRelativePositionForNextCropInput()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 10d,
            cropY: 20d,
            cropWidth: 30d,
            cropHeight: 40d,
            pivotX: 35d,
            pivotY: 50d,
            inputKind: SpriteEditGeometryInputKind.Pivot);
        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 20d,
            cropY: 30d,
            cropWidth: 60d,
            cropHeight: 80d,
            pivotX: 35d,
            pivotY: 50d,
            inputKind: SpriteEditGeometryInputKind.Crop);

        Assert.Equal(20d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(60d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(80d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(70d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(90d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditResetOriginal_ShouldRestoreInitialCropAndPivot()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);
        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 20d,
            cropY: 30d,
            cropWidth: 60d,
            cropHeight: 80d,
            pivotX: 70d,
            pivotY: 90d,
            inputKind: SpriteEditGeometryInputKind.Pivot);

        viewModel.ResetSpriteEditGeometryToOriginal();

        Assert.Equal(10d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(20d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(40d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(20d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(30d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditFullSize_ShouldUseWholeCanvasAndKeepCurrentPivotRatio()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);
        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 10d,
            cropY: 20d,
            cropWidth: 30d,
            cropHeight: 40d,
            pivotX: 35d,
            pivotY: 50d,
            inputKind: SpriteEditGeometryInputKind.Pivot);

        viewModel.UseFullImageForSpriteEditGeometry();

        Assert.Equal(0d, viewModel.SpriteEditCropX, precision: 3);
        Assert.Equal(0d, viewModel.SpriteEditCropY, precision: 3);
        Assert.Equal(100d, viewModel.SpriteEditCropWidth, precision: 3);
        Assert.Equal(100d, viewModel.SpriteEditCropHeight, precision: 3);
        Assert.Equal(83.333d, viewModel.SpriteEditPivotX, precision: 3);
        Assert.Equal(75d, viewModel.SpriteEditPivotY, precision: 3);
    }

    [Fact]
    public void SpriteEditMeasurements_ShouldExposeCoordinatesLengthsAndAxis()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);

        Assert.Equal(Visibility.Visible, viewModel.SpriteEditMeasurementVisibility);
        Assert.Equal("(10, 20)", viewModel.SpriteEditTopLeftCoordinateText);
        Assert.Equal("(40, 20)", viewModel.SpriteEditTopRightCoordinateText);
        Assert.Equal("(10, 60)", viewModel.SpriteEditBottomLeftCoordinateText);
        Assert.Equal("(40, 60)", viewModel.SpriteEditBottomRightCoordinateText);
        Assert.Equal("30", viewModel.SpriteEditTopLengthText);
        Assert.Equal("30", viewModel.SpriteEditBottomLengthText);
        Assert.Equal("40", viewModel.SpriteEditLeftLengthText);
        Assert.Equal("40", viewModel.SpriteEditRightLengthText);
        Assert.True(viewModel.FullImageX > 0d);
        Assert.True(viewModel.FullImageY > 0d);
        Assert.Equal(viewModel.FullImageX, viewModel.SpriteEditAxisOriginX, precision: 3);
        Assert.Equal(viewModel.FullImageY, viewModel.SpriteEditAxisOriginY, precision: 3);
        Assert.True(viewModel.SpriteEditAxisXEndX > viewModel.SpriteEditAxisOriginX);
        Assert.True(viewModel.SpriteEditAxisYEndY > viewModel.SpriteEditAxisOriginY);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
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

    private static InspectorPreviewImageViewModel CreateExtendedSpriteEditRegion()
    {
        var viewModel = new InspectorPreviewImageViewModel();
        ApplySpriteEditPreview(viewModel);
        viewModel.UpdateSpriteEditGeometryFromInput(
            cropX: 5d,
            cropY: 7d,
            cropWidth: 100d,
            cropHeight: 100d,
            pivotX: 50d,
            pivotY: 50d,
            inputKind: SpriteEditGeometryInputKind.Pivot);
        return viewModel;
    }

    private static void DragFromSource(
        InspectorPreviewImageViewModel viewModel,
        double startSourceX,
        double startSourceY,
        double endSourceX,
        double endSourceY)
    {
        var startViewportX = MapSourceXToViewport(viewModel, startSourceX);
        var startViewportY = MapSourceYToViewport(viewModel, startSourceY);
        var endViewportX = MapSourceXToViewport(viewModel, endSourceX);
        var endViewportY = MapSourceYToViewport(viewModel, endSourceY);

        Assert.True(viewModel.TryBeginSpriteEditDrag(startViewportX, startViewportY));
        viewModel.UpdateSpriteEditDrag(endViewportX, endViewportY);
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

    private static double MapSourceWidthToViewport(InspectorPreviewImageViewModel viewModel, double sourceWidth)
    {
        return sourceWidth * (viewModel.FullImageWidth / viewModel.CanvasWidth);
    }

    private static double MapSourceHeightToViewport(InspectorPreviewImageViewModel viewModel, double sourceHeight)
    {
        return sourceHeight * (viewModel.FullImageHeight / viewModel.CanvasHeight);
    }
}
