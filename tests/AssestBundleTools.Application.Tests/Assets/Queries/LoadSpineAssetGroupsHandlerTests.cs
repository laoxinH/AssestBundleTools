using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static class LoadSpineAssetGroupsHandlerTests
{
    public static async Task HandleAsync_ShouldGroupSkeletonAtlasAndWorkspaceTexturePages()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var textureBundleId = new BundleId("bundle.textures");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-hero", "Assets/Hero/Hero.atlas", AssetType.TextAsset));
        var textureAssets = CreateAssetList(
            workspaceId,
            textureBundleId,
            "textures.ab",
            CreateItem(textureBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D),
            CreateItem(textureBundleId, "texture-other", "Assets/Other/Other.png", AssetType.Texture2D));
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets, textureAssets]));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group query should succeed for a loaded bundle.");
        var list = result.Value ?? throw new InvalidOperationException("Expected Spine group list.");
        var group = TestAssert.Single(list.Items);
        TestAssert.Equal("hero", group.DisplayName, "Group display name should come from the normalized asset basename.");
        TestAssert.Equal("assets/hero/hero", group.GroupKey, "Group key should preserve the normalized relative path.");
        TestAssert.Equal(spineBundleId, group.BundleId, "Group identity should stay anchored to the current bundle view.");
        TestAssert.Single(group.SkeletonAssets);
        TestAssert.Single(group.AtlasAssets);
        var texturePage = TestAssert.Single(group.TexturePageAssets);
        TestAssert.Equal(textureBundleId, texturePage.BundleId, "Texture page matching should allow current-workspace assets outside the anchor bundle.");
        TestAssert.Empty(group.Diagnostics, "Complete metadata-level Spine groups should not carry diagnostics.");
    }

    public static async Task HandleAsync_ShouldReportMissingAtlasAndTextureDiagnostics()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-dragon", "Assets/Dragon/Dragon.json", AssetType.TextAsset));
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets]));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group query should still succeed when the group is incomplete.");
        var group = TestAssert.Single(result.Value!.Items);
        TestAssert.True(
            group.Diagnostics.Any(diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.MissingAtlas),
            "Incomplete groups should carry a missing-atlas diagnostic.");
        TestAssert.True(
            group.Diagnostics.Any(diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.MissingTexturePage),
            "Incomplete groups should carry a missing-texture-page diagnostic.");
    }

    private static AssetListResultDto CreateAssetList(
        WorkspaceId workspaceId,
        BundleId bundleId,
        string bundleDisplayName,
        params AssetListItemDto[] items)
    {
        return new AssetListResultDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            BundleDisplayName: bundleDisplayName,
            SearchKeyword: null,
            AssetTypeFilter: null,
            TotalCount: items.Length,
            FilteredCount: items.Length,
            Items: items);
    }

    private static AssetListItemDto CreateItem(
        BundleId bundleId,
        string assetId,
        string assetPath,
        AssetType assetType)
    {
        var assetName = assetPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        return new AssetListItemDto(
            AssetId: new AssetId(assetId),
            BundleId: bundleId,
            Name: assetName,
            AssetPath: assetPath,
            AssetType: assetType,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            HasPreview: true,
            IsModified: false,
            AssetTypeDisplayName: assetType.ToString());
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly WorkspaceId _workspaceId;
        private readonly IReadOnlyDictionary<string, AssetListResultDto> _assetListsByBundleId;

        public TestWorkspaceSessionRegistry(
            WorkspaceId workspaceId,
            IReadOnlyList<AssetListResultDto> assetLists)
        {
            _workspaceId = workspaceId;
            _assetListsByBundleId = assetLists.ToDictionary(
                static list => list.BundleId.Value,
                StringComparer.Ordinal);
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) =>
            throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) =>
            _workspaceId.Equals(workspaceId);

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) =>
            ContainsWorkspace(workspaceId) &&
            _assetListsByBundleId.ContainsKey(bundleId.Value);

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            ContainsBundle(workspaceId, bundleId) &&
            _assetListsByBundleId[bundleId.Value].Items.Any(item => item.AssetId.Equals(assetId));

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            if (!ContainsWorkspace(workspaceId))
            {
                bundleTree = Array.Empty<BundleTreeNodeDto>();
                return false;
            }

            bundleTree = _assetListsByBundleId.Values
                .Select(static list => new BundleTreeNodeDto(
                    BundleId: list.BundleId,
                    DisplayName: list.BundleDisplayName,
                    LogicalPath: list.BundleDisplayName,
                    SourcePath: list.BundleDisplayName,
                    IsVirtualFolder: false,
                    AssetCount: list.TotalCount,
                    Children: Array.Empty<BundleTreeNodeDto>()))
                .ToArray();
            return true;
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult)
        {
            if (!ContainsWorkspace(workspaceId))
            {
                assetListResult = null;
                return false;
            }

            return _assetListsByBundleId.TryGetValue(bundleId.Value, out assetListResult);
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            assetDetails = null;
            return false;
        }

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = null;
            return false;
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            extractionResult = null;
            return false;
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
        {
            candidates = Array.Empty<WorkspaceReplacementCandidateDto>();
            return false;
        }

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            return false;
        }

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            return false;
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            undoResult = null;
            return false;
        }

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext)
        {
            paneContext = null;
            return false;
        }
    }
}
