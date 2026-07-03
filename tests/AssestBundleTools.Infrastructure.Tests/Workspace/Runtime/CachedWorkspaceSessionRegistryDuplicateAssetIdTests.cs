using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Runtime;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Runtime;

internal static class CachedWorkspaceSessionRegistryDuplicateAssetIdTests
{
    public static void OpenWorkspace_ShouldKeepDuplicateLegacyAssetIdsScopedByBundle()
    {
        const string duplicateTextureId = "asset.3f689d878241b97f9c3de1968c8ad7474cc36725238002264d1941a26c7764cd";
        const string bundleAId = "bundle.characters.shared";
        const string bundleBId = "bundle.effects.shared";
        const string materialAId = "asset.material-a";
        const string materialBId = "asset.material-b";

        var bundleA = CreateBundle(
            bundleId: bundleAId,
            bundleCacheKey: "bundle-cache-characters-shared",
            sourcePath: @"D:\Bundles\characters\shared.ab",
            logicalPath: "characters/shared.ab",
            textureName: "CharacterTexture",
            materialId: materialAId,
            materialName: "CharacterMaterial",
            duplicateTextureId: duplicateTextureId);
        var bundleB = CreateBundle(
            bundleId: bundleBId,
            bundleCacheKey: "bundle-cache-effects-shared",
            sourcePath: @"D:\Bundles\effects\shared.ab",
            logicalPath: "effects/shared.ab",
            textureName: "EffectTexture",
            materialId: materialBId,
            materialName: "EffectMaterial",
            duplicateTextureId: duplicateTextureId);
        var registry = new CachedWorkspaceSessionRegistry(
            new TestWorkspaceDataStoreFactory(new TestBundleIndexCacheStore([bundleA, bundleB])));

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: "workspace-a",
            WorkspacePath: @"D:\Bundles",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));

        TestAssert.Equal(2, summary.BundleCount, "Workspace should keep both same-name bundles.");
        TestAssert.Equal(4, summary.AssetCount, "Duplicate legacy asset ids should not collapse visible assets.");

        var hasTextureADetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(bundleAId),
            new AssetId(duplicateTextureId),
            out var textureADetails);
        var hasTextureBDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(bundleBId),
            new AssetId(duplicateTextureId),
            out var textureBDetails);

        TestAssert.True(hasTextureADetails, "First duplicate asset should remain addressable by bundle and asset id.");
        TestAssert.True(hasTextureBDetails, "Second duplicate asset should remain addressable by bundle and asset id.");
        TestAssert.Equal("CharacterTexture", textureADetails!.Name, "First bundle should resolve its own duplicate asset.");
        TestAssert.Equal("EffectTexture", textureBDetails!.Name, "Second bundle should resolve its own duplicate asset.");
        var referencedByA = TestAssert.Single(textureADetails.ReferencedBy ?? Array.Empty<AssestBundleTools.Application.Assets.Dtos.AssetDependencyDto>());
        var referencedByB = TestAssert.Single(textureBDetails.ReferencedBy ?? Array.Empty<AssestBundleTools.Application.Assets.Dtos.AssetDependencyDto>());
        TestAssert.Equal(materialAId, referencedByA.AssetId.Value, "Reverse dependency for first duplicate should stay in its bundle.");
        TestAssert.Equal(bundleAId, referencedByA.BundleId.Value, "Reverse dependency for first duplicate should keep bundle identity.");
        TestAssert.Equal(materialBId, referencedByB.AssetId.Value, "Reverse dependency for second duplicate should stay in its bundle.");
        TestAssert.Equal(bundleBId, referencedByB.BundleId.Value, "Reverse dependency for second duplicate should keep bundle identity.");

        var hasImpactsA = registry.TryGetAssociatedAssetImpacts(
            summary.WorkspaceId,
            new BundleId(bundleAId),
            new AssetId(duplicateTextureId),
            out var impactsA);
        var hasImpactsB = registry.TryGetAssociatedAssetImpacts(
            summary.WorkspaceId,
            new BundleId(bundleBId),
            new AssetId(duplicateTextureId),
            out var impactsB);

        TestAssert.True(hasImpactsA, "Associated impact query should work for first duplicate asset.");
        TestAssert.True(hasImpactsB, "Associated impact query should work for second duplicate asset.");
        var impactA = TestAssert.Single(impactsA);
        var impactB = TestAssert.Single(impactsB);
        TestAssert.Equal(materialAId, impactA.AssetId.Value, "First duplicate asset should affect only its own material.");
        TestAssert.Equal(bundleAId, impactA.BundleId.Value, "First duplicate asset impact should stay scoped to the first bundle.");
        TestAssert.Equal(materialBId, impactB.AssetId.Value, "Second duplicate asset should affect only its own material.");
        TestAssert.Equal(bundleBId, impactB.BundleId.Value, "Second duplicate asset impact should stay scoped to the second bundle.");
    }

    private static BundleIndexCacheEntryDto CreateBundle(
        string bundleId,
        string bundleCacheKey,
        string sourcePath,
        string logicalPath,
        string textureName,
        string materialId,
        string materialName,
        string duplicateTextureId)
    {
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: bundleCacheKey,
            BundleId: bundleId,
            DisplayName: "shared.ab",
            LogicalPath: logicalPath,
            SourceFingerprint: new WorkspaceFileFingerprintDto(sourcePath, 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(duplicateTextureId, textureName, AssetType.Texture2D, "Texture2D", pathId: 10),
                CreateAsset(materialId, materialName, AssetType.Material, "Material", pathId: 20, dependencies:
                [
                    CreateDependency(duplicateTextureId, textureName, AssetType.Texture2D, "Texture2D", pathId: 10)
                ])
            ],
            SerializedFileNames: ["CAB-shared"]);
    }

    private static BundleIndexAssetCacheEntryDto CreateAsset(
        string assetId,
        string name,
        AssetType assetType,
        string displayType,
        long pathId,
        IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? dependencies = null)
    {
        return new BundleIndexAssetCacheEntryDto(
            AssetId: assetId,
            AssetName: name,
            AssetType: assetType.ToString(),
            AssetPath: $"CAB-shared/{name}",
            ContainerPath: string.Empty,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            HasPreview: true,
            CanExtract: true,
            CanReplace: true,
            SerializedFileName: "CAB-shared",
            PathId: pathId,
            AssetTypeDisplayName: displayType,
            Dependencies: dependencies,
            IncludeInAssetList: true);
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
            AssetTypeDisplayName: displayType,
            SerializedFileName: "CAB-shared",
            ExternalReferencePath: null);
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
