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

    private static BundleIndexAssetCacheEntryDto CreateAsset(
        string assetId,
        string name,
        AssetType assetType,
        string displayType,
        long pathId,
        bool includeInList = true,
        IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? dependencies = null)
    {
        return new BundleIndexAssetCacheEntryDto(
            AssetId: assetId,
            AssetName: name,
            AssetType: assetType.ToString(),
            AssetPath: $"CAB-test/{name}",
            ContainerPath: string.Empty,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            HasPreview: includeInList,
            CanExtract: includeInList,
            CanReplace: includeInList,
            SerializedFileName: "CAB-test",
            PathId: pathId,
            AssetTypeDisplayName: displayType,
            Dependencies: dependencies,
            IncludeInAssetList: includeInList);
    }

    private static BundleIndexAssetDependencyCacheEntryDto CreateDependency(
        string assetId,
        string name,
        AssetType assetType,
        string displayType,
        long pathId)
    {
        return new BundleIndexAssetDependencyCacheEntryDto(
            AssetId: assetId,
            AssetName: name,
            AssetType: assetType.ToString(),
            PathId: pathId,
            AssetTypeDisplayName: displayType);
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;

        public TestWorkspaceDataStoreFactory(IBundleIndexCacheStore bundleIndexCacheStore)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
        }

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
