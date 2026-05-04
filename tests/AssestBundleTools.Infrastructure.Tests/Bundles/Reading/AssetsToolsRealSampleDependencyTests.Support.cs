using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Reading;

internal static partial class AssetsToolsRealSampleDependencyTests
{
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

    private static async Task<IReadOnlyList<BundleIndexCacheEntryDto>> LoadReadableSampleWorkspaceCacheEntriesAsync()
    {
        var cacheEntries = new List<BundleIndexCacheEntryDto>();
        var sampleBundleFound = false;
        foreach (var bundlePath in EnumerateSampleBundlePaths())
        {
            var readResult = await ReadBundleAsync(bundlePath);
            if (readResult is null)
            {
                continue;
            }

            if (string.Equals(bundlePath, SampleBundlePath, StringComparison.OrdinalIgnoreCase))
            {
                sampleBundleFound = true;
            }

            cacheEntries.Add(CreateCacheEntry(bundlePath, readResult));
        }

        TestAssert.True(sampleBundleFound, $"Sample bundle should be readable: {SampleBundlePath}");
        return cacheEntries;
    }

    private static async Task<AssetBundleReadResultDto?> ReadBundleAsync(string bundlePath)
    {
        var reader = new AssetsToolsAssetBundleReader();
        var readResult = await reader.ReadAsync(new AssetBundleReadRequestDto(
            BundlePath: bundlePath,
            LogicalPath: Path.GetFileName(bundlePath),
            WorkspaceKey: SampleWorkspaceKey,
            WorkspaceDataRootPath: Path.GetTempPath()));

        if (!readResult.IsSuccess)
        {
            Console.WriteLine($"[WARN] Skip unreadable bundle: {Path.GetFileName(bundlePath)}");
            return null;
        }

        return TestAssert.NotNull(
            readResult.Value,
            $"Reader should return a bundle read result for {Path.GetFileName(bundlePath)}.");
    }

    private static IEnumerable<string> EnumerateSampleBundlePaths()
    {
        var sampleDirectoryPath = Path.GetDirectoryName(SampleBundlePath);
        return string.IsNullOrWhiteSpace(sampleDirectoryPath)
            ? Array.Empty<string>()
            : Directory.EnumerateFiles(sampleDirectoryPath, "*.ab")
                .OrderBy(static path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
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

    private static BundleIndexCacheEntryDto CreateCacheEntry(string bundlePath, AssetBundleReadResultDto readResult)
    {
        var bundleFileInfo = new FileInfo(bundlePath);
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: SampleWorkspaceKey,
            BundleCacheKey: Path.GetFileNameWithoutExtension(bundlePath),
            BundleId: Path.GetFileName(bundlePath),
            DisplayName: readResult.DisplayName,
            LogicalPath: readResult.LogicalPath,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                bundlePath,
                bundleFileInfo.Length,
                bundleFileInfo.LastWriteTimeUtc,
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
