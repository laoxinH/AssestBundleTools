using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed partial class BundleExplorerAssetExtractionActionHandlerTests
{
    [Fact]
    public async Task DragReplacement_ShouldPrepareAndApplyEveryMatchedOrdinaryAsset()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var handler = CreateDragReplacementHandler(gateway);
        var hero = CreateAssetEntry("asset-hero", "hero");
        var portrait = CreateAssetEntry("asset-portrait", "portrait");
        var sourcePaths = new[]
        {
            @"D:\Exports\hero.png",
            @"D:\Exports\portrait.png"
        };

        var plan = await handler.PrepareAssetsAsync(
            CreateWorkspaceSummary(),
            hero,
            [hero, portrait],
            [],
            sourcePaths,
            busy => gateway.BusyTransitions.Add($"busy:{busy.ToString().ToLowerInvariant()}"),
            _ => throw new Xunit.Sdk.XunitException("Source matching should succeed."));
        var applied = await handler.ApplyAssetsAsync(
            CreateWorkspaceSummary(),
            Assert.IsType<AssetReplacementSourceMatchPlanDto>(plan).Matches,
            busy => gateway.BusyTransitions.Add($"busy:{busy.ToString().ToLowerInvariant()}"),
            _ => throw new Xunit.Sdk.XunitException("Matched replacements should succeed."));

        Assert.True(applied);
        var matchQuery = Assert.Single(gateway.AssetReplacementSourceMatchQueries);
        Assert.Equal(sourcePaths, matchQuery.SourcePaths);
        Assert.Equal(hero.Item.AssetId, matchQuery.DirectDropTarget?.AssetId);
        Assert.Equal(2, gateway.ReplaceAssetFromFileCommands.Count);
        Assert.Equal(
            [hero.Item.AssetId, portrait.Item.AssetId],
            gateway.ReplaceAssetFromFileCommands.Select(static command => command.TargetAssetId));
        Assert.Equal(["busy:true", "busy:false", "busy:true", "busy:false"], gateway.BusyTransitions);
    }

    [Fact]
    public async Task DragReplacement_ShouldMatchAndApplyMultipleSpineFilesAsRelatedBatch()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"abt-spine-drop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var sourcePath = Path.Combine(directoryPath, "hero.png");
            var unrelatedPath = Path.Combine(directoryPath, "readme.txt");
            File.WriteAllText(sourcePath, "png");
            File.WriteAllText(unrelatedPath, "ignored");
            var gateway = new RecordingWorkspaceAssetOperationGateway();
            var handler = CreateDragReplacementHandler(gateway);
            var target = CreateTextureOnlySpineGroupEntry();

            var draft = await handler.PrepareSpineGroupAsync(
                CreateWorkspaceSummary(),
                target,
                [unrelatedPath, sourcePath],
                _ => throw new Xunit.Sdk.XunitException("Spine matching should succeed."));
            var applied = await handler.ApplySpineGroupAsync(
                CreateWorkspaceSummary(),
                Assert.IsType<SpineAssetGroupEditDraftViewModel>(draft),
                busy => gateway.BusyTransitions.Add($"busy:{busy.ToString().ToLowerInvariant()}"),
                _ => throw new Xunit.Sdk.XunitException("Spine replacement should succeed."));

            Assert.True(applied);
            Assert.Equal(2, Assert.Single(gateway.SpineReplacementSourceMatchQueries).SourcePaths.Count);
            var command = Assert.Single(gateway.ReplaceAssetFromFileCommands);
            Assert.Equal(sourcePath, command.SourceFilePath);
            Assert.NotNull(command.OperationContext);
            Assert.StartsWith("spine_batch_", command.OperationContext.BatchId ?? string.Empty);
            Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<WorkspaceOperationRelationDto>>(
                command.OperationContext.Relations));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static BundleExplorerDragReplacementActionHandler CreateDragReplacementHandler(
        RecordingWorkspaceAssetOperationGateway gateway)
    {
        var localization = new TestLocalizationService();
        return new BundleExplorerDragReplacementActionHandler(
            gateway,
            new TestAppLogService(),
            new TestAppNotificationService(),
            localization,
            new RecordingFilePickerService(),
            new WorkspaceOperationChangeNotifier());
    }

    private static SpineAssetGroupEntryViewModel CreateTextureOnlySpineGroupEntry()
    {
        var bundleId = new BundleId("bundle-ui");
        return new SpineAssetGroupEntryViewModel(
            new SpineAssetGroupDto(
                GroupId: "spine-hero",
                BundleId: bundleId,
                DisplayName: "hero",
                GroupKey: "characters/hero",
                SkeletonAssets: [],
                AtlasAssets: [],
                TexturePageAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.TexturePage,
                        bundleId,
                        new AssetId("hero-texture"),
                        "bundle.ab",
                        "hero.png",
                        "Assets/Spine/hero.png",
                        AssetType.Texture2D,
                        "Texture2D")
                ],
                Diagnostics: []),
            "Skeleton 0 / Atlas 0",
            "1 page",
            "Ready");
    }
}
