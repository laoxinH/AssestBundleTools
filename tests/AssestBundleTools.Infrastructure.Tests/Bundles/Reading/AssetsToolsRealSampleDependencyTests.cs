using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Runtime;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Reading;

internal static partial class AssetsToolsRealSampleDependencyTests
{
    private const string SampleBundlePath = @"D:\project\测试ab\char_2014_nian_cfa#1_20260503_231150.ab";
    private const string SampleWorkspaceKey = "real-sample-cone002";
    private const string SampleBundleId = "char_2014_nian_cfa#1_20260503_231150.ab";
    private const string TargetMeshName = "Cone002";
    private const string TargetMaterialName = "paopao_01";

    public static async Task Cone002Mesh_ShouldExposeRendererMaterialPath_ButNoTextureAssociation_FromRealDependencyGraph()
    {
        if (!File.Exists(SampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {SampleBundlePath}");
            return;
        }

        var reader = new AssetsToolsAssetBundleReader();
        var readResult = await reader.ReadAsync(new AssetBundleReadRequestDto(
            BundlePath: SampleBundlePath,
            LogicalPath: Path.GetFileName(SampleBundlePath),
            WorkspaceKey: SampleWorkspaceKey,
            WorkspaceDataRootPath: Path.GetTempPath()));

        TestAssert.True(readResult.IsSuccess, "Sample bundle should be readable by AssetsTools reader.");

        var bundleReadResult = TestAssert.NotNull<AssetBundleReadResultDto>(
            readResult.Value,
            "Reader should return a bundle read result.");
        Console.WriteLine($"[INFO] Bundle external dependencies ({bundleReadResult.Dependencies.Count}): {string.Join("; ", bundleReadResult.Dependencies)}");
        Console.WriteLine($"[INFO] Material assets: {CountAssets(bundleReadResult, AssetType.Material)}; Texture2D assets: {CountAssets(bundleReadResult, AssetType.Texture2D)}; Sprite assets: {CountAssets(bundleReadResult, AssetType.Sprite)}.");
        var materialTexEnvPointers = ProbeMaterialTexEnvPointers(TargetMaterialName);
        Console.WriteLine($"[INFO] {TargetMaterialName} TexEnv PPtrs ({materialTexEnvPointers.Count}): {FormatTexEnvPointers(materialTexEnvPointers)}");
        var coneMesh = bundleReadResult.Assets
            .SingleOrDefault(static asset =>
                asset.AssetType == AssetType.Mesh.ToString() &&
                string.Equals(asset.AssetName, TargetMeshName, StringComparison.Ordinal));

        TestAssert.NotNull(coneMesh, $"Sample bundle should contain one Mesh asset named {TargetMeshName}.");

        var cacheEntry = CreateCacheEntry(SampleBundlePath, bundleReadResult);
        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore([cacheEntry])));
        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: SampleWorkspaceKey,
            WorkspacePath: Path.GetDirectoryName(SampleBundlePath) ?? string.Empty,
            WorkspaceDataRootPath: Path.GetTempPath(),
            WorkspaceDisplayName: "Real Sample",
            InitialMode: WorkspaceMode.SinglePane));

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(SampleBundleId),
            new AssetId(coneMesh!.AssetId),
            out var details);

        TestAssert.True(hasDetails, $"{TargetMeshName} mesh details should be available from cached workspace state.");
        Console.WriteLine($"[INFO] {TargetMeshName} PathId={details!.PathId}, AssetId={details.AssetId.Value}.");
        Console.WriteLine($"[INFO] Forward dependencies ({details.Dependencies.Count}): {FormatDependencies(details.Dependencies)}");
        Console.WriteLine($"[INFO] Reverse dependencies ({details.ReferencedBy?.Count ?? 0}): {FormatDependencies(details.ReferencedBy)}");
        Console.WriteLine($"[INFO] Dependency closure ({details.DependencyClosure?.Count ?? 0}): {FormatDependencies(details.DependencyClosure)}");
        Console.WriteLine($"[INFO] Texture associations ({details.TextureAssociations?.Count ?? 0}): {FormatDependencies(details.TextureAssociations)}");
        var loggedMaterialAssetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var referrer in details.ReferencedBy ?? Array.Empty<AssetDependencyDto>())
        {
            var referrerAsset = FindReadAsset(bundleReadResult, referrer.AssetId.Value);
            Console.WriteLine(
                $"[INFO] Referrer {referrer.Name} dependencies ({referrerAsset?.Dependencies?.Count ?? 0}): {FormatReadDependencies(referrerAsset?.Dependencies)}");

            foreach (var materialDependency in referrerAsset?.Dependencies?
                         .Where(static dependency => dependency.AssetType == AssetType.Material.ToString()) ??
                     Array.Empty<AssetBundleReadAssetDependencyDto>())
            {
                var materialAsset = FindReadAsset(bundleReadResult, materialDependency.AssetId);
                if (!loggedMaterialAssetIds.Add(materialDependency.AssetId))
                {
                    continue;
                }

                Console.WriteLine(
                    $"[INFO] Material {materialDependency.AssetName} dependencies ({materialAsset?.Dependencies?.Count ?? 0}): {FormatReadDependencies(materialAsset?.Dependencies)}");
            }
        }
        TestAssert.True(
            details.ReferencedBy is { Count: > 0 },
            $"{TargetMeshName} should have reverse dependencies in the dependency tree.");
        TestAssert.True(
            details.ReferencedBy!
                .SelectMany(referrer => FindReadAsset(bundleReadResult, referrer.AssetId.Value)?.Dependencies ?? Array.Empty<AssetBundleReadAssetDependencyDto>())
                .Any(static dependency =>
                    dependency.AssetType == AssetType.Material.ToString() &&
                    string.Equals(dependency.AssetName, TargetMaterialName, StringComparison.Ordinal)),
            $"{TargetMeshName} reverse renderer path should reach material {TargetMaterialName}.");
        var paopaoMaterial = TestAssert.NotNull(
            bundleReadResult.Assets.SingleOrDefault(static asset =>
                asset.AssetType == AssetType.Material.ToString() &&
                string.Equals(asset.AssetName, TargetMaterialName, StringComparison.Ordinal)),
            $"Sample bundle should contain Material asset named {TargetMaterialName}.");
        var materialDependencies = paopaoMaterial.Dependencies ?? Array.Empty<AssetBundleReadAssetDependencyDto>();
        var materialExternalDependencies = materialDependencies
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency.ExternalReferencePath))
            .ToArray();
        TestAssert.True(
            materialTexEnvPointers.Count > 0,
            $"{TargetMaterialName} should expose texture PPtrs under m_SavedProperties.m_TexEnvs.");
        TestAssert.True(
            materialTexEnvPointers.All(static pointer => pointer.IsExternal),
            $"{TargetMaterialName} TexEnv PPtrs should target external CAB serialized files in this sample.");
        TestAssert.True(
            materialExternalDependencies.Length > 0,
            $"{TargetMaterialName} TexEnv PPtrs should be preserved as external asset dependency placeholders.");
        var externalDependencyPathIds = materialExternalDependencies
            .Select(static dependency => dependency.PathId)
            .ToHashSet();
        foreach (var texEnvPointer in materialTexEnvPointers)
        {
            TestAssert.True(
                externalDependencyPathIds.Contains(texEnvPointer.PathId),
                $"{TargetMaterialName} TexEnv PPtr {texEnvPointer.TexEnvName} PathId={texEnvPointer.PathId} should be present in Reader dependencies.");
        }

        TestAssert.False(
            materialDependencies.Any(static dependency =>
                dependency.AssetType == AssetType.Texture2D.ToString() ||
                dependency.AssetType == AssetType.Sprite.ToString()),
            $"{TargetMaterialName} should not resolve a concrete Texture2D/Sprite from this single bundle.");
        TestAssert.True(
            details.TextureAssociations is null or { Count: 0 },
            $"{TargetMeshName} should not currently resolve a concrete Texture2D/Sprite association from this single bundle.");
    }

    public static async Task Cone002Mesh_ShouldResolveTextureAssociation_WhenReadableSampleWorkspaceContainsMatchingTextureBundles()
    {
        if (!File.Exists(SampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {SampleBundlePath}");
            return;
        }

        var cacheEntries = await LoadReadableSampleWorkspaceCacheEntriesAsync();
        var workspaceTextureAssetIds = cacheEntries
            .SelectMany(static entry => entry.Assets)
            .Where(static asset =>
                string.Equals(asset.AssetType, AssetType.Texture2D.ToString(), StringComparison.Ordinal) ||
                string.Equals(asset.AssetType, AssetType.Sprite.ToString(), StringComparison.Ordinal))
            .Select(static asset => asset.AssetId)
            .ToHashSet(StringComparer.Ordinal);
        var materialTexEnvPointers = ProbeMaterialTexEnvPointers(TargetMaterialName);
        var expectedTextureAssociationAssetIds = materialTexEnvPointers
            .Select(static pointer => AssetsToolsAssetIdFactory.CreateAssetId(pointer.SerializedFileName, pointer.PathId))
            .Where(workspaceTextureAssetIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine($"[INFO] Readable sample bundles ({cacheEntries.Count}): {string.Join("; ", cacheEntries.Select(static entry => entry.DisplayName))}");
        Console.WriteLine($"[INFO] Readable texture assets ({workspaceTextureAssetIds.Count}).");
        Console.WriteLine($"[INFO] Workspace-matched TexEnv targets for {TargetMaterialName} ({expectedTextureAssociationAssetIds.Length}): {string.Join("; ", expectedTextureAssociationAssetIds)}");

        var registry = new CachedWorkspaceSessionRegistry(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore(cacheEntries)));
        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: SampleWorkspaceKey,
            WorkspacePath: Path.GetDirectoryName(SampleBundlePath) ?? string.Empty,
            WorkspaceDataRootPath: Path.GetTempPath(),
            WorkspaceDisplayName: "Real Sample",
            InitialMode: WorkspaceMode.SinglePane));
        var sampleBundleEntry = TestAssert.NotNull(
            cacheEntries.FirstOrDefault(static entry => string.Equals(entry.BundleId, SampleBundleId, StringComparison.Ordinal)),
            $"Sample bundle cache entry should be present for {SampleBundleId}.");
        var coneMesh = TestAssert.NotNull(
            sampleBundleEntry.Assets.SingleOrDefault(static asset =>
                asset.AssetType == AssetType.Mesh.ToString() &&
                string.Equals(asset.AssetName, TargetMeshName, StringComparison.Ordinal)),
            $"Sample bundle should contain one Mesh asset named {TargetMeshName}.");

        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(SampleBundleId),
            new AssetId(coneMesh.AssetId),
            out var details);

        TestAssert.True(hasDetails, $"{TargetMeshName} mesh details should be available from the readable sample workspace.");
        if (expectedTextureAssociationAssetIds.Length > 0)
        {
            TestAssert.True(
                details!.TextureAssociations is { Count: > 0 },
                $"{TargetMeshName} should resolve texture associations when the readable sample workspace contains matching texture bundles.");

            var actualTextureAssociationIds = details.TextureAssociations!
                .Select(static association => association.AssetId.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var expectedAssetId in expectedTextureAssociationAssetIds)
            {
                TestAssert.True(
                    actualTextureAssociationIds.Contains(expectedAssetId),
                    $"{TargetMeshName} should include workspace texture association {expectedAssetId}.");
            }
        }
        else
        {
            TestAssert.True(
                details!.TextureAssociations is null or { Count: 0 },
                $"{TargetMeshName} should still have no concrete texture association because the readable sample workspace does not contain matching texture bundles.");
        }
    }

}
