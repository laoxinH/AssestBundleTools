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
    public async Task DragReplacement_ShouldPrepareAndApplyOrdinaryAssetThroughExistingFileChain()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var handler = CreateDragReplacementHandler(gateway);
        var target = CreateAssetEntry("asset-drop", "hero");
        var sourcePath = @"D:\Exports\hero.png";

        var candidate = await handler.PrepareAssetAsync(
            CreateWorkspaceSummary(),
            target,
            sourcePath,
            busy => gateway.BusyTransitions.Add($"busy:{busy.ToString().ToLowerInvariant()}"),
            _ => throw new Xunit.Sdk.XunitException("Candidate preparation should succeed."));
        var applied = await handler.ApplyAssetAsync(
            CreateWorkspaceSummary(),
            target,
            Assert.IsType<FileReplacementCandidateDto>(candidate),
            busy => gateway.BusyTransitions.Add($"busy:{busy.ToString().ToLowerInvariant()}"),
            _ => throw new Xunit.Sdk.XunitException("Replacement should succeed."));

        Assert.True(applied);
        Assert.Equal(sourcePath, Assert.Single(gateway.FileReplacementCandidateQueries).SourceFilePath);
        var command = Assert.Single(gateway.ReplaceAssetFromFileCommands);
        Assert.Equal(target.Item.AssetId, command.TargetAssetId);
        Assert.Equal(sourcePath, command.SourceFilePath);
        Assert.Equal(["busy:true", "busy:false", "busy:true", "busy:false"], gateway.BusyTransitions);
    }

    [Fact]
    public async Task DragReplacement_ShouldMatchAndApplySpineExportDirectoryAsRelatedBatch()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"abt-spine-drop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var sourcePath = Path.Combine(directoryPath, "hero.png");
            File.WriteAllText(sourcePath, "png");
            var gateway = new RecordingWorkspaceAssetOperationGateway();
            var handler = CreateDragReplacementHandler(gateway);
            var target = CreateTextureOnlySpineGroupEntry();

            var draft = handler.PrepareSpineGroup(target, directoryPath);
            var applied = await handler.ApplySpineGroupAsync(
                CreateWorkspaceSummary(),
                Assert.IsType<SpineAssetGroupEditDraftViewModel>(draft),
                busy => gateway.BusyTransitions.Add($"busy:{busy.ToString().ToLowerInvariant()}"),
                _ => throw new Xunit.Sdk.XunitException("Spine replacement should succeed."));

            Assert.True(applied);
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
