using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetPreviewDisplayFormatterTests
{
    [Fact]
    public void BuildSpritePreviewOverlayDisplayState_ShouldProjectOverlayGeometry_AndBadgeText()
    {
        var formatter = new AssetPreviewDisplayFormatter(new TestLocalizationService());
        var preview = new AssetPreviewDto(
            WorkspaceId: new WorkspaceId("workspace-1"),
            BundleId: new BundleId("bundle-1"),
            AssetId: new AssetId("asset-1"),
            Kind: AssetPreviewKind.Image,
            ContentType: "image/png",
            BinaryContent: [0x89, 0x50, 0x4E, 0x47],
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: true,
            GeneratedAtUtc: new DateTimeOffset(2026, 4, 29, 8, 0, 0, TimeSpan.Zero),
            Diagnostic: new AssetPreviewDiagnosticDto(
                CacheStatus: AssetPreviewCacheStatusKind.Hit,
                FallbackReason: AssetPreviewFallbackReasonKind.None),
            ImageOverlay: new AssetPreviewImageOverlayDto(
                CanvasPixelWidth: 256,
                CanvasPixelHeight: 128,
                HighlightRegion: new AssetPreviewImageRegionDto(
                    X: 24,
                    Y: 36,
                    Width: 80,
                    Height: 48)));

        var state = formatter.BuildSpritePreviewOverlayDisplayState(preview);

        Assert.NotNull(state);
        Assert.Equal(256d, state!.CanvasWidth);
        Assert.Equal(128d, state.CanvasHeight);
        Assert.Equal(24d, state.RegionX);
        Assert.Equal(36d, state.RegionY);
        Assert.Equal(80d, state.RegionWidth);
        Assert.Equal(48d, state.RegionHeight);
        Assert.Equal(
            ResourceKeys.InspectorSpritePreviewOverlayBadgeFormat + ":24|36|80|48",
            state.BadgeText);
    }
}
