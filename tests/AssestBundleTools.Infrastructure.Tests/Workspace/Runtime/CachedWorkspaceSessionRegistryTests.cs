using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Runtime;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Runtime;

internal static class CachedWorkspaceSessionRegistryTests
{
    public static void OpenWorkspace_ShouldRepairLegacyNormalizedBundleDisplayName_FromSourcePath()
    {
        const string bundleId = "bundle.myrtle";
        const string sourcePath = @"C:\Bundles\ab\char_151_myrtle.ab";
        const string logicalPath = "ab/char_151_myrtle.ab";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-myrtle",
            BundleId: bundleId,
            DisplayName: "normalized.bundle",
            LogicalPath: logicalPath,
            SourceFingerprint: new WorkspaceFileFingerprintDto(sourcePath, 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets: Array.Empty<BundleIndexAssetCacheEntryDto>(),
            SerializedFileNames: ["CAB-myrtle"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));
        var hasBundleTree = registry.TryGetBundleTree(summary.WorkspaceId, out var bundleTree);

        TestAssert.True(hasBundleTree, "Bundle tree should be available after workspace open.");
        var groupNode = TestAssert.Single(bundleTree);
        TestAssert.Equal("ab", groupNode.DisplayName, "Bundle group should still come from the logical path.");
        var bundleNode = TestAssert.Single(groupNode.Children);
        TestAssert.Equal("char_151_myrtle.ab", bundleNode.DisplayName, "Legacy normalized artifact display names should be repaired from the source file.");
        TestAssert.Equal(logicalPath, bundleNode.LogicalPath, "Repair should not replace the workspace-relative logical path.");
        TestAssert.Equal(sourcePath, bundleNode.SourcePath, "Bundle tree should keep the original source path.");
    }

    public static void TryGetAssetDetails_ShouldResolveMeshTextureAssociations_FromRendererPath()
    {
        var meshId = "mesh-1";
        var meshFilterId = "mesh-filter-1";
        var rendererId = "renderer-1";
        var materialId = "material-1";
        var textureId = "texture-1";
        var directTextureId = "texture-direct";
        var bundleId = "bundle.mesh";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-a",
            BundleId: bundleId,
            DisplayName: "mesh.ab",
            LogicalPath: "mesh.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("mesh.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(meshId, "HeroMesh", AssetType.Mesh, "Mesh", pathId: 10, dependencies:
                [
                    CreateDependency(directTextureId, "DirectTexture", AssetType.Texture2D, "Texture2D", pathId: 60)
                ]),
                CreateAsset(meshFilterId, "HeroMeshFilter", AssetType.Unknown, "MeshFilter", pathId: 20, includeInList: false, dependencies:
                [
                    CreateDependency(meshId, "HeroMesh", AssetType.Mesh, "Mesh", pathId: 10),
                    CreateDependency(rendererId, "HeroRenderer", AssetType.Unknown, "MeshRenderer", pathId: 30)
                ]),
                CreateAsset(rendererId, "HeroRenderer", AssetType.Unknown, "MeshRenderer", pathId: 30, includeInList: false, dependencies:
                [
                    CreateDependency(materialId, "HeroMaterial", AssetType.Material, "Material", pathId: 40)
                ]),
                CreateAsset(materialId, "HeroMaterial", AssetType.Material, "Material", pathId: 40, dependencies:
                [
                    CreateDependency(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 50)
                ]),
                CreateAsset(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 50),
                CreateAsset(directTextureId, "DirectTexture", AssetType.Texture2D, "Texture2D", pathId: 60)
            ],
            SerializedFileNames: ["CAB-test"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));
        var meshAssetId = new AssetId(meshId);
        var bundle = new BundleId(bundleId);

        var hasAssetList = registry.TryGetAssetList(
            summary.WorkspaceId,
            bundle,
            searchKeyword: null,
            assetTypeFilter: null,
            assetTypeDisplayNameFilter: null,
            out var assetList);
        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            bundle,
            meshAssetId,
            out var details);

        TestAssert.True(hasAssetList, "Asset list should be available.");
        TestAssert.Equal(4, assetList!.TotalCount, "Hidden MeshFilter/Renderer graph nodes should not be visible in asset count.");
        TestAssert.True(hasDetails, "Mesh details should be available.");
        var textureAssociation = TestAssert.Single(details!.TextureAssociations ?? Array.Empty<AssestBundleTools.Application.Assets.Dtos.AssetDependencyDto>());
        TestAssert.Equal(textureId, textureAssociation.AssetId.Value, "Renderer path should resolve the texture association.");
        TestAssert.Equal(AssetType.Texture2D, textureAssociation.AssetType, "Texture association should keep texture type.");
    }

    public static void TryGetAssetDetails_ShouldFallbackToMeshDirectTexture_WhenRendererPathHasNoTexture()
    {
        var meshId = "mesh-1";
        var textureId = "texture-1";
        var bundleId = "bundle.mesh";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-a",
            BundleId: bundleId,
            DisplayName: "mesh.ab",
            LogicalPath: "mesh.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("mesh.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(meshId, "HeroMesh", AssetType.Mesh, "Mesh", pathId: 10, dependencies:
                [
                    CreateDependency(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 50)
                ]),
                CreateAsset(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 50)
            ],
            SerializedFileNames: ["CAB-test"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(meshId),
            out var details);

        TestAssert.True(hasDetails, "Mesh details should be available.");
        var textureAssociation = TestAssert.Single(details!.TextureAssociations ?? Array.Empty<AssestBundleTools.Application.Assets.Dtos.AssetDependencyDto>());
        TestAssert.Equal(textureId, textureAssociation.AssetId.Value, "Direct Mesh texture dependency should be used as fallback.");
    }

    public static void TryGetAssetDetails_ShouldResolveMeshTextureAssociations_FromExternalMaterialTexture()
    {
        var meshId = "mesh-1";
        var rendererId = "renderer-1";
        var materialId = "material-1";
        var textureId = "texture-external";
        var externalTexturePlaceholderId = "asset.external-texture-placeholder";
        var meshBundleId = "bundle.mesh";
        var textureBundleId = "bundle.texture";
        var meshCacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-mesh",
            BundleId: meshBundleId,
            DisplayName: "mesh.ab",
            LogicalPath: "mesh.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("mesh.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: ["archive:/CAB-textures/CAB-textures"],
            Assets:
            [
                CreateAsset(meshId, "HeroMesh", AssetType.Mesh, "Mesh", pathId: 10, serializedFileName: "CAB-mesh"),
                CreateAsset(rendererId, "HeroRenderer", AssetType.Unknown, "ParticleSystemRenderer", pathId: 20, includeInList: false, serializedFileName: "CAB-mesh", dependencies:
                [
                    CreateDependency(meshId, "HeroMesh", AssetType.Mesh, "Mesh", pathId: 10),
                    CreateDependency(materialId, "HeroMaterial", AssetType.Material, "Material", pathId: 30)
                ]),
                CreateAsset(materialId, "HeroMaterial", AssetType.Material, "Material", pathId: 30, serializedFileName: "CAB-mesh", dependencies:
                [
                    CreateDependency(
                        externalTexturePlaceholderId,
                        "CAB-textures #40",
                        AssetType.Unknown,
                        null,
                        pathId: 40,
                        serializedFileName: "CAB-textures",
                        externalReferencePath: "archive:/CAB-textures/CAB-textures")
                ])
            ],
            SerializedFileNames: ["CAB-mesh"]);
        var textureCacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-texture",
            BundleId: textureBundleId,
            DisplayName: "textures.ab",
            LogicalPath: "textures.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("textures.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 40, serializedFileName: "CAB-textures")
            ],
            SerializedFileNames: ["CAB-textures"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([meshCacheEntry, textureCacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(meshBundleId),
            new AssetId(meshId),
            out var details);
        var hasMaterialDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(meshBundleId),
            new AssetId(materialId),
            out var materialDetails);

        TestAssert.True(hasDetails, "Mesh details should be available.");
        TestAssert.True(hasMaterialDetails, "Material details should be available.");
        var materialTextureDependency = TestAssert.Single(materialDetails!.Dependencies);
        TestAssert.Equal(textureId, materialTextureDependency.AssetId.Value, "Material external placeholder should resolve to the imported texture asset.");
        TestAssert.Equal(textureBundleId, materialTextureDependency.BundleId.Value, "Material dependency should point at the target texture bundle.");
        TestAssert.Equal(AssetType.Texture2D, materialTextureDependency.AssetType, "Material dependency should use the concrete texture type after workspace resolution.");
        var textureAssociation = TestAssert.Single(details!.TextureAssociations ?? Array.Empty<AssestBundleTools.Application.Assets.Dtos.AssetDependencyDto>());
        TestAssert.Equal(textureId, textureAssociation.AssetId.Value, "External material texture dependency should resolve to imported texture asset.");
        TestAssert.Equal(textureBundleId, textureAssociation.BundleId.Value, "Texture association should point at the target bundle.");
        TestAssert.Equal(AssetType.Texture2D, textureAssociation.AssetType, "Resolved texture association should use the concrete texture type.");
    }

    public static void TryGetAssetList_ShouldExposeDynamicTypeFacets_AndFilterUnknownByDisplayName()
    {
        var bundleId = "bundle.types";
        var shaderId = "shader-1";
        var shaderVariantId = "shader-2";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-types",
            BundleId: bundleId,
            DisplayName: "types.ab",
            LogicalPath: "types.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("types.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset("texture-1", "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 10),
                CreateAsset("material-1", "HeroMaterial", AssetType.Material, "Material", pathId: 20, canExtract: false),
                CreateAsset(shaderId, "HeroShader", AssetType.Unknown, "Shader", pathId: 30),
                CreateAsset(shaderVariantId, "HeroShaderVariant", AssetType.Unknown, "shader", pathId: 31),
                CreateAsset("renderer-1", "HeroRenderer", AssetType.Unknown, "MeshRenderer", pathId: 40, includeInList: false)
            ],
            SerializedFileNames: ["CAB-types"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        var hasAllAssets = registry.TryGetAssetList(
            summary.WorkspaceId,
            new BundleId(bundleId),
            searchKeyword: null,
            assetTypeFilter: null,
            assetTypeDisplayNameFilter: null,
            out var allAssets);
        var hasShaderAssets = registry.TryGetAssetList(
            summary.WorkspaceId,
            new BundleId(bundleId),
            searchKeyword: null,
            assetTypeFilter: AssetType.Unknown,
            assetTypeDisplayNameFilter: "  Shader  ",
            out var shaderAssets);

        TestAssert.True(hasAllAssets, "Asset list should be available.");
        TestAssert.Equal(4, allAssets!.TotalCount, "Hidden renderer helper nodes should not be counted as visible assets.");
        TestAssert.True(
            allAssets.Items.Single(item => item.AssetId.Value == "texture-1").CanExtract,
            "Extractable cache entries should retain their capability in the asset list projection.");
        TestAssert.False(
            allAssets.Items.Single(item => item.AssetId.Value == "material-1").CanExtract,
            "Unsupported cache entries should remain non-extractable in the asset list projection.");
        var shaderFacet = allAssets.TypeFacets!.FirstOrDefault(facet =>
            facet.AssetType == AssetType.Unknown &&
            string.Equals(facet.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase));
        TestAssert.True(shaderFacet is not null, "Visible Shader assets should produce a dynamic Unknown/Shader facet.");
        TestAssert.Equal(2, shaderFacet!.Count, "Dynamic type facets should group display names case-insensitively.");
        TestAssert.True(
            allAssets.TypeFacets!.All(facet =>
                !string.Equals(facet.AssetTypeDisplayName, "MeshRenderer", StringComparison.OrdinalIgnoreCase)),
            "Hidden graph helper nodes should not produce visible filter facets.");

        TestAssert.True(hasShaderAssets, "Filtered Shader asset list should be available.");
        TestAssert.Equal(2, shaderAssets!.FilteredCount, "Unknown + Shader display filter should keep only Shader assets.");
        TestAssert.Equal("Shader", shaderAssets.AssetTypeDisplayNameFilter, "Display-name filter should be trimmed in the result.");
        TestAssert.True(
            shaderAssets.Items.All(item =>
                item.AssetType == AssetType.Unknown &&
                string.Equals(item.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase)),
            "Filtered items should all be dynamic Shader assets.");
        TestAssert.True(
            shaderAssets.Items.Any(item => item.AssetId.Value == shaderId) &&
            shaderAssets.Items.Any(item => item.AssetId.Value == shaderVariantId),
            "Both Shader assets should survive the dynamic type filter.");
    }

    public static void TryGetWorkspaceChangeSummary_ShouldIncludeHiddenExtractedAssets()
    {
        var bundleId = "bundle.spine";
        var hiddenSkeletonId = "asset.hidden-skeleton";
        var visibleTextureId = "asset.visible-texture";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-spine",
            BundleId: bundleId,
            DisplayName: "spine.ab",
            LogicalPath: "spine.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("spine.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(
                    hiddenSkeletonId,
                    "HiddenSkeleton",
                    AssetType.TextAsset,
                    "TextAsset",
                    pathId: 10,
                    includeInList: false,
                    canExtract: true),
                CreateAsset(visibleTextureId, "VisibleTexture", AssetType.Texture2D, "Texture2D", pathId: 20)
            ],
            SerializedFileNames: ["CAB-spine"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));
        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        var extracted = registry.TryExtractAsset(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(hiddenSkeletonId),
            out _);
        var hasSummary = registry.TryGetWorkspaceChangeSummary(summary.WorkspaceId, out var changeSummary);

        TestAssert.True(extracted, "Hidden dependency assets can still be valid extraction targets.");
        TestAssert.True(hasSummary, "Workspace change summary should be available.");
        var item = TestAssert.Single(changeSummary!.Items);
        TestAssert.Equal(hiddenSkeletonId, item.AssetId.Value, "Extracted hidden dependencies should remain visible in change records.");
        TestAssert.True(item.LastExtractedAtUtc.HasValue, "Hidden extracted dependency should keep the extraction timestamp.");
        TestAssert.Equal(1, changeSummary.ExtractedAssetCount, "Summary extracted count should include hidden extracted dependencies.");
    }

    public static void TryGetAssociatedAssetImpacts_ShouldReturnVisibleDependents_WhenTextureChanges()
    {
        var bundleId = "bundle.sprites";
        var spriteId = "asset.sprite";
        var textureId = "asset.texture";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-sprites",
            BundleId: bundleId,
            DisplayName: "sprites.ab",
            LogicalPath: "sprites.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("sprites.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(spriteId, "HeroSprite", AssetType.Sprite, "Sprite", pathId: 10, dependencies:
                [
                    CreateDependency(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 20)
                ]),
                CreateAsset(textureId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 20)
            ],
            SerializedFileNames: ["CAB-sprites"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        var hasImpacts = registry.TryGetAssociatedAssetImpacts(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(textureId),
            out var impacts);

        TestAssert.True(hasImpacts, "Associated impact query should be available for loaded assets.");
        var impact = TestAssert.Single(impacts);
        TestAssert.Equal(spriteId, impact.AssetId.Value, "Texture changes should affect Sprite dependents.");
        TestAssert.Equal(bundleId, impact.BundleId.Value, "Associated impact should keep dependent bundle identity.");
        TestAssert.Equal(AssetType.Sprite, impact.AssetType, "Associated impact should keep dependent asset type.");
    }

    public static void TryGetAssociatedAssetImpacts_ShouldReturnDirectDependents_WhenTextAssetChanges()
    {
        var bundleId = "bundle.spine";
        var skeletonId = "asset.skeleton";
        var atlasId = "asset.atlas";
        var hostId = "asset.host";
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-spine",
            BundleId: bundleId,
            DisplayName: "spine.ab",
            LogicalPath: "spine.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("spine.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(hostId, "HeroSpineHost", AssetType.Unknown, "MonoBehaviour", pathId: 10, dependencies:
                [
                    CreateDependency(skeletonId, "Hero.skel", AssetType.TextAsset, "TextAsset", pathId: 20),
                    CreateDependency(atlasId, "Hero.atlas", AssetType.TextAsset, "TextAsset", pathId: 30)
                ]),
                CreateAsset(skeletonId, "Hero.skel", AssetType.TextAsset, "TextAsset", pathId: 20),
                CreateAsset(atlasId, "Hero.atlas", AssetType.TextAsset, "TextAsset", pathId: 30)
            ],
            SerializedFileNames: ["CAB-spine"]);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore([cacheEntry]);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(bundleIndexCacheStore));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        var hasImpacts = registry.TryGetAssociatedAssetImpacts(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(skeletonId),
            out var impacts);

        TestAssert.True(hasImpacts, "Associated impact query should be available for loaded text assets.");
        var impact = TestAssert.Single(impacts);
        TestAssert.Equal(hostId, impact.AssetId.Value, "Text asset changes should affect direct dependents.");
        TestAssert.Equal(bundleId, impact.BundleId.Value, "Associated impact should keep dependent bundle identity.");
        TestAssert.Equal("MonoBehaviour", impact.AssetTypeDisplayName, "Associated impact should preserve dynamic dependent type text.");
    }

    private static BundleIndexAssetCacheEntryDto CreateAsset(
        string assetId,
        string name,
        AssetType assetType,
        string? displayType,
        long pathId,
        bool includeInList = true,
        bool? canExtract = null,
        string serializedFileName = "CAB-test",
        IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? dependencies = null)
    {
        return new BundleIndexAssetCacheEntryDto(
            AssetId: assetId,
            AssetName: name,
            AssetType: assetType.ToString(),
            AssetPath: $"{serializedFileName}/{name}",
            ContainerPath: string.Empty,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            HasPreview: includeInList,
            CanExtract: canExtract ?? includeInList,
            CanReplace: includeInList,
            SerializedFileName: serializedFileName,
            PathId: pathId,
            AssetTypeDisplayName: displayType,
            Dependencies: dependencies,
            IncludeInAssetList: includeInList);
    }

    private static BundleIndexAssetDependencyCacheEntryDto CreateDependency(
        string assetId,
        string name,
        AssetType assetType,
        string? displayType,
        long pathId,
        string? serializedFileName = null,
        string? externalReferencePath = null)
    {
        return new BundleIndexAssetDependencyCacheEntryDto(
            AssetId: assetId,
            AssetName: name,
            AssetType: assetType.ToString(),
            PathId: pathId,
            AssetTypeDisplayName: displayType,
            SerializedFileName: serializedFileName,
            ExternalReferencePath: externalReferencePath);
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;

        public TestWorkspaceDataStoreFactory(IBundleIndexCacheStore bundleIndexCacheStore)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";

        public IWorkspaceMetadataStore CreateWorkspaceMetadataStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly IReadOnlyList<BundleIndexCacheEntryDto> _entries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> entries)
        {
            _entries = entries;
        }

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) =>
            _entries.FirstOrDefault(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.Ordinal));

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) =>
            _entries
                .Where(entry => string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
                .ToArray();

        public void Save(BundleIndexCacheEntryDto cacheEntry) =>
            throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) =>
            throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) =>
            throw new NotSupportedException();
    }

}
