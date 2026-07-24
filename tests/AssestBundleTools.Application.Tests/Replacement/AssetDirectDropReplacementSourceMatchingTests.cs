using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Replacement;

internal static class AssetDirectDropReplacementSourceMatchingTests
{
    public static async Task DirectDrop_ShouldIgnoreFileNameAndTargetOnlyExactAsset()
    {
        var workspaceId = AssetReplacementSourceMatchingTests.CreateWorkspaceId();
        var bundleId = new BundleId("bundle.assets");
        var hero = CreateItem(bundleId, "asset.hero", "hero", AssetType.Texture2D);
        var portrait = CreateItem(bundleId, "asset.portrait", "portrait", AssetType.Texture2D);
        var source = @"D:\Replacement\renamed-image.png";
        var handler = AssetReplacementSourceMatchingTests.CreateHandler(
            new AssetReplacementSourceMatchingTests.AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [hero, portrait],
            [],
            [source],
            portrait));

        TestAssert.True(result.IsSuccess, "A direct compatible file should produce a match plan.");
        var match = TestAssert.Single(result.Value?.Matches ?? []);
        TestAssert.Equal(portrait.AssetId, match.Target.AssetId, "The exact dropped row should remain the target.");
        TestAssert.Equal(source, match.SourceFilePath, "The renamed image should not require a target-name match.");
        TestAssert.Equal(0, result.Value?.Conflicts.Count ?? -1, "A direct target should not conflict with sibling rows.");
    }

    public static async Task DirectDrop_ShouldRejectIncompatibleExtension()
    {
        var workspaceId = AssetReplacementSourceMatchingTests.CreateWorkspaceId();
        var target = CreateItem(new BundleId("bundle.assets"), "asset.hero", "hero", AssetType.Texture2D);
        var handler = AssetReplacementSourceMatchingTests.CreateHandler(
            new AssetReplacementSourceMatchingTests.AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [target],
            [],
            [@"D:\Replacement\renamed-audio.wav"],
            target));

        TestAssert.True(result.IsSuccess, "An incompatible direct file should return an empty plan.");
        TestAssert.Equal(0, result.Value?.Matches.Count ?? -1, "An audio source must not replace a texture target.");
    }

    public static async Task DirectDrop_ShouldAcceptAtlasTextSource()
    {
        var workspaceId = AssetReplacementSourceMatchingTests.CreateWorkspaceId();
        var bundleId = new BundleId("bundle.spine");
        var target = CreateItem(bundleId, "asset.atlas", "hero.atlas", AssetType.TextAsset);
        var source = @"D:\Replacement\edited-layout.txt";
        var group = new SpineAssetGroupDto(
            "spine-hero",
            bundleId,
            "hero",
            "hero",
            [],
            [CreatePart(SpineAssetGroupPartRole.Atlas, target)],
            [],
            []);
        var handler = AssetReplacementSourceMatchingTests.CreateHandler(
            new AssetReplacementSourceMatchingTests.AssetDetailsRegistry());

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [target],
            [group],
            [source],
            target));

        var match = TestAssert.Single(result.Value?.Matches ?? []);
        TestAssert.Equal(source, match.SourceFilePath, "An atlas row should accept a renamed .txt source.");
    }

    public static async Task DirectDrop_ShouldValidateSkeletonTextSourceByPayload()
    {
        var workspaceId = AssetReplacementSourceMatchingTests.CreateWorkspaceId();
        var bundleId = new BundleId("bundle.spine");
        var target = CreateItem(bundleId, "asset.skeleton", "hero.skel", AssetType.TextAsset);
        var source = @"D:\Replacement\edited-skeleton.txt";
        var incompatibleSource = @"D:\Replacement\edited-skeleton-json.txt";
        var details = AssetReplacementSourceMatchingTests.CreateAssetDetails(workspaceId, bundleId, target.AssetId);
        var compatibilityService = new SpineSkeletonReplacementCompatibilityService(
            new AssetReplacementSourceMatchingTests.AssetDetailsRegistry(details),
            new AssetReplacementSourceMatchingTests.StaticAssetPreviewHandler(
                AssetReplacementSourceMatchingTests.CreateSkeletonPreview(workspaceId, bundleId, target.AssetId)),
            new AssetReplacementSourceMatchingTests.StaticReplacementPreviewSourceProvider(
                new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase)
                {
                    [source] = new(AssetPreviewKind.Binary, "application/octet-stream", [0x1c, 0x00]),
                    [incompatibleSource] = new(
                        AssetPreviewKind.Text,
                        "application/json",
                        "{\"bones\":[]}"u8.ToArray())
                }));
        var handler = new MatchAssetReplacementSourcesHandler(
            new AssetReplacementSourceMatchingTests.PassthroughReplacementSourcePathExpander(),
            compatibilityService);
        var group = new SpineAssetGroupDto(
            "spine-hero",
            bundleId,
            "hero",
            "hero",
            [CreatePart(SpineAssetGroupPartRole.Skeleton, target)],
            [],
            [],
            []);

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [target],
            [group],
            [source],
            target));

        var match = TestAssert.Single(result.Value?.Matches ?? []);
        TestAssert.True(match.RequireSpineSkeletonFormatMatch, "Skeleton application should revalidate the real payload format.");

        var incompatibleResult = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [target],
            [group],
            [incompatibleSource],
            target));
        TestAssert.Equal(
            0,
            incompatibleResult.Value?.Matches.Count ?? -1,
            "A .txt extension must not bypass binary-versus-JSON skeleton compatibility.");
    }

    public static async Task SingleDirectoryResult_ShouldKeepBatchNameMatching()
    {
        var workspaceId = AssetReplacementSourceMatchingTests.CreateWorkspaceId();
        var bundleId = new BundleId("bundle.assets");
        var hero = CreateItem(bundleId, "asset.hero", "hero", AssetType.Texture2D);
        var portrait = CreateItem(bundleId, "asset.portrait", "portrait", AssetType.Texture2D);
        var directory = @"D:\Replacement\Folder";
        var expandedFile = @"D:\Replacement\Folder\hero.png";
        var compatibilityService = new SpineSkeletonReplacementCompatibilityService(
            new AssetReplacementSourceMatchingTests.AssetDetailsRegistry(),
            new AssetReplacementSourceMatchingTests.StaticAssetPreviewHandler(null!),
            new AssetReplacementSourceMatchingTests.StaticReplacementPreviewSourceProvider(
                new Dictionary<string, AssetPreviewContentDto>(StringComparer.OrdinalIgnoreCase)));
        var handler = new MatchAssetReplacementSourcesHandler(
            new MappedSourcePathExpander(expandedFile),
            compatibilityService);

        var result = await handler.HandleAsync(new MatchAssetReplacementSourcesQuery(
            workspaceId,
            "workspace-key",
            @"D:\WorkspaceData",
            [hero, portrait],
            [],
            [directory],
            portrait));

        var match = TestAssert.Single(result.Value?.Matches ?? []);
        TestAssert.Equal(hero.AssetId, match.Target.AssetId, "A directory must use batch file-name matching even when it expands to one file.");
    }

    private static AssetListItemDto CreateItem(
        BundleId bundleId,
        string assetId,
        string name,
        AssetType assetType) =>
        AssetReplacementSourceMatchingTests.CreateAssetListItem(
            bundleId,
            new AssetId(assetId),
            name,
            assetType,
            exportFileName: name);

    private static SpineAssetGroupPartDto CreatePart(
        SpineAssetGroupPartRole role,
        AssetListItemDto target) =>
        new(
            role,
            target.BundleId,
            target.AssetId,
            "bundle.ab",
            target.Name,
            target.AssetPath,
            target.AssetType,
            target.AssetType.ToString());

    private sealed class MappedSourcePathExpander(string expandedFile) : IReplacementSourcePathExpander
    {
        public Task<Result<IReadOnlyList<string>>> ExpandAsync(
            IReadOnlyList<string> sourcePaths,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<string>>.Success([expandedFile]));
    }
}
