using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Replacement;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Replacement;

internal static class ReplacementWorkflowHandlerTests
{
    public static async Task GetFileReplacementCandidateHandler_ShouldPreferTargetAssetType_WhenSharedExtensionMatches()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Sprite,
            assetName: "HeroSprite"));
        var handler = new GetFileReplacementCandidateHandler(registry);

        var result = await handler.HandleAsync(new GetFileReplacementCandidateQuery(
            workspaceId,
            targetBundleId,
            targetAssetId,
            "  C:\\Imports\\hero.png  "));

        TestAssert.True(result.IsSuccess, "Shared image extension should still resolve to the target asset type.");
        var candidate = result.Value ?? throw new InvalidOperationException("Expected replacement candidate.");
        TestAssert.Equal("C:\\Imports\\hero.png", candidate.SourceFilePath, "Source file path should be trimmed.");
        TestAssert.Equal("hero.png", candidate.SourceFileName, "Candidate should expose the source file name.");
        TestAssert.Equal(AssetType.Sprite, candidate.SourceAssetType, "Shared image extensions should prefer the target asset type.");
    }

    public static async Task GetFileReplacementCandidateHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            new BundleId("bundle.target"),
            new AssetId("asset.existing"),
            assetType: AssetType.Texture2D,
            assetName: "ExistingTexture"));
        var handler = new GetFileReplacementCandidateHandler(registry);

        var result = await handler.HandleAsync(new GetFileReplacementCandidateQuery(
            workspaceId,
            new BundleId("bundle.target"),
            new AssetId("asset.missing"),
            "C:\\Imports\\hero.png"));

        TestAssert.True(result.IsFailure, "Missing target asset should fail candidate lookup.");
        TestAssert.Equal(
            "Asset.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing asset error.");
    }

    public static async Task GetFileReplacementCandidateHandler_ShouldReturnFailure_WhenSourceExtensionIsIncompatible()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.TextAsset,
            assetName: "ConfigText"));
        var handler = new GetFileReplacementCandidateHandler(registry);

        var result = await handler.HandleAsync(new GetFileReplacementCandidateQuery(
            workspaceId,
            targetBundleId,
            targetAssetId,
            "C:\\Imports\\hero.png"));

        TestAssert.True(result.IsFailure, "Incompatible source file type should fail validation.");
        TestAssert.Equal(
            "Asset.Replace.TypeMismatch",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the replacement type mismatch error.");
    }

    public static async Task GetWorkspaceReplacementCandidatesHandler_ShouldReturnFailure_WhenRegistryProvidesNoCandidates()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Texture2D,
            assetName: "HeroTexture"));
        registry.WorkspaceReplacementCandidates = Array.Empty<WorkspaceReplacementCandidateDto>();
        var handler = new GetWorkspaceReplacementCandidatesHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceReplacementCandidatesQuery(
            workspaceId,
            targetBundleId,
            targetAssetId));

        TestAssert.True(result.IsFailure, "Missing compatible workspace candidates should fail.");
        TestAssert.Equal(
            "Asset.Replace.NoCompatibleSource",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the compatible replacement source error.");
    }

    public static async Task GetWorkspaceReplacementCandidatesHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            new BundleId("bundle.target"),
            new AssetId("asset.existing"),
            assetType: AssetType.Texture2D,
            assetName: "ExistingTexture"));
        var handler = new GetWorkspaceReplacementCandidatesHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceReplacementCandidatesQuery(
            workspaceId,
            new BundleId("bundle.target"),
            new AssetId("asset.missing")));

        TestAssert.True(result.IsFailure, "Missing target asset should fail candidate lookup.");
        TestAssert.Equal(
            "Asset.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing asset error.");
    }

    public static async Task ReplaceAssetFromFileHandler_ShouldApplyReplacement_WhenTargetTypeMatches()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.TextAsset,
            assetName: "DialogueText"));
        registry.TryReplaceAssetFromFileResult = new AssetFileReplacementResultDto(
            WorkspaceId: workspaceId,
            TargetBundleId: targetBundleId,
            TargetAssetId: targetAssetId,
            TargetAssetName: "DialogueText",
            SourceFilePath: "C:\\Imports\\dialogue.txt",
            SourceFileName: "dialogue.txt",
            SourceAssetType: AssetType.TextAsset,
            ReplacedAtUtc: new DateTimeOffset(2026, 4, 28, 15, 30, 0, TimeSpan.Zero));
        var handler = new ReplaceAssetFromFileHandler(registry);

        var result = await handler.HandleAsync(new ReplaceAssetFromFileCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            "C:\\Imports\\dialogue.txt",
            AssetType.TextAsset));

        TestAssert.True(result.IsSuccess, "Matching file replacement should succeed.");
        var replacement = result.Value ?? throw new InvalidOperationException("Expected replacement result.");
        TestAssert.Equal("DialogueText", replacement.TargetAssetName, "Replacement result should round-trip target asset name.");
        TestAssert.Equal("dialogue.txt", replacement.SourceFileName, "Replacement result should expose source file name.");
        TestAssert.Equal(1, registry.TryReplaceAssetFromFileCallCount, "Registry file replacement should be invoked once.");
        TestAssert.Equal("C:\\Imports\\dialogue.txt", registry.LastFileReplacementSourceFilePath, "Registry should receive the original source file path.");
        TestAssert.Equal(AssetType.TextAsset, registry.LastFileReplacementSourceAssetType, "Registry should receive the validated source asset type.");
    }

    public static async Task ReplaceAssetFromFileHandler_ShouldReturnFailure_WhenSourceFilePathIsBlank()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.TextAsset,
            assetName: "DialogueText"));
        var handler = new ReplaceAssetFromFileHandler(registry);

        var result = await handler.HandleAsync(new ReplaceAssetFromFileCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            " ",
            AssetType.TextAsset));

        TestAssert.True(result.IsFailure, "Blank source file path should fail validation.");
        TestAssert.Equal(
            "Asset.Replace.SourceFilePathRequired",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing source file path error.");
        TestAssert.Equal(0, registry.TryReplaceAssetFromFileCallCount, "Validation failure should not invoke the registry.");
    }

    public static async Task ReplaceAssetFromFileHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry();
        var handler = new ReplaceAssetFromFileHandler(registry);

        var result = await handler.HandleAsync(new ReplaceAssetFromFileCommand(
            workspaceId,
            new BundleId("bundle.target"),
            new AssetId("asset.missing"),
            "C:\\Imports\\dialogue.txt",
            AssetType.TextAsset));

        TestAssert.True(result.IsFailure, "Missing target asset should fail replacement.");
        TestAssert.Equal(
            "Asset.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing target asset error.");
        TestAssert.Equal(0, registry.TryReplaceAssetFromFileCallCount, "Missing asset should not invoke the registry.");
    }

    public static async Task ReplaceAssetFromWorkspaceHandler_ShouldApplyReplacement_WhenSourceAndTargetTypesMatch()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var sourceBundleId = new BundleId("bundle.source");
        var sourceAssetId = new AssetId("asset.source");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Texture2D,
            assetName: "HeroTexture"));
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            sourceBundleId,
            sourceAssetId,
            assetType: AssetType.Texture2D,
            assetName: "EnemyTexture"));
        registry.TryReplaceAssetFromWorkspaceResult = new AssetReplacementResultDto(
            WorkspaceId: workspaceId,
            TargetBundleId: targetBundleId,
            TargetAssetId: targetAssetId,
            TargetAssetName: "HeroTexture",
            SourceBundleId: sourceBundleId,
            SourceAssetId: sourceAssetId,
            SourceBundleDisplayName: "Characters",
            SourceAssetName: "EnemyTexture",
            SourceAssetType: AssetType.Texture2D,
            ReplacedAtUtc: new DateTimeOffset(2026, 4, 28, 15, 45, 0, TimeSpan.Zero));
        var handler = new ReplaceAssetFromWorkspaceHandler(registry);

        var result = await handler.HandleAsync(new ReplaceAssetFromWorkspaceCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            sourceBundleId,
            sourceAssetId));

        TestAssert.True(result.IsSuccess, "Matching workspace replacement should succeed.");
        var replacement = result.Value ?? throw new InvalidOperationException("Expected replacement result.");
        TestAssert.Equal("EnemyTexture", replacement.SourceAssetName, "Replacement result should expose source asset name.");
        TestAssert.Equal(1, registry.TryReplaceAssetFromWorkspaceCallCount, "Registry workspace replacement should be invoked once.");
        TestAssert.Equal(sourceBundleId, registry.LastWorkspaceReplacementSourceBundleId, "Registry should receive source bundle id.");
        TestAssert.Equal(sourceAssetId, registry.LastWorkspaceReplacementSourceAssetId, "Registry should receive source asset id.");
    }

    public static async Task ReplaceAssetFromWorkspaceHandler_ShouldReturnFailure_WhenSourceAssetDoesNotExist()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Texture2D,
            assetName: "HeroTexture"));
        var handler = new ReplaceAssetFromWorkspaceHandler(registry);

        var result = await handler.HandleAsync(new ReplaceAssetFromWorkspaceCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            new BundleId("bundle.source"),
            new AssetId("asset.missing")));

        TestAssert.True(result.IsFailure, "Missing source asset should fail replacement.");
        TestAssert.Equal(
            "Asset.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing source asset error.");
        TestAssert.Equal(0, registry.TryReplaceAssetFromWorkspaceCallCount, "Missing source asset should not invoke the registry.");
    }

    public static async Task ReplaceAssetFromWorkspaceHandler_ShouldReturnFailure_WhenSourceAndTargetTypesDoNotMatch()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var sourceBundleId = new BundleId("bundle.source");
        var sourceAssetId = new AssetId("asset.source");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Texture2D,
            assetName: "HeroTexture"));
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            sourceBundleId,
            sourceAssetId,
            assetType: AssetType.TextAsset,
            assetName: "DialogueText"));
        var handler = new ReplaceAssetFromWorkspaceHandler(registry);

        var result = await handler.HandleAsync(new ReplaceAssetFromWorkspaceCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            sourceBundleId,
            sourceAssetId));

        TestAssert.True(result.IsFailure, "Type mismatch should fail replacement.");
        TestAssert.Equal(
            "Asset.Replace.TypeMismatch",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the replacement type mismatch error.");
        TestAssert.Equal(0, registry.TryReplaceAssetFromWorkspaceCallCount, "Type mismatch should not invoke the registry.");
    }

    public static async Task UndoAssetReplacementHandler_ShouldReturnFailure_WhenNoAppliedReplacementExists()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Mesh,
            assetName: "ArenaMesh"));
        registry.TryUndoAssetReplacementReturnValue = false;
        var handler = new UndoAssetReplacementHandler(registry);

        var result = await handler.HandleAsync(new UndoAssetReplacementCommand(
            workspaceId,
            targetBundleId,
            targetAssetId));

        TestAssert.True(result.IsFailure, "Undo should fail when no applied replacement exists.");
        TestAssert.Equal(
            "Asset.Replace.NoAppliedChange",
            TestAssert.Single(result.Errors).Code,
            "Failure should report that no applied replacement exists.");
        TestAssert.Equal(1, registry.TryUndoAssetReplacementCallCount, "Undo attempt should still hit the registry once.");
    }

    public static async Task UndoAssetReplacementHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry();
        var handler = new UndoAssetReplacementHandler(registry);

        var result = await handler.HandleAsync(new UndoAssetReplacementCommand(
            workspaceId,
            new BundleId("bundle.target"),
            new AssetId("asset.missing")));

        TestAssert.True(result.IsFailure, "Missing target asset should fail undo.");
        TestAssert.Equal(
            "Asset.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing asset error.");
        TestAssert.Equal(0, registry.TryUndoAssetReplacementCallCount, "Missing asset should not invoke the registry.");
    }

    public static async Task RedoAssetReplacementHandler_ShouldApplyRedo_WhenRedoSnapshotExists()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Texture2D,
            assetName: "HeroTexture"));
        registry.TryRedoAssetReplacementResult = new AssetReplacementRedoResultDto(
            WorkspaceId: workspaceId,
            TargetBundleId: targetBundleId,
            TargetAssetId: targetAssetId,
            TargetAssetName: "HeroTexture",
            RedoneAtUtc: new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero));
        var handler = new RedoAssetReplacementHandler(registry);

        var result = await handler.HandleAsync(new RedoAssetReplacementCommand(
            workspaceId,
            targetBundleId,
            targetAssetId));

        TestAssert.True(result.IsSuccess, "Redo should succeed when the registry exposes a redo snapshot.");
        var redo = result.Value ?? throw new InvalidOperationException("Expected redo result.");
        TestAssert.Equal("HeroTexture", redo.TargetAssetName, "Redo result should expose target asset name.");
        TestAssert.Equal(1, registry.TryRedoAssetReplacementCallCount, "Redo should invoke the redo-capable registry once.");
    }

    public static async Task RedoAssetReplacementHandler_ShouldReturnFailure_WhenNoRedoSnapshotExists()
    {
        var workspaceId = CreateWorkspaceId();
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            assetType: AssetType.Texture2D,
            assetName: "HeroTexture"));
        registry.TryRedoAssetReplacementReturnValue = false;
        var handler = new RedoAssetReplacementHandler(registry);

        var result = await handler.HandleAsync(new RedoAssetReplacementCommand(
            workspaceId,
            targetBundleId,
            targetAssetId));

        TestAssert.True(result.IsFailure, "Redo should fail when no redo snapshot exists.");
        TestAssert.Equal(
            "Asset.Replace.NoRedoChange",
            TestAssert.Single(result.Errors).Code,
            "Failure should report that no redo replacement exists.");
        TestAssert.Equal(1, registry.TryRedoAssetReplacementCallCount, "Redo attempt should still hit the registry once.");
    }

    public static async Task GetWorkspaceBatchReplacementPreviewHandler_ShouldGroupCandidates_ByBundleAndSourceFilter()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-b"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Zeta",
                        assetName: "EnemyPortrait",
                        workspaceReplacementSourceAssetName: "SourcePortrait"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-a"),
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "Alpha",
                        assetName: "ConfigText",
                        fileReplacementSourceFileName: "config.json"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-a"),
                        assetId: new AssetId("asset-3"),
                        bundleDisplayName: "Alpha",
                        assetName: "ThemeMusic",
                        workspaceReplacementSourceAssetName: "SourceTheme",
                        fileReplacementSourceFileName: "theme.ogg")
                ])
        };
        var handler = new GetWorkspaceBatchReplacementPreviewHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPreviewQuery(
            workspaceId,
            BatchReplacementSourceKind.Workspace));

        TestAssert.True(result.IsSuccess, "Workspace replacement preview should succeed.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(2, preview.CandidateAssetCount, "Workspace filter should keep only items with workspace replacement sources.");
        TestAssert.Equal(2, preview.CandidateBundleCount, "Workspace filter should keep two bundles.");
        TestAssert.Equal(2, preview.WorkspaceReplacementCount, "Workspace replacement count should reflect filtered items.");
        TestAssert.Equal(1, preview.FileReplacementCount, "Filtered items should still expose file replacement overlap count.");
        TestAssert.Equal(2, preview.BundlePlans.Count, "Preview should group filtered items by bundle.");

        var firstBundlePlan = preview.BundlePlans[0];
        TestAssert.Equal("Alpha", firstBundlePlan.BundleDisplayName, "Bundle plans should be ordered by bundle display name.");
        TestAssert.Equal(1, firstBundlePlan.AssetCount, "Alpha bundle should keep the overlapping workspace replacement item only.");
        TestAssert.Equal(1, firstBundlePlan.WorkspaceReplacementCount, "Alpha bundle should expose one workspace replacement.");
        TestAssert.Equal(1, firstBundlePlan.FileReplacementCount, "Alpha bundle should expose one overlapping file replacement.");
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldReturnFailure_WhenNoCandidatesMatchFilter()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-a"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha",
                        assetName: "HeroTexture",
                        workspaceReplacementSourceAssetName: "SourceTexture")
                ])
        };
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry);

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            BatchReplacementSourceKind.File));

        TestAssert.True(result.IsFailure, "Batch replacement should fail when no candidates match the source filter.");
        TestAssert.Equal(
            "Asset.Replace.NoBatchCandidates",
            TestAssert.Single(result.Errors).Code,
            "Failure should report that no batch candidates exist.");
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldSummarizeProcessedCounts_WhenCandidatesExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-b"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Zeta",
                        assetName: "EnemyPortrait",
                        workspaceReplacementSourceAssetName: "SourcePortrait"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-a"),
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "Alpha",
                        assetName: "ConfigText",
                        fileReplacementSourceFileName: "config.json"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-a"),
                        assetId: new AssetId("asset-3"),
                        bundleDisplayName: "Alpha",
                        assetName: "ThemeMusic",
                        workspaceReplacementSourceAssetName: "SourceTheme",
                        fileReplacementSourceFileName: "theme.ogg")
                ])
        };
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry);

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            BatchReplacementSourceKind.All));

        TestAssert.True(result.IsSuccess, "Batch replacement summary should succeed when candidates exist.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected batch replacement result.");
        TestAssert.Equal(3, execution.ProcessedAssetCount, "All replacement-marked assets should be counted.");
        TestAssert.Equal(2, execution.ProcessedBundleCount, "Processed bundle count should de-duplicate bundle ids.");
        TestAssert.Equal(2, execution.WorkspaceReplacementCount, "Workspace replacement count should reflect matching items.");
        TestAssert.Equal(2, execution.FileReplacementCount, "File replacement count should reflect matching items.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseManifestSizeFirst_AndHashWhenSizeMatches()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var changedBySizeAssetId = new AssetId("asset-size");
        var changedByHashAssetId = new AssetId("asset-hash");
        var unchangedAssetId = new AssetId("asset-same");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, changedBySizeAssetId, AssetType.Texture2D, "SizeChanged"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, changedByHashAssetId, AssetType.Texture2D, "HashChanged"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, unchangedAssetId, AssetType.Texture2D, "SameTexture"));
        var manifestStore = new TestWorkspaceExportManifestStore(CreateManifest(
            "workspace-key",
            [
                CreateManifestItem(bundleId, changedBySizeAssetId, "Texture2D/size.png", 100, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                CreateManifestItem(bundleId, changedByHashAssetId, "Texture2D/hash.png", 100, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                CreateManifestItem(bundleId, unchangedAssetId, "Texture2D/same.png", 100, "cccccccccccccccccccccccccccccccc")
            ]));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.Register("Texture2D/size.png", exists: true, size: 128, hash: null);
        sourceProbe.Register("Texture2D/hash.png", exists: true, size: 100, hash: "dddddddddddddddddddddddddddddddd");
        sourceProbe.Register("Texture2D/same.png", exists: true, size: 100, hash: "cccccccccccccccccccccccccccccccc");
        var handler = new GetWorkspaceBatchReplacementPlanHandler(registry, manifestStore, sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Manifest));

        TestAssert.True(result.IsSuccess, "Manifest replacement plan should succeed.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(2, plan.ChangedCount, "Size mismatch and hash mismatch should both be marked changed.");
        TestAssert.Equal(1, plan.UnchangedCount, "Equal size and hash should be marked unchanged.");
        TestAssert.Equal(2, plan.Candidates.Count(candidate => candidate.IsDefaultSelected), "Only changed candidates should be selected by default.");
        TestAssert.Equal(2, sourceProbe.HashRequestCount, "Hash should only be requested for files whose size matches the manifest.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldReturnManifestIssue_WhenWorkspaceKeyDiffers()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry();
        var manifestStore = new TestWorkspaceExportManifestStore(CreateManifest("other-workspace", []));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        var handler = new GetWorkspaceBatchReplacementPlanHandler(registry, manifestStore, sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "current-workspace",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Manifest));

        TestAssert.True(result.IsSuccess, "Workspace-key mismatch should be reported as a diagnosable plan, not an exception.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(WorkspaceBatchReplacementMatchingMode.Manifest, plan.MatchingMode, "Mismatch still belongs to manifest mode.");
        TestAssert.Equal(1, plan.IssueCount, "Mismatch should produce one blocking issue.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.ManifestInvalid, candidate.MatchStatus, "Mismatch should block execution.");
        TestAssert.True(candidate.Diagnostics.Contains("WorkspaceKeyMismatch"), "Diagnostic should explain the blocker.");
        TestAssert.Equal(0, sourceProbe.SnapshotRequestCount, "No source files should be probed after workspace-key mismatch.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldBuildHighConfidenceHeuristicCandidate_FromStandardExportPath()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var assetId = new AssetId("asset-texture");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_452_bstalk.ab", "ab/char_452_bstalk.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            assetId,
            bundleId,
            "HeroTexture",
            "Assets/HeroTexture",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 42,
            DisplayName: "HeroTexture",
            ExportFileName: "HeroTexture"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_452_bstalk.ab/Texture2D/HeroTexture.png", 128);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Auto));

        TestAssert.True(result.IsSuccess, "Missing manifest in auto mode should fall back to heuristic plan.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(WorkspaceBatchReplacementMatchingMode.Heuristic, plan.MatchingMode, "Plan should be heuristic.");
        TestAssert.Equal(1, plan.ChangedCount, "One high-confidence candidate should be counted as executable.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Standard path should produce high confidence.");
        TestAssert.True(candidate.IsDefaultSelected, "High-confidence heuristic candidate should be selected by default.");
        TestAssert.Equal(bundleId, candidate.BundleId, "Candidate should resolve the target bundle.");
        TestAssert.Equal(assetId, candidate.AssetId, "Candidate should resolve the target asset.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldMarkHeuristicCandidateAmbiguous_WhenTargetsTie()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_452_bstalk.ab", "ab/char_452_bstalk.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            new AssetId("asset-texture-a"),
            bundleId,
            "HeroTexture",
            "Assets/HeroTextureA",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 42,
            DisplayName: "HeroTexture",
            ExportFileName: "HeroTexture"));
        registry.RegisterAssetListItem(new AssetListItemDto(
            new AssetId("asset-texture-b"),
            bundleId,
            "HeroTexture",
            "Assets/HeroTextureB",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 43,
            DisplayName: "HeroTexture",
            ExportFileName: "HeroTexture"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_452_bstalk.ab/Texture2D/HeroTexture.png", 128);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should succeed even when a file is ambiguous.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.Ambiguous, candidate.MatchStatus, "Tied target scores should be marked ambiguous.");
        TestAssert.False(candidate.IsDefaultSelected, "Ambiguous heuristic candidate should not be selected by default.");
        TestAssert.True(candidate.Diagnostics.Contains("Ambiguous"), "Candidate should expose ambiguity diagnostics.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseSourceDirectoryName_AsImplicitBundleForHeuristicPlan()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-audio");
        var assetId = new AssetId("asset-cn-001");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "Voice_CN_char_002_amiya.ab", "ab/Voice_CN_char_002_amiya.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            assetId,
            bundleId,
            "CN_001",
            "dyn/audio/sound_beta_2/voice/CN_001",
            AssetType.AudioClip,
            SizeInBytes: 160,
            PreferredDisplaySizeInBytes: 160,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "AudioClip",
            PathId: 1001,
            DisplayName: "CN_001",
            ExportFileName: "CN_001"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("AudioClip/CN_001.wav", 72288);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export\Voice_CN_char_002_amiya.ab",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support selecting a single bundle export directory.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Implicit bundle context should still produce high confidence.");
        TestAssert.True(candidate.IsDefaultSelected, "High-confidence candidate should remain selected by default.");
        TestAssert.Equal(bundleId, candidate.BundleId, "Candidate should resolve the bundle from source directory name.");
        TestAssert.Equal(assetId, candidate.AssetId, "Candidate should resolve the audio target.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseSourceDirectoryTypeAndParentBundle_ForHeuristicPlan()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-audio");
        var assetId = new AssetId("asset-cn-001");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "Voice_CN_char_002_amiya.ab", "ab/Voice_CN_char_002_amiya.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            assetId,
            bundleId,
            "CN_001",
            "dyn/audio/sound_beta_2/voice/CN_001",
            AssetType.AudioClip,
            SizeInBytes: 160,
            PreferredDisplaySizeInBytes: 160,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "AudioClip",
            PathId: 1001,
            DisplayName: "CN_001",
            ExportFileName: "CN_001"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("CN_001.wav", 72288);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export\Voice_CN_char_002_amiya.ab\AudioClip",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support selecting a type directory under a bundle export directory.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Implicit type and parent bundle context should produce high confidence.");
        TestAssert.True(candidate.IsDefaultSelected, "High-confidence candidate should remain selected by default.");
        TestAssert.Equal(bundleId, candidate.BundleId, "Candidate should resolve the bundle from the source directory parent.");
        TestAssert.Equal(assetId, candidate.AssetId, "Candidate should resolve the audio target.");
        TestAssert.True(candidate.Diagnostics.Contains("TypeDirectoryMatched"), "Candidate should treat the selected source directory as the type directory.");
    }

    private static WorkspaceId CreateWorkspaceId() => new("workspace-session-1");

    private static WorkspaceExportManifestDto CreateManifest(
        string workspaceKey,
        IReadOnlyList<WorkspaceExportManifestItemDto> items) =>
        new(
            SchemaVersion: 1,
            WorkspaceKey: workspaceKey,
            ExportedAtUtc: new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            OutputRoot: @"D:\Export",
            Items: items);

    private static WorkspaceExportManifestItemDto CreateManifestItem(
        BundleId bundleId,
        AssetId assetId,
        string relativePath,
        long exportedFileSizeInBytes,
        string exportedFileHash) =>
        new(
            RelativePath: relativePath,
            BundleId: bundleId.Value,
            BundleDisplayName: "Alpha",
            AssetId: assetId.Value,
            PathId: 12345,
            AssetType: "Texture2D",
            AssetTypeDisplayName: "Texture2D",
            OriginalName: assetId.Value,
            DisambiguatedName: assetId.Value,
            ContainerPath: null,
            OutputRole: WorkspaceExportManifestOutputRole.Asset,
            SpineGroupId: null,
            SpineGroupDisplayName: null,
            ExportedFileSizeInBytes: exportedFileSizeInBytes,
            ExportedFileHashAlgorithm: "MD5",
            ExportedFileHash: exportedFileHash,
            ExportedFileLastWriteTimeUtc: new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName)
    {
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: assetName,
            AssetPath: $"Assets/{assetName}",
            ContainerPath: string.Empty,
            AssetType: assetType,
            SizeInBytes: 256,
            PreferredDisplaySizeInBytes: 256,
            PathId: 12345,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>());
    }

    private static WorkspaceChangeSummaryDto CreateWorkspaceChangeSummary(
        WorkspaceId workspaceId,
        IReadOnlyList<WorkspaceChangeItemDto> items)
    {
        return new WorkspaceChangeSummaryDto(
            WorkspaceId: workspaceId,
            WorkspaceDisplayName: "Workspace One",
            SourcePath: "C:\\Workspaces\\WorkspaceOne",
            BundleCount: items.Select(static item => item.BundleId.Value).Distinct(StringComparer.Ordinal).Count(),
            AssetCount: items.Count,
            TotalChangeCount: items.Count,
            ModifiedAssetCount: items.Count,
            ExtractedAssetCount: 0,
            WorkspaceReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName)),
            FileReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)),
            Items: items);
    }

    private static WorkspaceChangeItemDto CreateChangeItem(
        BundleId bundleId,
        AssetId assetId,
        string bundleDisplayName,
        string assetName,
        string? workspaceReplacementSourceAssetName = null,
        string? fileReplacementSourceFileName = null)
    {
        var appliedAtUtc = new DateTimeOffset(2026, 4, 28, 16, 0, 0, TimeSpan.Zero);
        return new WorkspaceChangeItemDto(
            BundleId: bundleId,
            AssetId: assetId,
            BundleDisplayName: bundleDisplayName,
            AssetName: assetName,
            AssetType: AssetType.Texture2D,
            IsModified: true,
            LastExtractedAtUtc: null,
            WorkspaceReplacementSourceBundleDisplayName: string.IsNullOrWhiteSpace(workspaceReplacementSourceAssetName) ? null : "SourceBundle",
            WorkspaceReplacementSourceAssetName: workspaceReplacementSourceAssetName,
            WorkspaceReplacementAppliedAtUtc: string.IsNullOrWhiteSpace(workspaceReplacementSourceAssetName) ? null : appliedAtUtc,
            FileReplacementSourceFileName: fileReplacementSourceFileName,
            FileReplacementSourcePath: string.IsNullOrWhiteSpace(fileReplacementSourceFileName) ? null : $"C:\\Imports\\{fileReplacementSourceFileName}",
            FileReplacementAppliedAtUtc: string.IsNullOrWhiteSpace(fileReplacementSourceFileName) ? null : appliedAtUtc,
            LastChangedAtUtc: appliedAtUtc);
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry, IWorkspaceReplacementRedoSessionRegistry
    {
        private readonly Dictionary<(string BundleId, string AssetId), AssetDetailsDto> _assetDetails =
            new();
        private readonly List<BundleTreeNodeDto> _bundleTree = new();
        private readonly Dictionary<string, List<AssetListItemDto>> _assetLists =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _bundleIds = new(StringComparer.Ordinal);

        public bool ContainsWorkspaceResult { get; set; } = true;

        public IReadOnlyList<WorkspaceReplacementCandidateDto> WorkspaceReplacementCandidates { get; set; } =
            Array.Empty<WorkspaceReplacementCandidateDto>();

        public AssetFileReplacementResultDto? TryReplaceAssetFromFileResult { get; set; }

        public AssetReplacementResultDto? TryReplaceAssetFromWorkspaceResult { get; set; }

        public AssetReplacementUndoResultDto? TryUndoAssetReplacementResult { get; set; }

        public bool TryUndoAssetReplacementReturnValue { get; set; } = true;

        public AssetReplacementRedoResultDto? TryRedoAssetReplacementResult { get; set; }

        public bool TryRedoAssetReplacementReturnValue { get; set; } = true;

        public WorkspaceChangeSummaryDto? WorkspaceChangeSummary { get; set; }

        public int TryReplaceAssetFromFileCallCount { get; private set; }

        public int TryReplaceAssetFromWorkspaceCallCount { get; private set; }

        public int TryUndoAssetReplacementCallCount { get; private set; }

        public int TryRedoAssetReplacementCallCount { get; private set; }

        public string? LastFileReplacementSourceFilePath { get; private set; }

        public AssetType? LastFileReplacementSourceAssetType { get; private set; }

        public BundleId? LastWorkspaceReplacementSourceBundleId { get; private set; }

        public AssetId? LastWorkspaceReplacementSourceAssetId { get; private set; }

        public void RegisterAssetDetails(AssetDetailsDto details)
        {
            _assetDetails[(details.BundleId.Value, details.AssetId.Value)] = details;
            _bundleIds.Add(details.BundleId.Value);
        }

        public void RegisterBundle(WorkspaceId workspaceId, BundleId bundleId, string displayName, string logicalPath)
        {
            _bundleIds.Add(bundleId.Value);
            _bundleTree.Add(new BundleTreeNodeDto(
                bundleId,
                displayName,
                logicalPath,
                SourcePath: logicalPath,
                IsVirtualFolder: false,
                AssetCount: 0,
                Children: Array.Empty<BundleTreeNodeDto>()));
        }

        public void RegisterAssetListItem(AssetListItemDto item)
        {
            _bundleIds.Add(item.BundleId.Value);
            if (!_assetLists.TryGetValue(item.BundleId.Value, out var items))
            {
                items = new List<AssetListItemDto>();
                _assetLists[item.BundleId.Value] = items;
            }

            items.Add(item);
            RegisterAssetDetails(CreateAssetDetails(
                CreateWorkspaceId(),
                item.BundleId,
                item.AssetId,
                item.AssetType,
                item.DisplayName ?? item.Name));
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) => throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => ContainsWorkspaceResult;

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => _bundleIds.Contains(bundleId.Value);

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            _assetDetails.ContainsKey((bundleId.Value, assetId.Value));

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            bundleTree = _bundleTree;
            return _bundleTree.Count > 0;
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult)
        {
            if (!_assetLists.TryGetValue(bundleId.Value, out var items))
            {
                assetListResult = null;
                return false;
            }

            assetListResult = new AssetListResultDto(
                WorkspaceId: workspaceId,
                BundleId: bundleId,
                BundleDisplayName: bundleId.Value,
                SearchKeyword: searchKeyword,
                AssetTypeFilter: assetTypeFilter,
                TotalCount: items.Count,
                FilteredCount: items.Count,
                Items: items,
                AssetTypeDisplayNameFilter: assetTypeDisplayNameFilter);
            return true;
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            if (_assetDetails.TryGetValue((bundleId.Value, assetId.Value), out var value))
            {
                assetDetails = value;
                return true;
            }

            assetDetails = null;
            return false;
        }

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = WorkspaceChangeSummary;
            return changeSummary is not null;
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            extractionResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
        {
            candidates = WorkspaceReplacementCandidates;
            return candidates.Count > 0;
        }

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            TryReplaceAssetFromFileCallCount++;
            LastFileReplacementSourceFilePath = sourceFilePath;
            LastFileReplacementSourceAssetType = sourceAssetType;
            replacementResult = TryReplaceAssetFromFileResult;
            return replacementResult is not null;
        }

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult)
        {
            TryReplaceAssetFromWorkspaceCallCount++;
            LastWorkspaceReplacementSourceBundleId = sourceBundleId;
            LastWorkspaceReplacementSourceAssetId = sourceAssetId;
            replacementResult = TryReplaceAssetFromWorkspaceResult;
            return replacementResult is not null;
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            TryUndoAssetReplacementCallCount++;
            undoResult = TryUndoAssetReplacementResult;
            return TryUndoAssetReplacementReturnValue;
        }

        public bool TryRedoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementRedoResultDto? redoResult)
        {
            TryRedoAssetReplacementCallCount++;
            redoResult = TryRedoAssetReplacementResult;
            return TryRedoAssetReplacementReturnValue && redoResult is not null;
        }

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext)
        {
            paneContext = null;
            throw new NotSupportedException();
        }
    }

    private sealed class TestWorkspaceExportManifestStore : IWorkspaceExportManifestStore
    {
        private readonly WorkspaceExportManifestDto? _manifest;

        public TestWorkspaceExportManifestStore(WorkspaceExportManifestDto? manifest)
        {
            _manifest = manifest;
        }

        public Task<Result<WorkspaceExportManifestSaveResultDto>> SaveAsync(
            WorkspaceExportManifestSaveRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceExportManifestDto>> LoadAsync(
            string manifestPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_manifest is null
                ? Result<WorkspaceExportManifestDto>.Failure(ApplicationErrors.ExportManifestReadFailed(manifestPath, "TestMissing"))
                : Result<WorkspaceExportManifestDto>.Success(_manifest));
    }

    private sealed class TestWorkspaceBatchReplacementSourceFileProbe : IWorkspaceBatchReplacementSourceFileProbe
    {
        private readonly Dictionary<string, (bool Exists, long? Size, string? Hash)> _snapshots =
            new(StringComparer.OrdinalIgnoreCase);

        public int SnapshotRequestCount { get; private set; }

        public int HashRequestCount { get; private set; }

        public void Register(string relativePath, bool exists, long? size, string? hash) =>
            _snapshots[relativePath] = (exists, size, hash);

        public void RegisterFile(string relativePath, long? size) =>
            _snapshots[relativePath] = (Exists: true, Size: size, Hash: null);

        public Task<Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>> EnumerateFilesAsync(
            string sourceRootDirectory,
            int maxDepth,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto> files = _snapshots
                .Where(static pair => pair.Value.Exists)
                .Select(pair => new WorkspaceBatchReplacementSourceFileEntryDto(
                    pair.Key,
                    $@"{sourceRootDirectory}\{pair.Key}",
                    pair.Value.Size))
                .ToArray();
            return Task.FromResult(Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>.Success(files));
        }

        public Task<Result<WorkspaceBatchReplacementSourceFileSnapshotDto>> GetSnapshotAsync(
            string sourceRootDirectory,
            string relativePath,
            bool computeHash,
            CancellationToken cancellationToken = default)
        {
            SnapshotRequestCount++;
            if (computeHash)
            {
                HashRequestCount++;
            }

            if (!_snapshots.TryGetValue(relativePath, out var snapshot))
            {
                return Task.FromResult(Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Success(
                    new WorkspaceBatchReplacementSourceFileSnapshotDto(
                        $@"{sourceRootDirectory}\{relativePath}",
                        Exists: false,
                        SizeInBytes: null,
                        HashAlgorithm: null,
                        Hash: null)));
            }

            return Task.FromResult(Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Success(
                new WorkspaceBatchReplacementSourceFileSnapshotDto(
                    $@"{sourceRootDirectory}\{relativePath}",
                    snapshot.Exists,
                    snapshot.Size,
                    computeHash ? "MD5" : null,
                    computeHash ? snapshot.Hash : null)));
        }
    }
}
