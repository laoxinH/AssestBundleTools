using System.Runtime.CompilerServices;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPanelViewModelTests
{
    [Fact]
    public void Apply_ShouldShowAvailablePathsAsSeparateRows()
    {
        var viewModel = CreateViewModel();

        ApplyBasicDetails(
            viewModel,
            containerPath: "assets/ui/button.png",
            serializedFileName: "CAB-test");

        var rows = viewModel.InformationCards.Single(static card => card.Key == "basic").Rows;

        Assert.DoesNotContain(rows, static row => row.Label == "Path");
        Assert.Contains(rows, static row => row.Label == "Container" && row.Value == "assets/ui/button.png");
        Assert.Contains(rows, static row => row.Label == "Serialized File" && row.Value == "CAB-test");
        Assert.Contains(rows, static row => row.Label == "PathId" && row.Value == "1");
    }

    [Fact]
    public void Apply_ShouldHideUnavailablePathRows()
    {
        var viewModel = CreateViewModel();

        ApplyBasicDetails(
            viewModel,
            containerPath: string.Empty,
            serializedFileName: string.Empty);

        var rows = viewModel.InformationCards.Single(static card => card.Key == "basic").Rows;

        Assert.DoesNotContain(rows, static row => row.Label == "Path");
        Assert.DoesNotContain(rows, static row => row.Label == "Container");
        Assert.DoesNotContain(rows, static row => row.Label == "Serialized File");
        Assert.Contains(rows, static row => row.Label == "PathId" && row.Value == "1");
    }

    [Fact]
    public void UpdatePreviewImageViewport_ShouldMakeOverlayVisible_WhenViewportArrivesAfterPreviewState()
    {
        var viewModel = CreateViewModel();
        ApplyBasicDetails(viewModel, string.Empty, "CAB-test");
        viewModel.ApplyImagePreview(
            CreateBitmapImageStub(),
            imagePixelWidth: 1000,
            imagePixelHeight: 500,
            spritePreviewOverlayDisplayState: new SpritePreviewOverlayDisplayState(
                CanvasWidth: 1000,
                CanvasHeight: 500,
                RegionX: 100,
                RegionY: 50,
                RegionWidth: 200,
                RegionHeight: 100,
                PivotX: null,
                PivotY: null,
                BorderLeft: null,
                BorderTop: null,
                BorderRight: null,
                BorderBottom: null,
                BadgeText: "crop"));

        Assert.Equal(Visibility.Collapsed, viewModel.Preview.Image.OverlayVisibility);

        viewModel.UpdatePreviewImageViewport(300d, 200d);

        Assert.Equal(Visibility.Visible, viewModel.Preview.Image.OverlayVisibility);
        Assert.Equal(30d, viewModel.Preview.Image.OverlayX, precision: 3);
        Assert.Equal(40d, viewModel.Preview.Image.OverlayY, precision: 3);
        Assert.Equal(60d, viewModel.Preview.Image.OverlayWidth, precision: 3);
        Assert.Equal(30d, viewModel.Preview.Image.OverlayHeight, precision: 3);
    }

    [Fact]
    public void UpdatePreviewImageViewport_ShouldProjectOverlayIntoViewportWithoutUpscalingStrokeSpace()
    {
        var projected = TryCalculateLayout(
            canvasWidth: 1000d,
            canvasHeight: 500d,
            regionX: 100d,
            regionY: 50d,
            regionWidth: 200d,
            regionHeight: 100d,
            viewportWidth: 300d,
            viewportHeight: 200d);

        Assert.NotNull(projected);
        Assert.Equal(30d, projected.Value.OverlayX, precision: 3);
        Assert.Equal(40d, projected.Value.OverlayY, precision: 3);
        Assert.Equal(60d, projected.Value.OverlayWidth, precision: 3);
        Assert.Equal(30d, projected.Value.OverlayHeight, precision: 3);
    }

    [Fact]
    public void UpdatePreviewImageViewport_ShouldKeepSmallImageOverlayAtNativeScale_WhenViewportIsLarger()
    {
        var projected = TryCalculateLayout(
            canvasWidth: 100d,
            canvasHeight: 50d,
            regionX: 10d,
            regionY: 8d,
            regionWidth: 20d,
            regionHeight: 10d,
            viewportWidth: 300d,
            viewportHeight: 200d);

        Assert.NotNull(projected);
        Assert.Equal(110d, projected.Value.OverlayX, precision: 3);
        Assert.Equal(83d, projected.Value.OverlayY, precision: 3);
        Assert.Equal(20d, projected.Value.OverlayWidth, precision: 3);
        Assert.Equal(10d, projected.Value.OverlayHeight, precision: 3);
    }

    private static PreviewImageOverlayLayout? TryCalculateLayout(
        double canvasWidth,
        double canvasHeight,
        double regionX,
        double regionY,
        double regionWidth,
        double regionHeight,
        double viewportWidth,
        double viewportHeight)
    {
        return PreviewImageOverlayLayoutCalculator.TryCalculate(
            canvasWidth,
            canvasHeight,
            regionX,
            regionY,
            regionWidth,
            regionHeight,
            viewportWidth,
            viewportHeight,
            out var layout)
            ? layout
            : null;
    }

    private static void ApplyBasicDetails(
        InspectorPanelViewModel viewModel,
        string containerPath,
        string serializedFileName)
    {
        viewModel.Apply(
            assetName: "Sprite",
            bundleDisplayName: "bundle",
            assetTypeText: "Sprite",
            containerPath: containerPath,
            serializedFileName: serializedFileName,
            sizeText: "1 KB",
            pathIdText: "1",
            extractCapabilityText: string.Empty,
            fileReplacementCapabilityText: string.Empty,
            workspaceReplacementCapabilityText: string.Empty,
            replacementSourceText: string.Empty,
            replacementStatusText: string.Empty,
            changeSummaryText: string.Empty,
            canExtract: false,
            canSelectFileReplacementSource: false,
            canSelectReplacementSource: false,
            canApplyReplacement: false,
            canUndoReplacement: false,
            assetDependencies: Array.Empty<AssetDependencyViewModel>(),
            referencedByDependencies: Array.Empty<AssetDependencyViewModel>(),
            dependencyClosureItems: Array.Empty<AssetDependencyViewModel>(),
            bundleExternalDependencies: Array.Empty<BundleExternalDependencyViewModel>());
    }

    private static BitmapImage CreateBitmapImageStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }

    private static InspectorPanelViewModel CreateViewModel()
    {
        return new InspectorPanelViewModel(
            new InspectorInformationTextSet(
                BasicInfoTitle: "Basic",
                PreviewInfoTitle: "Preview",
                PreviewDiagnosticTitle: "Diagnostic",
                AssetNameLabel: "Name",
                AssetTypeLabel: "Type",
                BundleLabel: "Bundle",
                ContainerPathLabel: "Container",
                SerializedFileLabel: "Serialized File",
                AssetSizeLabel: "Size",
                PathIdLabel: "PathId",
                PreviewCacheStatusLabel: "Cache",
                PreviewFallbackReasonLabel: "Fallback",
                PreviewDiagnosticDetailLabel: "Detail",
                CopyToolTipText: "Copy",
                ItemCountFormat: "{0}"));
    }
}
