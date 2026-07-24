using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
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

internal static class AssetReplacementSourceMatchingTests
{
    public static async Task MatchAssetReplacementSourcesHandler_ShouldMatchMixedSourcesAcrossCurrentList()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.assets");
        var texture = CreateAssetListItem(bundleId, new AssetId("asset.texture"), "hero", AssetType.Texture2D);
        var audio = CreateAssetListItem(bundleId, new AssetId("asset.audio"), "voice", AssetType.AudioClip);
        var handler = CreateHandler(new AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [texture, audio],
            [],
            [@"D:\Replacement\hero.png", @"D:\Replacement\voice.ogg", @"D:\Replacement\readme.txt"]));

        TestAssert.True(result.IsSuccess, "Mixed ordinary replacement sources should produce a match plan.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected ordinary replacement source match plan.");
        TestAssert.Equal(2, plan.Matches.Count, "Every compatible file name in the current list should be matched.");
        TestAssert.Equal(0, plan.Conflicts.Count, "Distinct file names should not create conflicts.");
        TestAssert.True(
            plan.Matches.Any(match => match.Target.AssetId.Equals(texture.AssetId) && match.SourceFilePath.EndsWith("hero.png", StringComparison.Ordinal)),
            "The texture source should map to the texture target.");
        TestAssert.True(
            plan.Matches.Any(match => match.Target.AssetId.Equals(audio.AssetId) && match.SourceFilePath.EndsWith("voice.ogg", StringComparison.Ordinal)),
            "The audio source should map to the audio target.");
    }

    public static async Task MatchAssetReplacementSourcesHandler_ShouldMatchAllTypesAndOrdinalDisplayNames()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.mixed");
        var firstAudio = CreateAssetListItem(
            bundleId,
            new AssetId("asset.audio.1"),
            "shared_voice",
            AssetType.AudioClip,
            "shared_voice [1]",
            "shared_voice [1].wav");
        var secondAudio = CreateAssetListItem(
            bundleId,
            new AssetId("asset.audio.2"),
            "shared_voice",
            AssetType.AudioClip,
            "shared_voice [2]",
            "shared_voice [2].wav");
        var texture = CreateAssetListItem(bundleId, new AssetId("asset.texture"), "shared", AssetType.Texture2D, exportFileName: "shared.png");
        var audio = CreateAssetListItem(bundleId, new AssetId("asset.audio"), "shared", AssetType.AudioClip, exportFileName: "shared.wav");
        var text = CreateAssetListItem(bundleId, new AssetId("asset.text"), "config.atlas", AssetType.TextAsset, exportFileName: "config.atlas");
        var mesh = CreateAssetListItem(bundleId, new AssetId("asset.mesh"), "body", AssetType.Mesh, exportFileName: "body.obj");
        var handler = CreateHandler(new AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [firstAudio, secondAudio, texture, audio, text, mesh],
            [],
            [
                @"D:\Replacement\shared_voice [1].wav",
                @"D:\Replacement\shared_voice [2].ogg",
                @"D:\Replacement\shared.png",
                @"D:\Replacement\shared.wav",
                @"D:\Replacement\config.atlas",
                @"D:\Replacement\body.obj"
            ]));

        TestAssert.True(result.IsSuccess, "The all-types list should produce a match plan.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected all-types source match plan.");
        TestAssert.Equal(6, plan.Matches.Count, "Every supported type and ordinal source should match exactly once.");
        TestAssert.Equal(0, plan.Conflicts.Count, "Type extensions and ordinal names should remove ambiguity.");
        TestAssert.Equal(
            firstAudio.AssetId,
            TestAssert.Single(plan.Matches.Where(static match => match.SourceFileName == "shared_voice [1].wav").ToArray()).Target.AssetId,
            "The [1] source should map to the first projected duplicate.");
        TestAssert.Equal(
            secondAudio.AssetId,
            TestAssert.Single(plan.Matches.Where(static match => match.SourceFileName == "shared_voice [2].ogg").ToArray()).Target.AssetId,
            "The [2] source should map to the second projected duplicate even when the compatible audio extension changes.");
        TestAssert.Equal(
            texture.AssetId,
            TestAssert.Single(plan.Matches.Where(static match => match.SourceFileName == "shared.png").ToArray()).Target.AssetId,
            "The PNG source should map only to the texture target.");
        TestAssert.Equal(
            audio.AssetId,
            TestAssert.Single(plan.Matches.Where(static match => match.SourceFileName == "shared.wav").ToArray()).Target.AssetId,
            "The WAV source should map only to the audio target with the same base name.");
    }

    public static async Task MatchAssetReplacementSourcesHandler_ShouldConflictForSharedImageTypesWithoutOrdinalName()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.shared-image");
        var texture = CreateAssetListItem(bundleId, new AssetId("asset.texture"), "shared", AssetType.Texture2D, exportFileName: "shared.png");
        var sprite = CreateAssetListItem(bundleId, new AssetId("asset.sprite"), "shared", AssetType.Sprite, exportFileName: "shared.png");
        var source = @"D:\Replacement\shared.png";
        var handler = CreateHandler(new AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [texture, sprite],
            [],
            [source]));

        TestAssert.True(result.IsSuccess, "Shared image extensions should produce a structured plan.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected shared-image source match plan.");
        TestAssert.Equal(0, plan.Matches.Count, "One PNG must not be applied to both Texture2D and Sprite silently.");
        TestAssert.Equal(2, plan.Conflicts.Count, "Both ambiguous image targets should be listed in the conflict details.");
        TestAssert.True(
            plan.Conflicts.All(conflict => conflict.SourceFilePaths.SequenceEqual([source])),
            "Each shared-image conflict should identify the ambiguous PNG source.");
    }

    public static async Task MatchAssetReplacementSourcesHandler_ShouldReportEveryConflictingSource()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.assets");
        var texture = CreateAssetListItem(bundleId, new AssetId("asset.texture"), "hero", AssetType.Texture2D);
        var first = @"D:\Replacement\A\hero.png";
        var second = @"D:\Replacement\B\hero.jpg";
        var handler = CreateHandler(new AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [texture],
            [],
            [first, second]));

        TestAssert.True(result.IsSuccess, "Ambiguous ordinary sources should be represented by a structured plan.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected ordinary replacement source match plan.");
        TestAssert.Equal(0, plan.Matches.Count, "A conflicted target must not also produce an executable match.");
        var conflict = TestAssert.Single(plan.Conflicts);
        TestAssert.Equal(2, conflict.SourceFilePaths.Count, "Conflict details should retain every matching source path.");
        TestAssert.True(conflict.SourceFilePaths.Contains(first), "Conflict details should contain the first source.");
        TestAssert.True(conflict.SourceFilePaths.Contains(second), "Conflict details should contain the second source.");
    }

    public static async Task MatchAssetReplacementSourcesHandler_ShouldReportOneSourceMatchingDuplicateTargets()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.assets");
        var first = CreateAssetListItem(
            bundleId,
            new AssetId("asset.first"),
            "hero",
            AssetType.Texture2D,
            "hero [1]",
            "hero [1].png");
        var second = CreateAssetListItem(
            bundleId,
            new AssetId("asset.second"),
            "hero",
            AssetType.Texture2D,
            "hero [2]",
            "hero [2].png");
        var source = @"D:\Replacement\hero.png";
        var handler = CreateHandler(new AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [first, second],
            [],
            [source]));

        TestAssert.True(result.IsSuccess, "An unnumbered source should conflict with ordinal duplicate targets.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected ordinary replacement source match plan.");
        TestAssert.Equal(0, plan.Matches.Count, "One source must not be applied silently to multiple targets.");
        TestAssert.Equal(2, plan.Conflicts.Count, "Every ambiguous target should be included in conflict details.");
        TestAssert.True(
            plan.Conflicts.All(conflict => conflict.SourceFilePaths.SequenceEqual([source])),
            "Each ambiguous target should identify the shared source file.");
    }

    public static async Task MatchAssetReplacementSourcesHandler_ShouldUsePayloadFormatForSkeletonTargets()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.spine");
        var skeletonAssetId = new AssetId("asset.skeleton");
        var skeleton = CreateAssetListItem(bundleId, skeletonAssetId, "hero", AssetType.TextAsset);
        var registry = new AssetDetailsRegistry(CreateAssetDetails(workspaceId, bundleId, skeletonAssetId));
        var jsonNamedSkel = @"D:\Replacement\hero.skel";
        var binaryNamedJson = @"D:\Replacement\hero.json";
        var sameStemAtlas = @"D:\Replacement\hero.atlas";
        var sourceProvider = new StaticReplacementPreviewSourceProvider(new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase)
        {
            [jsonNamedSkel] = new(AssetPreviewKind.Text, "application/json", Encoding.UTF8.GetBytes("{\"bones\":[]}")),
            [binaryNamedJson] = new(AssetPreviewKind.Binary, "application/octet-stream", [0x1c, 0x00])
        });
        var compatibilityService = new SpineSkeletonReplacementCompatibilityService(
            registry,
            new StaticAssetPreviewHandler(CreateSkeletonPreview(workspaceId, bundleId, skeletonAssetId)),
            sourceProvider);
        var group = new SpineAssetGroupDto(
            "spine-hero",
            bundleId,
            "hero",
            "hero",
            [new SpineAssetGroupPartDto(
                SpineAssetGroupPartRole.Skeleton,
                bundleId,
                skeletonAssetId,
                "bundle.spine.ab",
                "hero.skel",
                "Assets/Spine/hero.skel",
                AssetType.TextAsset,
                "TextAsset")],
            [],
            [],
            []);
        var handler = new MatchAssetReplacementSourcesHandler(
            new PassthroughReplacementSourcePathExpander(),
            compatibilityService,
            new StaticSpineGroupListHandler(group));

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [skeleton],
            [],
            [jsonNamedSkel, binaryNamedJson, sameStemAtlas]));

        TestAssert.True(result.IsSuccess, "Skeleton ordinary-row matching should succeed.");
        var match = TestAssert.Single(result.Value?.Matches ?? []);
        TestAssert.Equal(binaryNamedJson, match.SourceFilePath, "Real binary content should win regardless of the .json suffix.");
        TestAssert.True(match.RequireSpineSkeletonFormatMatch, "Applying the match should revalidate skeleton payload compatibility.");
        TestAssert.Equal(0, result.Value?.Conflicts.Count ?? -1, "An incompatible JSON payload must not create a false conflict.");
    }

    public static async Task MatchAssetReplacementSourcesHandler_ShouldFailWhenSpineCatalogCannotLoad()
    {
        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle.assets");
        var target = CreateAssetListItem(bundleId, new AssetId("asset.text"), "hero", AssetType.TextAsset);
        var compatibilityService = new SpineSkeletonReplacementCompatibilityService(
            new AssetDetailsRegistry(),
            new StaticAssetPreviewHandler(null!),
            new StaticReplacementPreviewSourceProvider(
                new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase)));
        var handler = new MatchAssetReplacementSourcesHandler(
            new PassthroughReplacementSourcePathExpander(),
            compatibilityService,
            new FailedSpineGroupListHandler());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [target],
            [],
            [@"D:\Replacement\hero.skel"]));

        TestAssert.True(result.IsFailure, "A failed Spine catalog load must not bypass skeleton payload validation.");
        TestAssert.Equal("Asset.Replace.BatchSourceFileReadFailed", TestAssert.Single(result.Errors).Code, "The catalog failure should be preserved.");
    }

    private static WorkspaceId CreateWorkspaceId() => new("workspace-session-1");

    private static MatchAssetReplacementSourcesHandler CreateHandler(IWorkspaceSessionRegistry registry) =>
        new(
            new PassthroughReplacementSourcePathExpander(),
            new SpineSkeletonReplacementCompatibilityService(
                registry,
                new StaticAssetPreviewHandler(null!),
                new StaticReplacementPreviewSourceProvider(
                    new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase))));

    private static AssetListItemDto CreateAssetListItem(
        BundleId bundleId,
        AssetId assetId,
        string name,
        AssetType assetType,
        string? displayName = null,
        string? exportFileName = null) =>
        new(
            assetId,
            bundleId,
            name,
            $"Assets/{name}",
            assetType,
            128,
            128,
            HasPreview: true,
            IsModified: false,
            DisplayName: displayName ?? name,
            ExportFileName: exportFileName ?? name,
            CanExtract: true);

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId) =>
        new(
            workspaceId,
            bundleId,
            assetId,
            "hero.skel",
            "Assets/Spine/hero.skel",
            string.Empty,
            AssetType.TextAsset,
            256,
            256,
            12345,
            false,
            true,
            true,
            true,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            []);

    private static AssetPreviewDto CreateSkeletonPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId) =>
        new(
            workspaceId,
            bundleId,
            assetId,
            AssetPreviewKind.Binary,
            "application/octet-stream",
            [0x1c, 0x00],
            null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: [],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: 2,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));

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

    private sealed class StaticSpineGroupListHandler(SpineAssetGroupDto group) :
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>
    {
        public Task<Result<SpineAssetGroupListResultDto>> HandleAsync(
            LoadSpineAssetGroupsQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<SpineAssetGroupListResultDto>.Success(
                new SpineAssetGroupListResultDto(
                    query.WorkspaceId,
                    query.BundleId,
                    "Bundle",
                    query.SearchKeyword,
                    1,
                    1,
                    [group])));
    }

    private sealed class FailedSpineGroupListHandler :
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>
    {
        public Task<Result<SpineAssetGroupListResultDto>> HandleAsync(
            LoadSpineAssetGroupsQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<SpineAssetGroupListResultDto>.Failure(
                ApplicationErrors.BatchReplacementSourceFileReadFailed("spine-catalog")));
    }

    private sealed class AssetDetailsRegistry(AssetDetailsDto? details = null) : IWorkspaceSessionRegistry
    {
        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            assetDetails = details;
            return details is not null &&
                   details.WorkspaceId.Equals(workspaceId) &&
                   details.BundleId.Equals(bundleId) &&
                   details.AssetId.Equals(assetId);
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) => throw new NotSupportedException();
        public bool ContainsWorkspace(WorkspaceId workspaceId) => throw new NotSupportedException();
        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => throw new NotSupportedException();
        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) => throw new NotSupportedException();
        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree) => throw new NotSupportedException();
        public bool TryGetAssetList(WorkspaceId workspaceId, BundleId bundleId, string? searchKeyword, AssetType? assetTypeFilter, string? assetTypeDisplayNameFilter, out AssetListResultDto? assetListResult) => throw new NotSupportedException();
        public bool TryGetWorkspaceChangeSummary(WorkspaceId workspaceId, out WorkspaceChangeSummaryDto? changeSummary) => throw new NotSupportedException();
        public bool TryExtractAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId, out AssetExtractionResultDto? extractionResult) => throw new NotSupportedException();
        public bool TryGetWorkspaceReplacementCandidates(WorkspaceId workspaceId, BundleId targetBundleId, AssetId targetAssetId, out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates) => throw new NotSupportedException();
        public bool TryReplaceAssetFromFile(WorkspaceId workspaceId, BundleId targetBundleId, AssetId targetAssetId, string sourceFilePath, AssetType sourceAssetType, out AssetFileReplacementResultDto? replacementResult) => throw new NotSupportedException();
        public bool TryReplaceAssetFromWorkspace(WorkspaceId workspaceId, BundleId targetBundleId, AssetId targetAssetId, BundleId sourceBundleId, AssetId sourceAssetId, out AssetReplacementResultDto? replacementResult) => throw new NotSupportedException();
        public bool TryUndoAssetReplacement(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId, out AssetReplacementUndoResultDto? undoResult) => throw new NotSupportedException();
        public bool TryOpenAssetInPane(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId, PaneId paneId, out AssetPaneContextDto? paneContext) => throw new NotSupportedException();
    }
}
