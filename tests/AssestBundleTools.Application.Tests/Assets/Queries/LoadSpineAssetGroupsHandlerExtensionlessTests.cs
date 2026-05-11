using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class LoadSpineAssetGroupsHandlerTests
{
    public static async Task HandleAsync_ShouldMatchExtensionlessJsonSkeleton_WithSameNameAtlas()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "prefabs_spine_50250_skin_ccc04_spine",
            CreateItem(spineBundleId, "skeleton-ccc04", "CAB-6d6656f47018463c1fb821957721a1ed/50250_skin_CCC04", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-ccc04", "CAB-6d6656f47018463c1fb821957721a1ed/50250_skin_CCC04.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-ccc04", "CAB-6d6656f47018463c1fb821957721a1ed/50250_skin_CCC04", AssetType.Texture2D));
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets]));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Extensionless JSON skeleton resources should still be grouped with their atlas.");
        var group = TestAssert.Single(result.Value!.Items);
        TestAssert.Equal(
            "cab-6d6656f47018463c1fb821957721a1ed/50250_skin_ccc04",
            group.GroupKey,
            "The extensionless skeleton should use the atlas basename as the group key.");
        TestAssert.Single(group.SkeletonAssets);
        TestAssert.Single(group.AtlasAssets);
        TestAssert.Single(group.TexturePageAssets);
        TestAssert.False(
            group.Diagnostics.Any(static diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.MissingSkeleton),
            "The atlas-only fallback group should be upgraded with the extensionless skeleton.");
    }

    public static async Task HandleAsync_ShouldClassifyExtensionlessDuplicateSpineParts_UsingDependencyLinks()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skel-a", "Assets/Hero/Hero", AssetType.TextAsset),
            CreateItem(spineBundleId, "skel-b", "Assets/Hero/Hero", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-a", "Assets/Hero/HeroAtlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-b", "Assets/Hero/HeroAtlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-illustration", "Assets/Hero/Hero.png", AssetType.Texture2D),
            CreateItem(spineBundleId, "texture-a", "Assets/Hero/HeroPageA", AssetType.Texture2D),
            CreateItem(spineBundleId, "texture-b", "Assets/Hero/HeroPageB", AssetType.Texture2D),
            CreateItem(spineBundleId, "material-a", "Assets/Hero/Hero_Material_A", AssetType.Material),
            CreateItem(spineBundleId, "material-b", "Assets/Hero/Hero_Material_B", AssetType.Material),
            CreateItem(spineBundleId, "atlas-object-a", "Assets/Hero/Hero_Atlas_A", AssetType.Unknown, "MonoBehaviour"),
            CreateItem(spineBundleId, "atlas-object-b", "Assets/Hero/Hero_Atlas_B", AssetType.Unknown, "MonoBehaviour"),
            CreateItem(spineBundleId, "skeleton-data-a", "Assets/Hero/Hero_SkeletonData_A", AssetType.Unknown, "MonoBehaviour"),
            CreateItem(spineBundleId, "skeleton-data-b", "Assets/Hero/Hero_SkeletonData_B", AssetType.Unknown, "MonoBehaviour"));
        var details = new[]
        {
            CreateDetails(workspaceId, spineBundleId, "skeleton-data-a", "Assets/Hero/Hero_SkeletonData_A", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-object-a", "Hero_Atlas_A", AssetType.Unknown, "MonoBehaviour"),
                CreateDependency(spineBundleId, "skel-a", "Hero", AssetType.TextAsset)
            ]),
            CreateDetails(workspaceId, spineBundleId, "skeleton-data-b", "Assets/Hero/Hero_SkeletonData_B", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-object-b", "Hero_Atlas_B", AssetType.Unknown, "MonoBehaviour"),
                CreateDependency(spineBundleId, "skel-b", "Hero", AssetType.TextAsset)
            ]),
            CreateDetails(workspaceId, spineBundleId, "atlas-object-a", "Assets/Hero/Hero_Atlas_A", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-a", "HeroAtlas", AssetType.TextAsset),
                CreateDependency(spineBundleId, "material-a", "Hero_Material_A", AssetType.Material)
            ]),
            CreateDetails(workspaceId, spineBundleId, "atlas-object-b", "Assets/Hero/Hero_Atlas_B", AssetType.Unknown, "MonoBehaviour",
            [
                CreateDependency(spineBundleId, "atlas-b", "HeroAtlas", AssetType.TextAsset),
                CreateDependency(spineBundleId, "material-b", "Hero_Material_B", AssetType.Material)
            ]),
            CreateDetails(workspaceId, spineBundleId, "material-a", "Assets/Hero/Hero_Material_A", AssetType.Material, "Material",
            [
                CreateDependency(spineBundleId, "texture-a", "HeroPageA", AssetType.Texture2D)
            ]),
            CreateDetails(workspaceId, spineBundleId, "material-b", "Assets/Hero/Hero_Material_B", AssetType.Material, "Material",
            [
                CreateDependency(spineBundleId, "texture-b", "HeroPageB", AssetType.Texture2D)
            ])
        };
        var handler = new LoadSpineAssetGroupsHandler(new TestWorkspaceSessionRegistry(
            workspaceId,
            [spineAssets],
            details));

        var result = await handler.HandleAsync(new LoadSpineAssetGroupsQuery(
            workspaceId,
            spineBundleId));

        TestAssert.True(result.IsSuccess, "Dependency fallback should classify extensionless Spine TextAssets.");
        var groups = result.Value!.Items;
        TestAssert.Equal(2, groups.Count, "Dependency links should split duplicated extensionless Spine parts: " + FormatGroupSummary(groups));
        foreach (var group in groups)
        {
            TestAssert.Single(group.SkeletonAssets);
            TestAssert.Single(group.AtlasAssets);
            TestAssert.Single(group.TexturePageAssets);
            TestAssert.False(
                group.TexturePageAssets.Any(static part => part.AssetId.Value == "texture-illustration"),
                "Dependency-linked groups should not fall back to a loose same-name texture page.");
            TestAssert.Empty(group.Diagnostics, "Dependency-linked extensionless Spine groups should be complete.");
        }

        var textureSets = groups
            .Select(static group => TestAssert.Single(group.TexturePageAssets).AssetId.Value)
            .OrderBy(static assetId => assetId, StringComparer.Ordinal)
            .ToArray();
        TestAssert.Equal("texture-a", textureSets[0], "The first extensionless linked group should keep its material texture.");
        TestAssert.Equal("texture-b", textureSets[1], "The second extensionless linked group should keep its material texture.");
    }
}
