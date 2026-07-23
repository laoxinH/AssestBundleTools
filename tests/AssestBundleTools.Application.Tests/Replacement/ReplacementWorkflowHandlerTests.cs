using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
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
    public static async Task MatchSpineReplacementSourcesHandler_ShouldUsePayloadFormat_NotFileExtension()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.spine");
        var skeletonAssetId = new AssetId("asset.skeleton");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            skeletonAssetId,
            AssetType.TextAsset,
            "hero.skel.bytes"));
        var targetPreview = new StaticAssetPreviewHandler(CreateSkeletonPreview(
            workspaceId,
            bundleId,
            skeletonAssetId,
            AssetPreviewKind.Binary,
            binaryContent: [0x1c, 0x00, 0x00, 0x00]));
        var jsonNamedSkel = @"D:\Replacement\hero.skel";
        var binaryNamedJson = @"D:\Replacement\hero.json";
        var sourceProvider = new StaticReplacementPreviewSourceProvider(new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase)
        {
            [jsonNamedSkel] = new(
                AssetPreviewKind.Text,
                "application/json",
                Encoding.UTF8.GetBytes("{\"skeleton\":{\"spine\":\"4.2\"},\"bones\":[]}")),
            [binaryNamedJson] = new(
                AssetPreviewKind.Binary,
                "application/octet-stream",
                [0x1c, 0x00, 0x00, 0x00])
        });
        var compatibilityService = new SpineSkeletonReplacementCompatibilityService(
            registry,
            targetPreview,
            sourceProvider);
        var handler = new MatchSpineReplacementSourcesHandler(
            new PassthroughReplacementSourcePathExpander(),
            compatibilityService);
        var group = new SpineAssetGroupDto(
            "spine-hero",
            bundleId,
            "hero",
            "hero",
            SkeletonAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Skeleton,
                    bundleId,
                    skeletonAssetId,
                    "bundle.spine.ab",
                    "hero.skel.bytes",
                    "Assets/Spine/hero.skel.bytes",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            AtlasAssets: [],
            TexturePageAssets: [],
            Diagnostics: []);

        var result = await handler.HandleAsync(new MatchSpineReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            group,
            [jsonNamedSkel, binaryNamedJson]));

        TestAssert.True(result.IsSuccess, "Spine source matching should succeed.");
        var match = TestAssert.Single(result.Value ?? []);
        TestAssert.Equal(
            binaryNamedJson,
            match.SourceFilePath,
            "Matching should reject JSON content for a binary target and accept binary content regardless of its extension.");
    }

    public static async Task MatchSpineReplacementSourcesHandler_ShouldFail_WhenMultipleSourcesMatchOnePart()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.spine");
        var atlasAssetId = new AssetId("asset.atlas");
        var compatibilityService = new SpineSkeletonReplacementCompatibilityService(
            new TestWorkspaceSessionRegistry(),
            new StaticAssetPreviewHandler(null!),
            new StaticReplacementPreviewSourceProvider(
                new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase)));
        var handler = new MatchSpineReplacementSourcesHandler(
            new PassthroughReplacementSourcePathExpander(),
            compatibilityService);
        var group = new SpineAssetGroupDto(
            "spine-hero",
            bundleId,
            "hero",
            "hero",
            SkeletonAssets: [],
            AtlasAssets:
            [
                new SpineAssetGroupPartDto(
                    SpineAssetGroupPartRole.Atlas,
                    bundleId,
                    atlasAssetId,
                    "bundle.spine.ab",
                    "hero.atlas",
                    "Assets/Spine/hero.atlas",
                    AssetType.TextAsset,
                    "TextAsset")
            ],
            TexturePageAssets: [],
            Diagnostics: []);

        var result = await handler.HandleAsync(new MatchSpineReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            group,
            [@"D:\Replacement\Folder\hero.atlas", @"D:\Replacement\hero.atlas"]));

        TestAssert.True(result.IsFailure, "Ambiguous Spine source matching should fail.");
        var error = TestAssert.Single(result.Errors);
        TestAssert.Equal(
            "Asset.Replace.SpineSourceConflict",
            error.Code,
            "Ambiguous matching should return the stable Spine source conflict error.");
        TestAssert.Equal(
            2,
            Convert.ToInt32(error.Parameters["CandidateCount"]),
            "The conflict should report every compatible candidate for the target part.");
    }

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

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldReturnFailure_WhenNoCandidatesSelected()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry();
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry, new ReplaceAssetFromFileHandler(registry), sourceProbe);

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: "C:\\WorkspaceData",
            SourceRootDirectory: "C:\\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            Candidates: Array.Empty<WorkspaceBatchReplacementCandidateDto>()));

        TestAssert.True(result.IsFailure, "Batch replacement should fail when no candidates are selected.");
        TestAssert.Equal(
            "Asset.Replace.NoBatchCandidates",
            TestAssert.Single(result.Errors).Code,
            "Failure should report that no batch candidates exist.");
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldApplySelectedCandidates_AndSkipUnsupportedTargets()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var textureAssetId = new AssetId("asset-texture");
        var atlasAssetId = new AssetId("asset-atlas");
        var skeletonAssetId = new AssetId("asset-skeleton");
        var audioAssetId = new AssetId("asset-audio");
        var meshAssetId = new AssetId("asset-mesh");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, textureAssetId, AssetType.Texture2D, "HeroTexture"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, atlasAssetId, AssetType.TextAsset, "HeroAtlas"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, skeletonAssetId, AssetType.TextAsset, "HeroSkeleton"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, audioAssetId, AssetType.AudioClip, "HeroVoice"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, meshAssetId, AssetType.Mesh, "HeroMesh"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("Texture2D/HeroTexture.png", 128);
        sourceProbe.RegisterFile("AudioClip/HeroVoice.wav", 128);
        sourceProbe.RegisterFile("Spine/Hero/Hero.atlas", 128);
        sourceProbe.RegisterFile("Spine/Hero/Hero.skel", 128);
        sourceProbe.RegisterFile("Mesh/HeroMesh.obj", 128);
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry, new ReplaceAssetFromFileHandler(registry), sourceProbe);
        var candidates = new[]
        {
            CreateBatchReplacementCandidate(bundleId, textureAssetId, AssetType.Texture2D, "HeroTexture", "Texture2D/HeroTexture.png"),
            CreateBatchReplacementCandidate(bundleId, atlasAssetId, AssetType.TextAsset, "HeroAtlas", "Spine/Hero/Hero.atlas", spineGroupId: "hero-spine", spineGroupDisplayName: "Hero"),
            CreateBatchReplacementCandidate(bundleId, audioAssetId, AssetType.AudioClip, "HeroVoice", "AudioClip/HeroVoice.wav"),
            CreateBatchReplacementCandidate(bundleId, skeletonAssetId, AssetType.TextAsset, "HeroSkeleton", "Spine/Hero/Hero.skel", spineGroupId: "hero-spine", spineGroupDisplayName: "Hero"),
            CreateBatchReplacementCandidate(bundleId, meshAssetId, AssetType.Mesh, "HeroMesh", "Mesh/HeroMesh.obj")
        };

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: "C:\\WorkspaceData",
            SourceRootDirectory: "C:\\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            candidates));

        TestAssert.True(result.IsSuccess, "Batch replacement should succeed and summarize per-item results.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected batch replacement result.");
        TestAssert.Equal(5, execution.RequestedCandidateCount, "All selected candidates should be reflected in the result.");
        TestAssert.Equal(4, execution.ReplacedAssetCount, "Supported image, text, audio, and Spine component replacements should run through the single-asset replacement handler.");
        TestAssert.Equal(1, execution.SkippedAssetCount, "Unsupported candidate should be retained as a skipped item result.");
        TestAssert.Equal(0, execution.FailedAssetCount, "Skipped unsupported types should not be treated as handler failures.");
        TestAssert.Equal(4, registry.TryReplaceAssetFromFileCallCount, "Only supported candidates should invoke file replacement.");
        TestAssert.True(
            registry.FileReplacementSourceFilePaths.Take(2).All(static path => !path.Contains("\\Spine\\", StringComparison.OrdinalIgnoreCase)),
            "Ordinary resources should execute before Spine group components.");
        TestAssert.Equal("C:\\Exports\\Spine\\Hero\\Hero.atlas", registry.FileReplacementSourceFilePaths[2], "Spine atlas should execute after ordinary resources.");
        TestAssert.Equal("C:\\Exports\\Spine\\Hero\\Hero.skel", registry.FileReplacementSourceFilePaths[3], "Spine skeleton should execute after ordinary resources.");
        TestAssert.Equal(1, execution.ProcessedSpineGroupCount, "Spine component replacements should be summarized as one processed Spine group.");
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldExecuteDuplicateOrdinaryCandidateOnce()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var textureAssetId = new AssetId("asset-texture");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, textureAssetId, AssetType.Texture2D, "HeroTexture"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("Texture2D/HeroTexture.png", 128);
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry, new ReplaceAssetFromFileHandler(registry), sourceProbe);
        var candidate = CreateBatchReplacementCandidate(
            bundleId,
            textureAssetId,
            AssetType.Texture2D,
            "HeroTexture",
            "Texture2D/HeroTexture.png");

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: "C:\\WorkspaceData",
            SourceRootDirectory: "C:\\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            Candidates: [candidate, candidate]));

        TestAssert.True(result.IsSuccess, "Duplicate ordinary candidates should be normalized before execution.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected batch replacement result.");
        TestAssert.Equal(1, execution.RequestedCandidateCount, "Duplicate candidates should be counted once in the execution result.");
        TestAssert.Equal(1, execution.ReplacedAssetCount, "Only one ordinary replacement should be applied.");
        TestAssert.Equal(1, registry.TryReplaceAssetFromFileCallCount, "Duplicate ordinary candidates should not create duplicate replacement records.");
        TestAssert.Equal("C:\\Exports\\Texture2D\\HeroTexture.png", registry.FileReplacementSourceFilePaths[0], "The normalized candidate should still execute with its source path.");
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldRecordOnlyCurrentSpinePartRelation()
    {
        var workspaceId = CreateWorkspaceId();
        var workspaceKey = "workspace-key";
        var workspaceDataRootPath = "C:\\WorkspaceData";
        var bundleId = new BundleId("bundle-a");
        var textureAssetId = new AssetId("asset-texture");
        var atlasAssetId = new AssetId("asset-atlas");
        var skeletonAssetId = new AssetId("asset-skeleton");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, textureAssetId, AssetType.Texture2D, "HeroPage"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, atlasAssetId, AssetType.TextAsset, "HeroAtlas"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, skeletonAssetId, AssetType.TextAsset, "HeroSkeleton"));
        var changeStore = new TestWorkspaceChangeStore();
        var recorder = new WorkspaceReplacementOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("Spine/Hero/Hero.png", 128);
        sourceProbe.RegisterFile("Spine/Hero/Hero.atlas", 128);
        sourceProbe.RegisterFile("Spine/Hero/Hero.skel", 128);
        var handler = new ExecuteWorkspaceBatchReplacementHandler(
            registry,
            new ReplaceAssetFromFileHandler(registry, recorder),
            sourceProbe);

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            workspaceKey,
            workspaceDataRootPath,
            SourceRootDirectory: "C:\\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            Candidates:
            [
                CreateBatchReplacementCandidate(bundleId, textureAssetId, AssetType.Texture2D, "HeroPage", "Spine/Hero/Hero.png", spineGroupId: "hero-spine", spineGroupDisplayName: "Hero"),
                CreateBatchReplacementCandidate(bundleId, atlasAssetId, AssetType.TextAsset, "HeroAtlas", "Spine/Hero/Hero.atlas", spineGroupId: "hero-spine", spineGroupDisplayName: "Hero"),
                CreateBatchReplacementCandidate(bundleId, skeletonAssetId, AssetType.TextAsset, "HeroSkeleton", "Spine/Hero/Hero.skel", spineGroupId: "hero-spine", spineGroupDisplayName: "Hero")
            ]));

        TestAssert.True(result.IsSuccess, "Spine batch replacement should still execute all group parts.");
        var operations = changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>();
        TestAssert.Equal(3, operations.Count, "Each Spine part should produce one replacement operation.");
        foreach (var operation in operations)
        {
            var relatedTargets = (operation.Relations ?? Array.Empty<WorkspaceOperationRelationDto>())
                .Where(static relation => relation.RelatedTarget is not null)
                .Select(static relation => relation.RelatedTarget!)
                .ToArray();

            TestAssert.Equal(1, relatedTargets.Length, "Spine operation relation should point only to the part being replaced.");
            TestAssert.Equal(operation.Target?.BundleId, relatedTargets[0].BundleId, "Related target should keep the current operation bundle.");
            TestAssert.Equal(operation.Target?.AssetId, relatedTargets[0].AssetId, "Related target should keep the current operation asset.");
        }
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldApplySpriteImageToAssociatedTexture()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var spriteAssetId = new AssetId("asset-sprite");
        var textureAssetId = new AssetId("asset-texture");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, textureAssetId, AssetType.Texture2D, "HeroTexture"));
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            spriteAssetId,
            AssetType.Sprite,
            "HeroSprite",
            dependencies:
            [
                new AssetDependencyDto(textureAssetId, bundleId, "HeroTexture", AssetType.Texture2D)
            ]));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("Sprite/HeroSprite.png", 128);
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry, new ReplaceAssetFromFileHandler(registry), sourceProbe);

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: "C:\\WorkspaceData",
            SourceRootDirectory: "C:\\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            Candidates:
            [
                CreateBatchReplacementCandidate(bundleId, spriteAssetId, AssetType.Sprite, "HeroSprite", "Sprite/HeroSprite.png")
            ]));

        TestAssert.True(result.IsSuccess, "Sprite image batch replacement should succeed through its associated Texture2D.");
        TestAssert.Equal(1, registry.TryReplaceAssetFromFileCallCount, "Sprite candidate should still execute one file replacement.");
        TestAssert.Equal(textureAssetId, registry.LastFileReplacementTargetAssetId, "Sprite image replacement should target the associated Texture2D.");
        TestAssert.Equal(AssetType.Texture2D, registry.LastFileReplacementSourceAssetType, "Sprite image replacement source type should be converted to Texture2D.");
    }

    public static async Task ExecuteWorkspaceBatchReplacementHandler_ShouldApplyTextureBeforeSpriteOverride_WhenBothTargetSameTexture()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var spriteAssetId = new AssetId("asset-sprite");
        var textureAssetId = new AssetId("asset-texture");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, textureAssetId, AssetType.Texture2D, "HeroTexture"));
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            spriteAssetId,
            AssetType.Sprite,
            "HeroSprite",
            dependencies:
            [
                new AssetDependencyDto(textureAssetId, bundleId, "HeroTexture", AssetType.Texture2D)
            ]));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("Sprite/HeroSprite.png", 128);
        sourceProbe.RegisterFile("Texture2D/HeroTexture.png", 128);
        var handler = new ExecuteWorkspaceBatchReplacementHandler(registry, new ReplaceAssetFromFileHandler(registry), sourceProbe);

        var result = await handler.HandleAsync(new ExecuteWorkspaceBatchReplacementCommand(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: "C:\\WorkspaceData",
            SourceRootDirectory: "C:\\Exports",
            WorkspaceBatchReplacementMatchingMode.Manifest,
            Candidates:
            [
                CreateBatchReplacementCandidate(bundleId, spriteAssetId, AssetType.Sprite, "HeroSprite", "Sprite/HeroSprite.png"),
                CreateBatchReplacementCandidate(bundleId, textureAssetId, AssetType.Texture2D, "HeroTexture", "Texture2D/HeroTexture.png")
            ]));

        TestAssert.True(result.IsSuccess, "Texture and Sprite candidates should both apply when they target the same texture.");
        TestAssert.Equal(2, registry.TryReplaceAssetFromFileCallCount, "Both candidates should become replacement operations.");
        TestAssert.Equal("C:\\Exports\\Texture2D\\HeroTexture.png", registry.FileReplacementSourceFilePaths[0], "Texture2D export should be applied first.");
        TestAssert.Equal("C:\\Exports\\Sprite\\HeroSprite.png", registry.FileReplacementSourceFilePaths[1], "Sprite export should be applied after Texture2D so it wins.");
        TestAssert.True(
            registry.FileReplacementTargetAssetIds.All(id => id == textureAssetId),
            "Both operations should target the associated Texture2D.");
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

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldKeepSpineGroupMetadata_FromManifestItems()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var textureAssetId = new AssetId("spine-texture");
        var atlasAssetId = new AssetId("spine-atlas");
        var spineGroupId = "char_452_bstalk_snow#4 [1]";
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, textureAssetId, AssetType.Texture2D, "char_452_bstalk_snow#4"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, bundleId, atlasAssetId, AssetType.TextAsset, "char_452_bstalk_snow#4"));
        var manifestStore = new TestWorkspaceExportManifestStore(CreateManifest(
            "workspace-key",
            [
                CreateManifestItem(
                    bundleId,
                    textureAssetId,
                    "char_452_bstalk.ab/Spine/char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.png",
                    100,
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    "Texture2D",
                    WorkspaceExportManifestOutputRole.SpineTexturePage,
                    spineGroupId,
                    spineGroupId),
                CreateManifestItem(
                    bundleId,
                    atlasAssetId,
                    "char_452_bstalk.ab/Spine/char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.atlas",
                    100,
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    "TextAsset",
                    WorkspaceExportManifestOutputRole.SpineAtlas,
                    spineGroupId,
                    spineGroupId)
            ]));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.Register("char_452_bstalk.ab/Spine/char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.png", exists: true, size: 128, hash: null);
        sourceProbe.Register("char_452_bstalk.ab/Spine/char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.atlas", exists: true, size: 128, hash: null);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(registry, manifestStore, sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Manifest));

        TestAssert.True(result.IsSuccess, "Manifest replacement plan should keep Spine metadata.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(2, plan.ChangedCount, "Both changed Spine component files should remain executable candidates.");
        TestAssert.True(
            plan.Candidates.All(candidate => string.Equals(candidate.SpineGroupId, spineGroupId, StringComparison.Ordinal)),
            "Manifest candidates for Spine group parts should keep the shared SpineGroupId for UI grouping and execution ordering.");
        TestAssert.True(
            plan.Candidates.Any(candidate => candidate.OutputRole == WorkspaceExportManifestOutputRole.SpineTexturePage.ToString()),
            "Manifest Spine texture page candidates should keep the texture page output role.");
        TestAssert.True(
            plan.Candidates.Any(candidate => candidate.OutputRole == WorkspaceExportManifestOutputRole.SpineAtlas.ToString()),
            "Manifest Spine atlas candidates should keep the atlas output role.");
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

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldReturnCancelled_WhenHeuristicSpineCatalogCancels()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var assetId = new AssetId("asset-atlas");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_1001_amiya2.ab", "ab/char_1001_amiya2.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            assetId,
            bundleId,
            "char_1001_amiya2_casc#1.atlas",
            "Assets/Spine/char_1001_amiya2_casc#1.atlas",
            AssetType.TextAsset,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "TextAsset",
            PathId: 42,
            DisplayName: "char_1001_amiya2_casc#1.atlas",
            ExportFileName: "char_1001_amiya2_casc#1"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.atlas", 128);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe,
            new CancellingSpineAssetGroupsHandler());

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Auto));

        TestAssert.True(result.IsCancelled, "Heuristic plan cancellation from Spine catalog should return a cancelled result instead of surfacing OperationCanceledException.");
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

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseOrdinalSuffix_ForDuplicateOrdinaryAssets()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-a");
        var firstAssetId = new AssetId("asset-texture-one");
        var secondAssetId = new AssetId("asset-texture-two");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_452_bstalk.ab", "ab/char_452_bstalk.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            firstAssetId,
            bundleId,
            "HeroTexture",
            "Assets/HeroTexture",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 42));
        registry.RegisterAssetListItem(new AssetListItemDto(
            secondAssetId,
            bundleId,
            "HeroTexture",
            "Assets/HeroTexture",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 43));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_452_bstalk.ab/Texture2D/HeroTexture [1].png", 128);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should succeed for duplicate ordinary assets.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Exact ordinal suffix should disambiguate ordinary same-name assets.");
        TestAssert.True(candidate.IsDefaultSelected, "Ordinal-disambiguated ordinary assets should be selected by default.");
        TestAssert.Equal(firstAssetId, candidate.AssetId, "Candidate should resolve to the ordinary asset with the matching ordinal suffix.");
        TestAssert.Equal("HeroTexture [1]", candidate.AssetName, "Candidate should expose the projected duplicate display name.");
        TestAssert.True(candidate.Diagnostics.Contains("OrdinalSuffixMatched"), "Candidate should expose ordinal suffix match evidence.");
        TestAssert.False(candidate.Diagnostics.Contains("Ambiguous"), "Ordinal-disambiguated ordinary assets should not be marked ambiguous.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseProjectedOrdinalSuffix_FromStandardExportRoot()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-amiya");
        var firstAssetId = new AssetId("asset-casc-one");
        var secondAssetId = new AssetId("asset-casc-two");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_1001_amiya2.ab", "ab/char_1001_amiya2.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            firstAssetId,
            bundleId,
            "char_1001_amiya2_casc#1",
            "Assets/characters/char_1001_amiya2_casc#1_a",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 1001));
        registry.RegisterAssetListItem(new AssetListItemDto(
            secondAssetId,
            bundleId,
            "char_1001_amiya2_casc#1",
            "Assets/characters/char_1001_amiya2_casc#1_b",
            AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 1002));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Texture2D/char_1001_amiya2_casc#1 [1].png", 128);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support ordinary duplicate assets from the standard export root.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Projected export suffix should disambiguate duplicate ordinary assets.");
        TestAssert.Equal(firstAssetId, candidate.AssetId, "Candidate should resolve to the projected [1] ordinary asset.");
        TestAssert.Equal("char_1001_amiya2_casc#1 [1]", candidate.AssetName, "Candidate should expose the same disambiguated name shown by the asset list.");
        TestAssert.True(candidate.Diagnostics.Contains("OrdinalSuffixMatched"), "Candidate should expose ordinal suffix match evidence.");
        TestAssert.False(candidate.Diagnostics.Contains("Ambiguous"), "Projected ordinal suffix should avoid multi-candidate status.");
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

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineDirectoryAndParentBundle_ForHeuristicPlan()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var assetId = new AssetId("asset-atlas");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_452_bstalk.ab", "ab/char_452_bstalk.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            assetId,
            bundleId,
            "char_452_bstalk_snow#4",
            "char_452_bstalk_snow#4.atlas",
            AssetType.TextAsset,
            SizeInBytes: 512,
            PreferredDisplaySizeInBytes: 512,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "TextAsset",
            PathId: 2001,
            DisplayName: "char_452_bstalk_snow#4",
            ExportFileName: "char_452_bstalk_snow#4"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_452_bstalk_snow#4 [1]/char_452_bstalk_snow#4.atlas", 2048);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export\char_452_bstalk.ab\Spine",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support selecting the Spine directory under a bundle export directory.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Implicit Spine directory context should produce high confidence.");
        TestAssert.True(candidate.IsDefaultSelected, "High-confidence Spine candidate should remain selected by default.");
        TestAssert.Equal(bundleId, candidate.BundleId, "Candidate should resolve the bundle from the Spine directory parent.");
        TestAssert.Equal(assetId, candidate.AssetId, "Candidate should resolve the atlas target.");
        TestAssert.Equal("char_452_bstalk_snow#4 [1]", candidate.SpineGroupId, "Candidate should resolve the Spine group from the relative path.");
        TestAssert.Equal("Atlas", candidate.OutputRole, "Atlas files under Spine context should resolve the atlas output role.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldPreferSpineGroupScope_FromStandardExportRoot()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var groupAssetId = new AssetId("asset-group-page");
        var looseAssetId = new AssetId("asset-loose-page");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_1001_amiya2.ab", "ab/char_1001_amiya2.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            groupAssetId,
            bundleId,
            "char_1001_amiya2_casc#1",
            "Spine/char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.png",
            AssetType.Texture2D,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: 1024,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 3001,
            DisplayName: "char_1001_amiya2_casc#1",
            ExportFileName: "char_1001_amiya2_casc#1"));
        registry.RegisterAssetListItem(new AssetListItemDto(
            looseAssetId,
            bundleId,
            "char_1001_amiya2_casc#1",
            "Texture2D/char_1001_amiya2_casc#1.png",
            AssetType.Texture2D,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: 1024,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 3002,
            DisplayName: "char_1001_amiya2_casc#1",
            ExportFileName: "char_1001_amiya2_casc#1"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.png", 4096);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support standard export roots without a manifest.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Spine group context should disambiguate same-name texture targets.");
        TestAssert.True(candidate.IsDefaultSelected, "High-confidence Spine texture page should be selected by default.");
        TestAssert.Equal(groupAssetId, candidate.AssetId, "Candidate should prefer the target whose path belongs to the inferred Spine group.");
        TestAssert.Equal("char_1001_amiya2_casc#1 [1]", candidate.SpineGroupId, "Candidate should preserve the source Spine group.");
        TestAssert.Equal("TexturePage", candidate.OutputRole, "Texture files under Spine context should resolve the texture page output role.");
        TestAssert.False(candidate.Diagnostics.Contains("Ambiguous"), "Spine group scoped match should not be marked ambiguous.");
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldPreserveSpineGroupOrdinalScope_FromSpineDirectory()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_1001_amiya2.ab", "ab/char_1001_amiya2.ab");
        RegisterSpineGroupParts(registry, bundleId, "char_1001_amiya2_casc#1 [1]", "one");
        RegisterSpineGroupParts(registry, bundleId, "char_1001_amiya2_casc#1 [2]", "two");
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.atlas", 4096);
        sourceProbe.RegisterFile("char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.png", 4096);
        sourceProbe.RegisterFile("char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.skel", 4096);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export\char_1001_amiya2.ab\Spine",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support selecting the Spine directory without a manifest.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(3, plan.Candidates.Count, "All three source Spine parts should be planned.");
        foreach (var candidate in plan.Candidates)
        {
            TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Exact Spine group ordinal should disambiguate same-name group parts.");
            TestAssert.True(candidate.IsDefaultSelected, "High-confidence Spine parts should be selected by default.");
            TestAssert.True(candidate.AssetId?.Value.EndsWith("-one", StringComparison.Ordinal) == true, "Candidate should resolve to the [1] group assets.");
            TestAssert.Equal("char_1001_amiya2_casc#1 [1]", candidate.SpineGroupId, "Candidate should preserve the exact source Spine group.");
            TestAssert.False(candidate.Diagnostics.Contains("Ambiguous"), "Exact Spine group scope should not be marked ambiguous.");
        }
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldPreserveSpineGroupOrdinalScope_FromStandardExportRoot()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_1001_amiya2.ab", "ab/char_1001_amiya2.ab");
        RegisterSpineGroupParts(registry, bundleId, "char_1001_amiya2_casc#1 [1]", "one");
        RegisterSpineGroupParts(registry, bundleId, "char_1001_amiya2_casc#1 [2]", "two");
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.atlas", 4096);
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.png", 4096);
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/char_1001_amiya2_casc#1 [1]/char_1001_amiya2_casc#1.skel", 4096);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support selecting the standard export root without a manifest.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(3, plan.Candidates.Count, "All three source Spine parts should be planned from the standard export root.");
        foreach (var candidate in plan.Candidates)
        {
            TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Exact Spine group ordinal should disambiguate same-name group parts from the export root: " + FormatCandidate(candidate));
            TestAssert.True(candidate.IsDefaultSelected, "High-confidence Spine parts should be selected by default.");
            TestAssert.True(candidate.AssetId?.Value.EndsWith("-one", StringComparison.Ordinal) == true, "Candidate should resolve to the [1] group assets.");
            TestAssert.Equal("char_1001_amiya2.ab", candidate.BundleDisplayName, "Candidate should resolve the bundle from the first path segment.");
            TestAssert.Equal("char_1001_amiya2_casc#1 [1]", candidate.SpineGroupId, "Candidate should preserve the exact source Spine group.");
            TestAssert.False(candidate.Diagnostics.Contains("Ambiguous"), "Exact Spine group scope from the export root should not be marked ambiguous.");
        }
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineGroupCatalog_ForDuplicateHeuristicParts()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_1001_amiya2.ab", "ab/char_1001_amiya2.ab");
        RegisterLinkedDuplicateSpineGroup(registry, workspaceId, bundleId, "one");
        RegisterLinkedDuplicateSpineGroup(registry, workspaceId, bundleId, "two");
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/Hero [1]/Hero.atlas", 4096);
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/Hero [1]/Hero.png", 4096);
        sourceProbe.RegisterFile("char_1001_amiya2.ab/Spine/Hero [1]/Hero.skel", 4096);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe,
            new LoadSpineAssetGroupsHandler(registry));

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should use the existing Spine group catalog when manifest is missing.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        TestAssert.Equal(3, plan.Candidates.Count, "All exported parts from the selected Spine group should be planned.");
        foreach (var candidate in plan.Candidates)
        {
            TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Spine catalog should resolve duplicate same-name parts without ambiguity: " + FormatCandidate(candidate));
            TestAssert.True(candidate.AssetId?.Value.EndsWith("-one", StringComparison.Ordinal) == true, "Candidate should resolve to the first dependency-linked Spine group.");
            TestAssert.True(candidate.Diagnostics.Contains("SpineGroupCatalogMatched"), "Candidate should expose the catalog match evidence.");
            TestAssert.False(candidate.Diagnostics.Contains("Ambiguous"), "Catalog-resolved duplicate Spine parts should not be marked ambiguous.");
        }
    }

    public static async Task GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineGroupDirectoryAndAncestorBundle_ForHeuristicPlan()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-spine");
        var assetId = new AssetId("asset-page");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterBundle(workspaceId, bundleId, "char_452_bstalk.ab", "ab/char_452_bstalk.ab");
        registry.RegisterAssetListItem(new AssetListItemDto(
            assetId,
            bundleId,
            "char_452_bstalk_snow#4",
            "char_452_bstalk_snow#4.png",
            AssetType.Texture2D,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: 1024,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: "Texture2D",
            PathId: 2002,
            DisplayName: "char_452_bstalk_snow#4",
            ExportFileName: "char_452_bstalk_snow#4"));
        var sourceProbe = new TestWorkspaceBatchReplacementSourceFileProbe();
        sourceProbe.RegisterFile("char_452_bstalk_snow#4.png", 4096);
        var handler = new GetWorkspaceBatchReplacementPlanHandler(
            registry,
            new TestWorkspaceExportManifestStore(null),
            sourceProbe);

        var result = await handler.HandleAsync(new GetWorkspaceBatchReplacementPlanQuery(
            workspaceId,
            "workspace-key",
            @"D:\Export\char_452_bstalk.ab\Spine\char_452_bstalk_snow#4 [1]",
            WorkspaceBatchReplacementMatchingMode.Heuristic));

        TestAssert.True(result.IsSuccess, "Heuristic planning should support selecting a single Spine group directory.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected replacement plan.");
        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.Equal(WorkspaceBatchReplacementMatchStatus.HighConfidence, candidate.MatchStatus, "Implicit Spine group context should produce high confidence.");
        TestAssert.True(candidate.IsDefaultSelected, "High-confidence Spine group candidate should remain selected by default.");
        TestAssert.Equal(bundleId, candidate.BundleId, "Candidate should resolve the bundle from the Spine group directory ancestor.");
        TestAssert.Equal(assetId, candidate.AssetId, "Candidate should resolve the texture page target.");
        TestAssert.Equal("char_452_bstalk_snow#4 [1]", candidate.SpineGroupId, "Candidate should resolve the Spine group from the selected directory name.");
        TestAssert.Equal("TexturePage", candidate.OutputRole, "Texture files under Spine context should resolve the texture page output role.");
    }

    private static WorkspaceId CreateWorkspaceId() => new("workspace-session-1");

    private static string FormatCandidate(WorkspaceBatchReplacementCandidateDto candidate) =>
        string.Concat(
            candidate.RelativePath,
            " => ",
            candidate.AssetId?.Value ?? "<none>",
            " / ",
            candidate.MatchStatus,
            " / ",
            string.Join(",", candidate.Diagnostics));

    private static void RegisterSpineGroupParts(
        TestWorkspaceSessionRegistry registry,
        BundleId bundleId,
        string groupName,
        string suffix)
    {
        RegisterSpineGroupPart(registry, bundleId, groupName, suffix, "atlas", AssetType.TextAsset);
        RegisterSpineGroupPart(registry, bundleId, groupName, suffix, "skel", AssetType.TextAsset);
        RegisterSpineGroupPart(registry, bundleId, groupName, suffix, "png", AssetType.Texture2D);
    }

    private static void RegisterSpineGroupPart(
        TestWorkspaceSessionRegistry registry,
        BundleId bundleId,
        string groupName,
        string suffix,
        string extension,
        AssetType assetType)
    {
        registry.RegisterAssetListItem(new AssetListItemDto(
            new AssetId($"asset-{extension}-{suffix}"),
            bundleId,
            "char_1001_amiya2_casc#1",
            $"Spine/{groupName}/char_1001_amiya2_casc#1.{extension}",
            assetType,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: 1024,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: assetType.ToString(),
            PathId: (suffix == "one" ? 3100 : 3200) + extension.Length,
            DisplayName: "char_1001_amiya2_casc#1",
            ExportFileName: "char_1001_amiya2_casc#1"));
    }

    private static void RegisterLinkedDuplicateSpineGroup(
        TestWorkspaceSessionRegistry registry,
        WorkspaceId workspaceId,
        BundleId bundleId,
        string suffix)
    {
        RegisterAssetListItemWithDetails(registry, workspaceId, bundleId, $"skel-{suffix}", "Assets/Hero/Hero.skel", AssetType.TextAsset);
        RegisterAssetListItemWithDetails(registry, workspaceId, bundleId, $"atlas-{suffix}", "Assets/Hero/Hero.atlas", AssetType.TextAsset);
        RegisterAssetListItemWithDetails(registry, workspaceId, bundleId, $"texture-{suffix}", "Assets/Hero/Hero.png", AssetType.Texture2D);
        RegisterAssetListItemWithDetails(registry, workspaceId, bundleId, $"material-{suffix}", "Assets/Hero/Hero_Material", AssetType.Material, dependencies:
        [
            CreateDependency(bundleId, $"texture-{suffix}", "Hero", AssetType.Texture2D)
        ]);
        RegisterAssetListItemWithDetails(registry, workspaceId, bundleId, $"atlas-object-{suffix}", "Assets/Hero/Hero_Atlas", AssetType.Unknown, "MonoBehaviour", dependencies:
        [
            CreateDependency(bundleId, $"atlas-{suffix}", "Hero.atlas", AssetType.TextAsset),
            CreateDependency(bundleId, $"material-{suffix}", "Hero_Material", AssetType.Material)
        ]);
        RegisterAssetListItemWithDetails(registry, workspaceId, bundleId, $"skeleton-data-{suffix}", "Assets/Hero/Hero_SkeletonData", AssetType.Unknown, "MonoBehaviour", dependencies:
        [
            CreateDependency(bundleId, $"atlas-object-{suffix}", "Hero_Atlas", AssetType.Unknown, "MonoBehaviour"),
            CreateDependency(bundleId, $"skel-{suffix}", "Hero.skel", AssetType.TextAsset)
        ]);
    }

    private static void RegisterAssetListItemWithDetails(
        TestWorkspaceSessionRegistry registry,
        WorkspaceId workspaceId,
        BundleId bundleId,
        string assetId,
        string assetPath,
        AssetType assetType,
        string? assetTypeDisplayName = null,
        IReadOnlyList<AssetDependencyDto>? dependencies = null)
    {
        var item = new AssetListItemDto(
            new AssetId(assetId),
            bundleId,
            Path.GetFileNameWithoutExtension(assetPath),
            assetPath,
            assetType,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: 1024,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: assetTypeDisplayName ?? assetType.ToString(),
            PathId: assetId.GetHashCode(StringComparison.Ordinal),
            DisplayName: Path.GetFileNameWithoutExtension(assetPath),
            ExportFileName: Path.GetFileNameWithoutExtension(assetPath));
        registry.RegisterAssetListItem(
            item,
            CreateAssetDetails(
                workspaceId,
                bundleId,
                item.AssetId,
                assetType,
                item.Name,
                assetPath,
                assetTypeDisplayName ?? assetType.ToString(),
                dependencies ?? Array.Empty<AssetDependencyDto>()));
    }

    private static AssetDependencyDto CreateDependency(
        BundleId bundleId,
        string assetId,
        string name,
        AssetType assetType,
        string? assetTypeDisplayName = null) =>
        new(
            new AssetId(assetId),
            bundleId,
            name,
            assetType,
            PathId: 0,
            AssetTypeDisplayName: assetTypeDisplayName ?? assetType.ToString());

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
        string exportedFileHash,
        string assetType = "Texture2D",
        WorkspaceExportManifestOutputRole outputRole = WorkspaceExportManifestOutputRole.Asset,
        string? spineGroupId = null,
        string? spineGroupDisplayName = null) =>
        new(
            RelativePath: relativePath,
            BundleId: bundleId.Value,
            BundleDisplayName: "Alpha",
            AssetId: assetId.Value,
            PathId: 12345,
            AssetType: assetType,
            AssetTypeDisplayName: assetType,
            OriginalName: assetId.Value,
            DisambiguatedName: assetId.Value,
            ContainerPath: null,
            OutputRole: outputRole,
            SpineGroupId: spineGroupId,
            SpineGroupDisplayName: spineGroupDisplayName,
            ExportedFileSizeInBytes: exportedFileSizeInBytes,
            ExportedFileHashAlgorithm: "MD5",
            ExportedFileHash: exportedFileHash,
            ExportedFileLastWriteTimeUtc: new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

    private static WorkspaceBatchReplacementCandidateDto CreateBatchReplacementCandidate(
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName,
        string relativePath,
        WorkspaceBatchReplacementMatchStatus status = WorkspaceBatchReplacementMatchStatus.Changed,
        string? spineGroupId = null,
        string? spineGroupDisplayName = null) =>
        new(
            CandidateId: $"candidate:{bundleId.Value}:{assetId.Value}:{relativePath}",
            BundleId: bundleId,
            BundleDisplayName: bundleId.Value,
            AssetId: assetId,
            PathId: 12345,
            AssetType: assetType,
            AssetTypeDisplayName: assetType.ToString(),
            AssetName: assetName,
            ContainerPath: null,
            RelativePath: relativePath,
            FullPath: $"C:\\Exports\\{relativePath.Replace('/', '\\')}",
            CurrentFileSizeInBytes: 128,
            CurrentFileHashAlgorithm: "MD5",
            CurrentFileHash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ExportedFileSizeInBytes: 100,
            ExportedFileHashAlgorithm: "MD5",
            ExportedFileHash: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            SpineGroupId: spineGroupId,
            SpineGroupDisplayName: spineGroupDisplayName,
            OutputRole: WorkspaceExportManifestOutputRole.Asset.ToString(),
            MatchStatus: status,
            MatchMode: WorkspaceBatchReplacementMatchingMode.Manifest,
            IsDefaultSelected: true,
            Diagnostics: Array.Empty<string>());

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName,
        string? assetPath = null,
        string? assetTypeDisplayName = null,
        IReadOnlyList<AssetDependencyDto>? dependencies = null)
    {
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: assetName,
            AssetPath: assetPath ?? $"Assets/{assetName}",
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
            Dependencies: dependencies ?? Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetTypeDisplayName);
    }

    private static AssetPreviewDto CreateSkeletonPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetPreviewKind kind,
        byte[]? binaryContent = null,
        string? textContent = null) =>
        new(
            workspaceId,
            bundleId,
            assetId,
            kind,
            kind == AssetPreviewKind.Text ? "application/json" : "application/octet-stream",
            binaryContent,
            textContent,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: [],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: binaryContent?.Length,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None))
        {
            FullTextContent = textContent
        };

    private sealed class StaticAssetPreviewHandler(AssetPreviewDto preview) :
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>>
    {
        public Task<Result<AssetPreviewDto>> HandleAsync(
            GetAssetPreviewQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetPreviewDto>.Success(preview));
    }

    private sealed class PassthroughReplacementSourcePathExpander : IReplacementSourcePathExpander
    {
        public Task<Result<IReadOnlyList<string>>> ExpandAsync(
            IReadOnlyList<string> sourcePaths,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<string>>.Success(sourcePaths));
    }

    private sealed class StaticReplacementPreviewSourceProvider(
        IReadOnlyDictionary<string, AssetPreviewContentDto> previews) : IReplacementPreviewSourceProvider
    {
        public Task<Result<AssetPreviewContentDto>> LoadFilePreviewAsync(
            AssetDetailsDto targetDetails,
            string sourceFilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(previews.TryGetValue(sourceFilePath, out var preview)
                ? Result<AssetPreviewContentDto>.Success(preview)
                : Result<AssetPreviewContentDto>.Failure(ApplicationErrors.BatchReplacementSourceFileReadFailed(sourceFilePath)));

        public Task<Result<ReplacementTextChunkDto>> LoadFileTextChunkAsync(
            string sourceFilePath,
            int offset,
            int length,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ReplacementTextLineRangeDto>> LoadFileTextLinesAsync(
            string sourceFilePath,
            int startLineIndex,
            int lineCount,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ReplacementTextSearchResultDto>> SearchFileTextAsync(
            string sourceFilePath,
            string searchText,
            int startLineIndex,
            int maxMatchCount,
            bool matchCase,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly TestWorkspaceChangeStore _changeStore;

        public TestWorkspaceDataStoreFactory(TestWorkspaceChangeStore changeStore)
        {
            _changeStore = changeStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => _changeStore;

        public string GetDefaultRootDirectoryPath() => "C:\\WorkspaceData";
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? SavedChangeSet { get; private set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey) => SavedChangeSet;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            SavedChangeSet = changeSet;
        }

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
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

        public AssetId? LastFileReplacementTargetAssetId { get; private set; }

        public List<string> FileReplacementSourceFilePaths { get; } = [];

        public List<AssetId> FileReplacementTargetAssetIds { get; } = [];

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
            RegisterAssetListItem(
                item,
                CreateAssetDetails(
                    CreateWorkspaceId(),
                    item.BundleId,
                    item.AssetId,
                    item.AssetType,
                    item.DisplayName ?? item.Name));
        }

        public void RegisterAssetListItem(AssetListItemDto item, AssetDetailsDto details)
        {
            _bundleIds.Add(item.BundleId.Value);
            if (!_assetLists.TryGetValue(item.BundleId.Value, out var items))
            {
                items = new List<AssetListItemDto>();
                _assetLists[item.BundleId.Value] = items;
            }

            items.Add(item);
            RegisterAssetDetails(details);
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
            LastFileReplacementTargetAssetId = targetAssetId;
            FileReplacementSourceFilePaths.Add(sourceFilePath);
            FileReplacementTargetAssetIds.Add(targetAssetId);
            replacementResult = TryReplaceAssetFromFileResult;
            if (replacementResult is null &&
                _assetDetails.TryGetValue((targetBundleId.Value, targetAssetId.Value), out var details))
            {
                replacementResult = new AssetFileReplacementResultDto(
                    workspaceId,
                    targetBundleId,
                    targetAssetId,
                    details.Name,
                    sourceFilePath,
                    Path.GetFileName(sourceFilePath),
                    sourceAssetType,
                    DateTimeOffset.UtcNow);
            }
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

    private sealed class CancellingSpineAssetGroupsHandler :
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>
    {
        public Task<Result<SpineAssetGroupListResultDto>> HandleAsync(
            LoadSpineAssetGroupsQuery query,
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(cancellationToken);
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
