using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed partial class BundleExplorerSpineGroupDocumentCoordinatorTests
{
    private static TestContext CreateContext()
    {
        var localizationService = new TestLocalizationService();
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var bundleTab = new BundleDocumentTabViewModel(new BundleId("bundle-ui"), "ui.ab", "ui.ab", isPreview: false);
        documentWorkspace.BundleTabs.Add(bundleTab);
        documentWorkspace.SetActiveBundleTab(bundleTab);
        var hostCoordinator = new BundleExplorerAssetDocumentHostCoordinator(
            documentWorkspace,
            localizationService,
            static () => { },
            static () => { });

        return new TestContext(
            localizationService,
            documentWorkspace,
            bundleTab,
            hostCoordinator,
            new AssetPaneViewModel());
    }

    private static BundleExplorerSpineGroupDocumentCoordinator CreateCoordinator(
        TestContext context,
        ISpineAssetGroupDetailsLoader? loader = null)
    {
        var bundlePane = new BundleExplorerBundlePaneViewModel(context.LocalizationService);
        return new BundleExplorerSpineGroupDocumentCoordinator(
            context.DocumentWorkspace,
            context.PrimaryPane,
            context.HostCoordinator,
            new SpineAssetGroupInspectorPresenter(bundlePane, context.LocalizationService),
            loader ?? new TestSpineAssetGroupDetailsLoader(),
            static () => { },
            static () => { });
    }

    private static SpineAssetGroupEntryViewModel CreateSpineGroupEntry(
        string groupId,
        string displayName,
        DateTimeOffset? lastExtractedAtUtc = null)
    {
        return new SpineAssetGroupEntryViewModel(
            CreateSpineGroup(groupId, displayName, lastExtractedAtUtc),
            "Skeleton 1 / Atlas 1",
            "1 page",
            "Ready");
    }

    private static SpineAssetGroupDto CreateSpineGroup(
        string groupId,
        string displayName,
        DateTimeOffset? lastExtractedAtUtc = null)
    {
        var bundleId = new BundleId("bundle-ui");
        return new SpineAssetGroupDto(
            groupId,
            bundleId,
            displayName,
            $"characters/{groupId}",
            SkeletonAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Skeleton,
                    bundleId,
                    new AssetId($"{groupId}-json"),
                    "ui.ab",
                    $"{groupId}.json",
                    $"Assets/{groupId}.json",
                    AssetType.TextAsset,
                    "TextAsset",
                    lastExtractedAtUtc)
            ],
            AtlasAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Atlas,
                    bundleId,
                    new AssetId($"{groupId}-atlas"),
                    "ui.ab",
                    $"{groupId}.atlas",
                    $"Assets/{groupId}.atlas",
                    AssetType.TextAsset,
                    "TextAsset",
                    lastExtractedAtUtc)
            ],
            TexturePageAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.TexturePage,
                    bundleId,
                    new AssetId($"{groupId}-texture"),
                    "ui.ab",
                    $"{groupId}.png",
                    $"Assets/{groupId}.png",
                    AssetType.Texture2D,
                    "Texture2D",
                    lastExtractedAtUtc)
            ],
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }

    private static AssetPreviewDto CreateImagePreview(
        SpineAssetGroupPartDto texturePageAsset,
        byte[]? binaryContent = null)
    {
        return new AssetPreviewDto(
            WorkspaceId: new WorkspaceId("workspace"),
            texturePageAsset.BundleId,
            texturePageAsset.AssetId,
            AssetPreviewKind.Image,
            ContentType: "image/png",
            BinaryContent: binaryContent,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: null,
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UnixEpoch,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));
    }

    private static SpineSkeletonSummaryDto CreateJsonSkeletonSummary(
        SpineAssetGroupPartDto skeletonAsset) =>
        new(
            skeletonAsset,
            SpineSkeletonAssetFormat.Json,
            SpineVersion: "4.1",
            BoneCount: 2,
            SlotCount: 3,
            SkinCount: 1,
            AnimationCount: 4,
            EventCount: 1,
            IkConstraintCount: 0,
            TransformConstraintCount: 0,
            PathConstraintCount: 0);

    private static SpineAnimationPreviewDto CreateAnimationPreview(
        SpineAssetGroupPartDto skeletonAsset,
        string pageName) =>
        new(
            skeletonAsset,
            SpineVersion: "4.1",
            Bones:
            [
                new SpineAnimationBoneDto("root", ParentName: null, X: 0d, Y: 0d, Rotation: 0d, ScaleX: 1d, ScaleY: 1d)
            ],
            Slots:
            [
                new SpineAnimationSlotDto("body-slot", "root", "body", DrawOrder: 0)
            ],
            Attachments:
            [
                new SpineAnimationAttachmentDto(
                    "body-slot",
                    "body",
                    "body",
                    pageName,
                    RegionX: 0,
                    RegionY: 0,
                    RegionWidth: 64,
                    RegionHeight: 64,
                    IsRegionRotated: false,
                    X: 0d,
                    Y: 0d,
                    Width: 64d,
                    Height: 64d,
                    Rotation: 0d,
                    ScaleX: 1d,
                    ScaleY: 1d)
            ],
            Animations:
            [
                new SpineAnimationClipDto(
                    "idle",
                    Duration: 1d,
                    BoneTimelines:
                    [
                        new SpineAnimationBoneTimelineDto(
                            "root",
                            SpineAnimationBoneTimelineKind.Translate,
                            [
                                new SpineAnimationTimelineFrameDto(0d, X: 0d, Y: 0d),
                                new SpineAnimationTimelineFrameDto(1d, X: 1d, Y: 1d)
                            ])
                    ],
                    SlotAttachmentTimelines: [])
                {
                    SampledFrames =
                    [
                        new SpineAnimationFrameDto(
                            Time: 0d,
                            DrawCommands:
                            [
                                new SpineAnimationDrawCommandDto(
                                    "body-slot",
                                    "body",
                                    "body",
                                    pageName,
                                    SourceX: 0,
                                    SourceY: 0,
                                    SourceWidth: 64,
                                    SourceHeight: 64,
                                    Width: 64d,
                                    Height: 64d,
                                    TransformM11: 1d,
                                    TransformM12: 0d,
                                    TransformM21: 0d,
                                    TransformM22: 1d,
                                    TransformM31: 0d,
                                    TransformM32: 0d)
                            ])
                    ]
                }
            ],
            Runtime: new SpineAnimationRuntimeDto(
                "4.1",
                Major: 4,
                Minor: 1,
                AdapterKey: "V41",
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable));

    private static SpineAssetGroupTexturePreviewState CreateTexturePreviewState(
        SpineAtlasPageDto atlasPage,
        SpineAssetGroupPartDto texturePageAsset,
        byte[]? binaryContent = null)
    {
        return new SpineAssetGroupTexturePreviewState(
            atlasPage,
            CreateImagePreview(texturePageAsset, binaryContent),
            CreateImageSourceStub(),
            PixelWidth: atlasPage.Width ?? 1,
            PixelHeight: atlasPage.Height ?? 1);
    }

    private static ImageSource CreateImageSourceStub()
    {
        return (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
    }

    private sealed record TestContext(
        TestLocalizationService LocalizationService,
        BundleExplorerDocumentWorkspaceViewModel DocumentWorkspace,
        BundleDocumentTabViewModel BundleTab,
        BundleExplorerAssetDocumentHostCoordinator HostCoordinator,
        AssetPaneViewModel PrimaryPane);

    private sealed class TestSpineAssetGroupDetailsLoader : ISpineAssetGroupDetailsLoader
    {
        public int LoadCallCount { get; private set; }

        public int LoadTexturePreviewCallCount { get; private set; }

        public Func<SpineAssetGroupDocumentTabViewModel, Task<SpineAssetGroupDetailsLoadResult?>> LoadHandler { get; init; } =
            _ => Task.FromResult<SpineAssetGroupDetailsLoadResult?>(null);

        public Func<SpineAssetGroupDocumentTabViewModel, SpineAtlasPageOptionViewModel, Task<SpineAssetGroupTexturePreviewState?>> LoadTexturePreviewHandler { get; init; } =
            (_, _) => Task.FromResult<SpineAssetGroupTexturePreviewState?>(null);

        public Task<SpineAssetGroupDetailsLoadResult?> LoadAsync(
            SpineAssetGroupDocumentTabViewModel groupTab,
            CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return LoadHandler(groupTab);
        }

        public Task<SpineAssetGroupTexturePreviewState?> LoadTexturePreviewAsync(
            SpineAssetGroupDocumentTabViewModel groupTab,
            SpineAtlasPageOptionViewModel atlasPageOption,
            CancellationToken cancellationToken = default)
        {
            LoadTexturePreviewCallCount++;
            return LoadTexturePreviewHandler(groupTab, atlasPageOption);
        }
    }
}
