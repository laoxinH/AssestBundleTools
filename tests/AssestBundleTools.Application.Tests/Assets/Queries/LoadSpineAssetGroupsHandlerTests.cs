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

internal static partial class LoadSpineAssetGroupsHandlerTests
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

    public static async Task HandleAsync_ShouldExposePartExtractionState()
    {
        var extractedAtUtc = new DateTimeOffset(2026, 5, 12, 8, 30, 0, TimeSpan.Zero);
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(
                spineBundleId,
                "skeleton-hero",
                "Assets/Hero/Hero.skel",
                AssetType.TextAsset,
                lastExtractedAtUtc: extractedAtUtc),
            CreateItem(spineBundleId, "atlas-hero", "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets]));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group query should carry asset list change metadata.");
        var group = TestAssert.Single(result.Value!.Items);
        var skeleton = TestAssert.Single(group.SkeletonAssets);
        TestAssert.Equal(
            extractedAtUtc,
            skeleton.LastExtractedAtUtc,
            "Spine part metadata should retain the resource-level extraction timestamp.");
    }

    public static async Task HandleAsync_ShouldSplitDuplicateSpineResourcesUsingDependencyLinks()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skel-a", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, "skel-b", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-a", "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-b", "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-illustration", "Assets/Hero/Hero.png", AssetType.Texture2D),
            CreateItem(spineBundleId, "texture-a", "Assets/Hero/Hero.png", AssetType.Texture2D),
            CreateItem(spineBundleId, "texture-b", "Assets/Hero/Hero.png", AssetType.Texture2D),
            CreateItem(spineBundleId, "alpha-a", "Assets/Hero/Hero[alpha]", AssetType.Texture2D),
            CreateItem(spineBundleId, "alpha-b", "Assets/Hero/Hero[alpha]", AssetType.Texture2D),
            CreateItem(spineBundleId, "material-a", "Assets/Hero/Hero_Material", AssetType.Material),
            CreateItem(spineBundleId, "material-b", "Assets/Hero/Hero_Material", AssetType.Material),
            CreateItem(spineBundleId, "atlas-object-a", "Assets/Hero/Hero_Atlas", AssetType.Unknown, "MonoBehaviour"),
            CreateItem(spineBundleId, "atlas-object-b", "Assets/Hero/Hero_Atlas", AssetType.Unknown, "MonoBehaviour"),
            CreateItem(spineBundleId, "skeleton-data-a", "Assets/Hero/Hero_SkeletonData", AssetType.Unknown, "MonoBehaviour"),
            CreateItem(spineBundleId, "skeleton-data-b", "Assets/Hero/Hero_SkeletonData", AssetType.Unknown, "MonoBehaviour"));
        var details = new[]
        {
            CreateDetails(workspaceId, spineBundleId, "skeleton-data-a", "Assets/Hero/Hero_SkeletonData", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-object-a", "Hero_Atlas", AssetType.Unknown, "MonoBehaviour"),
                CreateDependency(spineBundleId, "skel-a", "Hero.skel", AssetType.TextAsset)
            ]),
            CreateDetails(workspaceId, spineBundleId, "skeleton-data-b", "Assets/Hero/Hero_SkeletonData", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-object-b", "Hero_Atlas", AssetType.Unknown, "MonoBehaviour"),
                CreateDependency(spineBundleId, "skel-b", "Hero.skel", AssetType.TextAsset)
            ]),
            CreateDetails(workspaceId, spineBundleId, "atlas-object-a", "Assets/Hero/Hero_Atlas", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-a", "Hero.atlas", AssetType.TextAsset),
                CreateDependency(spineBundleId, "material-a", "Hero_Material", AssetType.Material)
            ]),
            CreateDetails(workspaceId, spineBundleId, "atlas-object-b", "Assets/Hero/Hero_Atlas", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-b", "Hero.atlas", AssetType.TextAsset),
                CreateDependency(spineBundleId, "material-b", "Hero_Material", AssetType.Material)
            ]),
            CreateDetails(workspaceId, spineBundleId, "material-a", "Assets/Hero/Hero_Material", AssetType.Material, "Material",
            [
                CreateDependency(spineBundleId, "texture-a", "Hero", AssetType.Texture2D),
                CreateDependency(spineBundleId, "alpha-a", "Hero[alpha]", AssetType.Texture2D)
            ]),
            CreateDetails(workspaceId, spineBundleId, "material-b", "Assets/Hero/Hero_Material", AssetType.Material, "Material",
            [
                CreateDependency(spineBundleId, "texture-b", "Hero", AssetType.Texture2D),
                CreateDependency(spineBundleId, "alpha-b", "Hero[alpha]", AssetType.Texture2D)
            ])
        };
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets],
            details));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Linked duplicate Spine groups should still load successfully.");
        var groups = result.Value!.Items;
        TestAssert.Equal(2, groups.Count, "Dependency-linked duplicate skeleton/atlas pairs should be split into separate groups: " + FormatGroupSummary(groups));
        foreach (var group in groups)
        {
            TestAssert.Single(group.SkeletonAssets);
            TestAssert.Single(group.AtlasAssets);
            TestAssert.Equal(2, group.TexturePageAssets.Count, "Each linked group should keep its color texture and alpha texture.");
            TestAssert.False(
                group.TexturePageAssets.Any(static part => part.AssetId.Value == "texture-illustration"),
                "Loose same-name Texture2D assets outside the Spine dependency chain should not be used as atlas pages.");
            TestAssert.False(
                group.Diagnostics.Any(static diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.MultipleSkeletons),
                "Linked pairs should not be reported as one ambiguous multi-skeleton group.");
            TestAssert.False(
                group.Diagnostics.Any(static diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.MultipleAtlases),
                "Linked pairs should not be reported as one ambiguous multi-atlas group.");
        }

        var textureSets = groups
            .Select(static group => string.Join(
                ",",
                group.TexturePageAssets
                    .Select(static part => part.AssetId.Value)
                    .OrderBy(static assetId => assetId, StringComparer.Ordinal)))
            .OrderBy(static textureSet => textureSet, StringComparer.Ordinal)
            .ToArray();
        TestAssert.Equal("alpha-a,texture-a", textureSets[0], "The first linked group should keep the material-specific texture pair.");
        TestAssert.Equal("alpha-b,texture-b", textureSets[1], "The second linked group should keep the material-specific texture pair.");
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
        AssetType assetType,
        string? assetTypeDisplayName = null,
        DateTimeOffset? lastExtractedAtUtc = null)
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
            AssetTypeDisplayName: assetTypeDisplayName ?? assetType.ToString(),
            LastExtractedAtUtc: lastExtractedAtUtc);
    }

    private static AssetDetailsDto CreateDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        string assetId,
        string assetPath,
        AssetType assetType,
        string? assetTypeDisplayName,
        IReadOnlyList<AssetDependencyDto> dependencies)
    {
        var assetName = assetPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: new AssetId(assetId),
            Name: assetName,
            AssetPath: assetPath,
            ContainerPath: assetPath,
            AssetType: assetType,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            PathId: 0,
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
            Dependencies: dependencies,
            AssetTypeDisplayName: assetTypeDisplayName ?? assetType.ToString());
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
            AssetTypeDisplayName: assetTypeDisplayName ?? assetType.ToString());

    private static string FormatGroupSummary(IReadOnlyList<SpineAssetGroupDto> groups) =>
        string.Join(
            " | ",
            groups.Select(static group =>
                string.Concat(
                    group.GroupId,
                    " sk=",
                    string.Join(",", group.SkeletonAssets.Select(static part => part.AssetId.Value)),
                    " at=",
                    string.Join(",", group.AtlasAssets.Select(static part => part.AssetId.Value)),
                    " tx=",
                    string.Join(",", group.TexturePageAssets.Select(static part => part.AssetId.Value)))));

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly WorkspaceId _workspaceId;
        private readonly IReadOnlyDictionary<string, AssetListResultDto> _assetListsByBundleId;
        private readonly IReadOnlyDictionary<string, AssetDetailsDto> _assetDetailsByAssetKey;

        public TestWorkspaceSessionRegistry(
            WorkspaceId workspaceId,
            IReadOnlyList<AssetListResultDto> assetLists,
            IReadOnlyList<AssetDetailsDto>? assetDetails = null)
        {
            _workspaceId = workspaceId;
            _assetListsByBundleId = assetLists.ToDictionary(
                static list => list.BundleId.Value,
                StringComparer.Ordinal);
            _assetDetailsByAssetKey = (assetDetails ?? Array.Empty<AssetDetailsDto>())
                .ToDictionary(
                    static details => CreateAssetKey(details.BundleId, details.AssetId),
                    StringComparer.OrdinalIgnoreCase);
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
            if (!ContainsWorkspace(workspaceId))
            {
                assetDetails = null;
                return false;
            }

            return _assetDetailsByAssetKey.TryGetValue(
                CreateAssetKey(bundleId, assetId),
                out assetDetails);
        }

        private static string CreateAssetKey(BundleId bundleId, AssetId assetId) =>
            string.Concat(bundleId.Value, "\n", assetId.Value);

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
