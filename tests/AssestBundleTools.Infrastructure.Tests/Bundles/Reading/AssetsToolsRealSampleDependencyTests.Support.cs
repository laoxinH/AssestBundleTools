using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;
using System.Globalization;
using System.Text.Json;

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

    private static IReadOnlyList<MonoBehaviourPreviewProbeSummary> ProbeMonoBehaviourPreviewSummaries(string bundlePath)
    {
        var assetsManager = new AssetsManager
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance);
            var internalSerializedFileLookup = AssetsToolsBundleDependencyResolver.BuildInternalSerializedFileLookup(serializedFiles);
            var summaries = new List<MonoBehaviourPreviewProbeSummary>();

            foreach (var serializedFile in serializedFiles)
            {
                foreach (var assetInfo in serializedFile.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
                {
                    var monoBehaviourField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
                    if (monoBehaviourField is null)
                    {
                        continue;
                    }

                    if (!AssetsToolsMonoBehaviourPreviewBuilder.TryBuildMonoBehaviourPreview(
                            assetsManager,
                            serializedFiles,
                            serializedFile,
                            monoBehaviourField,
                            internalSerializedFileLookup,
                            assetInfo.ByteSize,
                            out var previewContent))
                    {
                        continue;
                    }

                    var monoBehaviour = JsonSerializer.Deserialize<AssetPreviewMonoBehaviourDto>(
                        previewContent.Content,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (monoBehaviour is null)
                    {
                        continue;
                    }

                    var assetName = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(monoBehaviourField, "m_Name")) ??
                                    string.Format(CultureInfo.InvariantCulture, "MonoBehaviour #{0}", assetInfo.PathId);
                    summaries.Add(new MonoBehaviourPreviewProbeSummary(
                        BundleName: Path.GetFileName(bundlePath),
                        AssetName: assetName,
                        SerializedFileName: serializedFile.name,
                        PathId: assetInfo.PathId,
                        Preview: monoBehaviour,
                        Distribution: ProbeMonoBehaviourFieldDistribution(monoBehaviourField)));
                }
            }

            return summaries;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[WARN] Skip MonoBehaviour probe for unreadable bundle: {Path.GetFileName(bundlePath)} ({exception.GetType().Name})");
            return Array.Empty<MonoBehaviourPreviewProbeSummary>();
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static IReadOnlyList<AnimatorControllerPreviewProbeSummary> ProbeAnimatorControllerPreviewSummaries(string bundlePath)
    {
        var assetsManager = new AssetsManager
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance);
            var internalSerializedFileLookup = AssetsToolsBundleDependencyResolver.BuildInternalSerializedFileLookup(serializedFiles);
            var summaries = new List<AnimatorControllerPreviewProbeSummary>();

            foreach (var serializedFile in serializedFiles)
            {
                foreach (var assetInfo in serializedFile.file.GetAssetsOfType(AssetClassID.AnimatorController))
                {
                    var animatorControllerField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
                    if (animatorControllerField is null)
                    {
                        continue;
                    }

                    if (!AssetsToolsAnimatorControllerPreviewBuilder.TryBuildAnimatorControllerPreview(
                            assetsManager,
                            serializedFiles,
                            serializedFile,
                            animatorControllerField,
                            internalSerializedFileLookup,
                            assetInfo.ByteSize,
                            out var previewContent))
                    {
                        continue;
                    }

                    var animatorController = JsonSerializer.Deserialize<AssetPreviewAnimatorControllerDto>(
                        previewContent.Content,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (animatorController is null)
                    {
                        continue;
                    }

                    var assetName = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(animatorControllerField, "m_Name")) ??
                                    string.Format(CultureInfo.InvariantCulture, "AnimatorController #{0}", assetInfo.PathId);
                    summaries.Add(new AnimatorControllerPreviewProbeSummary(
                        BundleName: Path.GetFileName(bundlePath),
                        AssetName: assetName,
                        SerializedFileName: serializedFile.name,
                        PathId: assetInfo.PathId,
                        Preview: animatorController));
                }
            }

            return summaries;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[WARN] Skip AnimatorController probe for unreadable bundle: {Path.GetFileName(bundlePath)} ({exception.GetType().Name})");
            return Array.Empty<AnimatorControllerPreviewProbeSummary>();
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static MonoBehaviourFieldDistributionProbe ProbeMonoBehaviourFieldDistribution(AssetTypeValueField monoBehaviourField)
    {
        var totalFieldCount = 0;
        var scalarFieldCount = 0;
        var referenceFieldCount = 0;
        var arrayFieldCount = 0;
        var rootGroups = new Dictionary<string, MonoBehaviourFieldGroupProbeBuilder>(StringComparer.OrdinalIgnoreCase);
        var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var containers = new List<MonoBehaviourContainerProbe>();
        var pendingFields = new Stack<MonoBehaviourFieldProbeReadItem>();

        if (monoBehaviourField.Children is { Count: > 0 } children)
        {
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new MonoBehaviourFieldProbeReadItem(children[index], string.Empty, index));
            }
        }

        while (pendingFields.Count > 0)
        {
            var item = pendingFields.Pop();
            totalFieldCount++;

            var fieldName = ResolveProbeFieldName(item.Field, item.ParentPath, item.Index);
            var fieldPath = string.IsNullOrWhiteSpace(item.ParentPath)
                ? fieldName
                : string.Concat(item.ParentPath, ".", fieldName);
            var rootName = ResolveProbeRootName(fieldPath);
            var group = GetOrCreateProbeGroup(rootGroups, rootName);
            group.TotalFieldCount++;

            if (!string.IsNullOrWhiteSpace(item.Field.TypeName))
            {
                typeCounts[item.Field.TypeName] = typeCounts.GetValueOrDefault(item.Field.TypeName) + 1;
            }

            if (IsProbeAssetPointerField(item.Field))
            {
                referenceFieldCount++;
                group.ReferenceFieldCount++;
                continue;
            }

            if (TryGetProbeArrayChildren(item.Field, out var arrayChildren))
            {
                arrayFieldCount++;
                group.ArrayFieldCount++;
                containers.Add(new MonoBehaviourContainerProbe(
                    Path: fieldPath,
                    TypeName: item.Field.TypeName,
                    ChildCount: arrayChildren.Count));
            }
            else if (IsProbeScalarField(item.Field))
            {
                scalarFieldCount++;
                group.ScalarFieldCount++;
            }

            if (item.Field.Children is not { Count: > 0 } childFields)
            {
                continue;
            }

            for (var index = childFields.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new MonoBehaviourFieldProbeReadItem(childFields[index], fieldPath, index));
            }
        }

        return new MonoBehaviourFieldDistributionProbe(
            TotalFieldCount: totalFieldCount,
            ScalarFieldCount: scalarFieldCount,
            ReferenceFieldCount: referenceFieldCount,
            ArrayFieldCount: arrayFieldCount,
            RootGroups: rootGroups.Values
                .Select(static group => group.Build())
                .OrderBy(static group => group.IsUnityBuiltin)
                .ThenByDescending(static group => group.TotalFieldCount)
                .ThenBy(static group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Containers: containers
                .OrderByDescending(static container => container.ChildCount)
                .ThenBy(static container => container.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TypeCounts: typeCounts
                .OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(static pair => new MonoBehaviourTypeCountProbe(pair.Key, pair.Value))
                .ToArray());
    }

    private static MonoBehaviourFieldGroupProbeBuilder GetOrCreateProbeGroup(
        IDictionary<string, MonoBehaviourFieldGroupProbeBuilder> groups,
        string name)
    {
        if (groups.TryGetValue(name, out var group))
        {
            return group;
        }

        group = new MonoBehaviourFieldGroupProbeBuilder(name, IsUnityBuiltinProbeRootName(name));
        groups[name] = group;
        return group;
    }

    private static bool TryGetProbeArrayChildren(
        AssetTypeValueField field,
        out IReadOnlyList<AssetTypeValueField> children)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(field, "Array");
        if (arrayField?.Children is { Count: > 0 } arrayChildren)
        {
            children = arrayChildren;
            return true;
        }

        children = Array.Empty<AssetTypeValueField>();
        return false;
    }

    private static bool IsProbeScalarField(AssetTypeValueField field)
    {
        return field.Children is not { Count: > 0 } &&
               (IsProbeTextType(field.TypeName) ||
                IsProbeBooleanType(field.TypeName) ||
                IsProbeFloatType(field.TypeName) ||
                IsProbeIntegerType(field.TypeName));
    }

    private static bool IsProbeAssetPointerField(AssetTypeValueField field)
    {
        return field.TypeName.StartsWith("PPtr<", StringComparison.OrdinalIgnoreCase) ||
               (field.Children?.Count == 2 &&
                AssetsToolsFieldReader.TryGetField(field, "m_FileID") is not null &&
                AssetsToolsFieldReader.TryGetField(field, "m_PathID") is not null);
    }

    private static string ResolveProbeFieldName(
        AssetTypeValueField field,
        string parentPath,
        int index)
    {
        var templateName = field.TemplateField?.Name;
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var fieldName = templateName.Trim();
            return string.Equals(fieldName, "data", StringComparison.OrdinalIgnoreCase) &&
                   parentPath.EndsWith(".Array", StringComparison.OrdinalIgnoreCase)
                ? string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", fieldName, index)
                : fieldName;
        }

        return string.Format(CultureInfo.InvariantCulture, "#{0}", index);
    }

    private static string ResolveProbeRootName(string fieldPath)
    {
        var firstSeparatorIndex = fieldPath.IndexOf('.');
        return firstSeparatorIndex > 0
            ? fieldPath[..firstSeparatorIndex]
            : fieldPath;
    }

    private static bool IsUnityBuiltinProbeRootName(string name)
    {
        return name.Equals("m_GameObject", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_Enabled", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_Script", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_Name", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_EditorHideFlags", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_CorrespondingSourceObject", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_PrefabInstance", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("m_PrefabAsset", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProbeTextType(string? typeName) =>
        IsProbeType(typeName, "string") ||
        IsProbeType(typeName, "TypelessData");

    private static bool IsProbeBooleanType(string? typeName) =>
        IsProbeType(typeName, "bool");

    private static bool IsProbeFloatType(string? typeName) =>
        IsProbeType(typeName, "float") ||
        IsProbeType(typeName, "double");

    private static bool IsProbeIntegerType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return typeName.Equals("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("uint", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt8", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt8", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt16", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt16", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt32", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt32", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt64", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt64", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProbeType(string? typeName, string expectedTypeName) =>
        string.Equals(typeName, expectedTypeName, StringComparison.OrdinalIgnoreCase);

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

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

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

    private sealed record MonoBehaviourPreviewProbeSummary(
        string BundleName,
        string AssetName,
        string SerializedFileName,
        long PathId,
        AssetPreviewMonoBehaviourDto Preview,
        MonoBehaviourFieldDistributionProbe Distribution);

    private sealed record AnimatorControllerPreviewProbeSummary(
        string BundleName,
        string AssetName,
        string SerializedFileName,
        long PathId,
        AssetPreviewAnimatorControllerDto Preview);

    private sealed record MonoBehaviourFieldDistributionProbe(
        int TotalFieldCount,
        int ScalarFieldCount,
        int ReferenceFieldCount,
        int ArrayFieldCount,
        IReadOnlyList<MonoBehaviourFieldGroupProbe> RootGroups,
        IReadOnlyList<MonoBehaviourContainerProbe> Containers,
        IReadOnlyList<MonoBehaviourTypeCountProbe> TypeCounts);

    private sealed record MonoBehaviourFieldGroupProbe(
        string Name,
        int TotalFieldCount,
        int ScalarFieldCount,
        int ReferenceFieldCount,
        int ArrayFieldCount,
        bool IsUnityBuiltin);

    private sealed record MonoBehaviourContainerProbe(
        string Path,
        string? TypeName,
        int ChildCount);

    private sealed record MonoBehaviourTypeCountProbe(
        string TypeName,
        int Count);

    private sealed record MonoBehaviourFieldProbeReadItem(
        AssetTypeValueField Field,
        string ParentPath,
        int Index);

    private sealed class MonoBehaviourFieldGroupProbeBuilder
    {
        public MonoBehaviourFieldGroupProbeBuilder(string name, bool isUnityBuiltin)
        {
            Name = name;
            IsUnityBuiltin = isUnityBuiltin;
        }

        public string Name { get; }

        public bool IsUnityBuiltin { get; }

        public int TotalFieldCount { get; set; }

        public int ScalarFieldCount { get; set; }

        public int ReferenceFieldCount { get; set; }

        public int ArrayFieldCount { get; set; }

        public MonoBehaviourFieldGroupProbe Build() =>
            new(
                Name,
                TotalFieldCount,
                ScalarFieldCount,
                ReferenceFieldCount,
                ArrayFieldCount,
                IsUnityBuiltin);
    }
}
