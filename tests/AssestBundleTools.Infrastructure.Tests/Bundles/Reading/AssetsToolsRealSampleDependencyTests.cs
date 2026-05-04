using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Runtime;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Reading;

internal static class AssetsToolsRealSampleDependencyTests
{
    private const string SampleBundlePath = @"D:\project\测试ab\char_2014_nian_cfa#1_20260503_231150.ab";
    private const string SampleWorkspaceKey = "real-sample-cone002";
    private const string SampleBundleId = "char_2014_nian_cfa#1_20260503_231150.ab";
    private const string SampleBundleCacheKey = "char_2014_nian_cfa#1_20260503_231150";
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

        var cacheEntry = CreateCacheEntry(bundleReadResult);
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

    private static string FormatDependencies(IReadOnlyList<AssetDependencyDto>? dependencies)
    {
        return dependencies is { Count: > 0 }
            ? string.Join(
                "; ",
                dependencies.Select(static dependency =>
                    $"{dependency.Name} ({dependency.AssetTypeDisplayName ?? dependency.AssetType.ToString()}, PathId={dependency.PathId})"))
            : "<none>";
    }

    private static string FormatReadDependencies(IReadOnlyList<AssetBundleReadAssetDependencyDto>? dependencies)
    {
        return dependencies is { Count: > 0 }
            ? string.Join(
                "; ",
                dependencies.Select(static dependency =>
                    $"{dependency.AssetName} ({dependency.AssetTypeDisplayName ?? dependency.AssetType}, PathId={dependency.PathId})"))
            : "<none>";
    }

    private static string FormatTexEnvPointers(IReadOnlyList<MaterialTexEnvPointer> pointers)
    {
        return pointers is { Count: > 0 }
            ? string.Join(
                "; ",
                pointers.Select(static pointer =>
                    $"{pointer.TexEnvName}: fileId={pointer.FileId}, pathId={pointer.PathId}, target={pointer.SerializedFileName}, external={pointer.ExternalReferencePath ?? "<none>"}"))
            : "<none>";
    }

    private static IReadOnlyList<MaterialTexEnvPointer> ProbeMaterialTexEnvPointers(string materialName)
    {
        var assetsManager = new AssetsManager
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(SampleBundlePath, unpackIfPacked: true);
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance);
            var internalSerializedFileLookup = AssetsToolsBundleDependencyResolver.BuildInternalSerializedFileLookup(serializedFiles);
            foreach (var serializedFile in serializedFiles)
            {
                foreach (var materialInfo in serializedFile.file.GetAssetsOfType(AssetClassID.Material))
                {
                    var materialField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, materialInfo);
                    var name = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(materialField, "m_Name"));
                    if (!string.Equals(name, materialName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return ReadMaterialTexEnvPointers(
                        serializedFile,
                        materialField,
                        internalSerializedFileLookup);
                }
            }

            return Array.Empty<MaterialTexEnvPointer>();
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static IReadOnlyList<AssetsFileInstance> LoadSerializedFiles(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance)
    {
        var serializedFiles = new List<AssetsFileInstance>();
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;
        for (var index = 0; index < directoryInfos.Count; index++)
        {
            if (bundleInstance.file.IsAssetsFile(index))
            {
                serializedFiles.Add(assetsManager.LoadAssetsFileFromBundle(bundleInstance, index, loadDeps: false));
            }
        }

        return serializedFiles;
    }

    private static IReadOnlyList<MaterialTexEnvPointer> ReadMaterialTexEnvPointers(
        AssetsFileInstance serializedFile,
        AssetTypeValueField? materialField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var texEnvArray = AssetsToolsFieldReader.TryGetField(
            materialField,
            "m_SavedProperties",
            "m_TexEnvs",
            "Array");
        if (texEnvArray is null)
        {
            return Array.Empty<MaterialTexEnvPointer>();
        }

        var pointers = new List<MaterialTexEnvPointer>();
        for (var index = 0; index < texEnvArray.Children.Count; index++)
        {
            var entry = texEnvArray.Children[index];
            var texEnvName = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "first")) ??
                             AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "key")) ??
                             $"TexEnv #{index}";
            var texturePointer = AssetsToolsFieldReader.TryGetField(entry, "second", "m_Texture") ??
                                 AssetsToolsFieldReader.TryGetField(entry, "value", "m_Texture") ??
                                 AssetsToolsFieldReader.TryGetField(entry, "m_Texture");
            var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(texturePointer, "m_FileID")) ?? 0;
            var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(texturePointer, "m_PathID"));
            if (pathId is null || pathId.Value == 0)
            {
                continue;
            }

            TestAssert.True(
                AssetsToolsBundleDependencyResolver.TryResolveTargetSerializedFileReference(
                    serializedFile,
                    fileId,
                    internalSerializedFileLookup,
                    out var targetReference),
                $"{TargetMaterialName} TexEnv PPtr {texEnvName} should resolve to a serialized file reference.");
            pointers.Add(new MaterialTexEnvPointer(
                TexEnvName: texEnvName,
                FileId: fileId,
                PathId: pathId.Value,
                SerializedFileName: targetReference.SerializedFileName,
                ExternalReferencePath: targetReference.ExternalReferencePath,
                IsExternal: targetReference.IsExternal));
        }

        return pointers;
    }

    private static AssetBundleReadAssetDto? FindReadAsset(AssetBundleReadResultDto readResult, string assetId)
    {
        return readResult.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId, assetId, StringComparison.Ordinal));
    }

    private static int CountAssets(AssetBundleReadResultDto readResult, AssetType assetType)
    {
        return readResult.Assets.Count(asset => string.Equals(asset.AssetType, assetType.ToString(), StringComparison.Ordinal));
    }

    private static BundleIndexCacheEntryDto CreateCacheEntry(AssetBundleReadResultDto readResult)
    {
        var sampleFileInfo = new FileInfo(SampleBundlePath);
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: SampleWorkspaceKey,
            BundleCacheKey: SampleBundleCacheKey,
            BundleId: SampleBundleId,
            DisplayName: readResult.DisplayName,
            LogicalPath: readResult.LogicalPath,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SampleBundlePath,
                sampleFileInfo.Length,
                sampleFileInfo.LastWriteTimeUtc,
                ContentHash: null),
            CachedAtUtc: DateTimeOffset.UtcNow,
            ParserVersion: readResult.ParserVersion,
            ToolVersion: "test",
            Dependencies: readResult.Dependencies,
            Assets: readResult.Assets.Select(CreateCacheAsset).ToArray(),
            SerializedFileNames: readResult.SerializedFileNames);
    }

    private static BundleIndexAssetCacheEntryDto CreateCacheAsset(AssetBundleReadAssetDto asset)
    {
        return new BundleIndexAssetCacheEntryDto(
            AssetId: asset.AssetId,
            AssetName: asset.AssetName,
            AssetType: asset.AssetType,
            AssetPath: asset.AssetPath,
            ContainerPath: asset.ContainerPath,
            SizeInBytes: asset.SizeInBytes,
            PreferredDisplaySizeInBytes: asset.PreferredDisplaySizeInBytes,
            HasPreview: asset.HasPreview,
            CanExtract: asset.CanExtract,
            CanReplace: asset.CanReplace,
            SerializedFileName: asset.SerializedFileName,
            PathId: asset.PathId,
            AssetTypeDisplayName: asset.AssetTypeDisplayName,
            Dependencies: asset.Dependencies?.Select(CreateCacheDependency).ToArray(),
            IncludeInAssetList: asset.IncludeInAssetList);
    }

    private static BundleIndexAssetDependencyCacheEntryDto CreateCacheDependency(AssetBundleReadAssetDependencyDto dependency)
    {
        return new BundleIndexAssetDependencyCacheEntryDto(
            AssetId: dependency.AssetId,
            AssetName: dependency.AssetName,
            AssetType: dependency.AssetType,
            PathId: dependency.PathId,
            AssetTypeDisplayName: dependency.AssetTypeDisplayName,
            SerializedFileName: dependency.SerializedFileName,
            ExternalReferencePath: dependency.ExternalReferencePath);
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

        public string GetDefaultRootDirectoryPath() => Path.GetTempPath();

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

    private sealed record MaterialTexEnvPointer(
        string TexEnvName,
        long FileId,
        long PathId,
        string SerializedFileName,
        string? ExternalReferencePath,
        bool IsExternal);
}
