using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Reading;

/// <summary>
/// 基于 AssetsTools.NET 的真实 Unity 资源包 Reader。
/// </summary>
/// <remarks>
/// 当前阶段先稳定产出“资源包列表 + 资产列表 + 基础详情摘要”所需的轻量读模型，
/// 更深入的依赖解析、预览派生和写回能力仍后置到后续里程碑。
/// </remarks>
public sealed class AssetsToolsAssetBundleReader : IAssetBundleReader
{
    // 资源包索引当前新增原始压缩格式，保存导出需要从缓存传递该值而不是在写包阶段重新推断，
    // 需要提升版本以触发旧索引缓存自动重建。
    private const string ParserVersion = "assetstools.net/3.0.4-dependency-graph.8";

    private static readonly HashSet<AssetClassID> MetadataOnlyAssetClassIds =
    [
        AssetClassID.AssetBundle,
        AssetClassID.AssetBundleManifest,
        AssetClassID.ResourceManager,
        AssetClassID.MonoManager,
        AssetClassID.PreloadData
    ];

    private static readonly HashSet<AssetClassID> AlwaysIncludeAssetClassIds =
    [
        AssetClassID.Texture2D,
        AssetClassID.Sprite,
        AssetClassID.TextAsset,
        AssetClassID.AudioClip,
        AssetClassID.Mesh,
        AssetClassID.Material,
        AssetClassID.GameObject,
        AssetClassID.MonoBehaviour,
        AssetClassID.AnimationClip,
        AssetClassID.AnimatorController,
        AssetClassID.Shader,
        AssetClassID.Font,
        AssetClassID.SpriteAtlas
    ];

    private readonly AssetsToolsBundleLoadedSessionCache _bundleSessionCache;

    public AssetsToolsAssetBundleReader()
        : this(AssetsToolsBundleLoadedSessionCache.Shared)
    {
    }

    internal AssetsToolsAssetBundleReader(AssetsToolsBundleLoadedSessionCache bundleSessionCache)
    {
        _bundleSessionCache = bundleSessionCache ?? throw new ArgumentNullException(nameof(bundleSessionCache));
    }

    public string CacheParserVersion => ParserVersion;

    public bool CanRead(string bundlePath) => UnityAssetBundleFileInspector.CanTreatAsBundle(bundlePath);

    public bool SupportsCacheParserVersion(string parserVersion)
    {
        return string.Equals(parserVersion, ParserVersion, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Result<AssetBundleReadResultDto>> ReadAsync(
        AssetBundleReadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetBundleReadResultDto>.Cancelled();
        }

        if (!File.Exists(request.BundlePath))
        {
            return Result<AssetBundleReadResultDto>.Failure(
                ApplicationErrors.BundleSourceMissing(request.BundlePath));
        }

        if (!CanRead(request.BundlePath))
        {
            return Result<AssetBundleReadResultDto>.Failure(
                ApplicationErrors.BundleReadFormatUnrecognized(request.BundlePath));
        }

        AssetsToolsBundleLoadedSessionLease? sessionLease = null;
        try
        {
            sessionLease = await _bundleSessionCache.AcquireAsync(
                request.BundlePath,
                request.BundleSessionCacheKey,
                cancellationToken);
            var session = sessionLease.Session;
            await session.ExecutionGate.WaitAsync(cancellationToken);
            try
            {
                var logicalPath = UnityAssetBundleFileInspector.NormalizeLogicalPath(request.LogicalPath, request.BundlePath);
                var readGraph = ReadBundleGraph(session, cancellationToken);
                var result = new AssetBundleReadResultDto(
                    DisplayName: Path.GetFileName(request.BundlePath),
                    LogicalPath: logicalPath,
                    ParserVersion: CacheParserVersion,
                    Dependencies: readGraph.BundleDependencies,
                    Assets: readGraph.Assets,
                    SerializedFileNames: readGraph.SerializedFileNames,
                    OriginalCompression: session.OriginalCompression);

                return Result<AssetBundleReadResultDto>.Success(result);
            }
            finally
            {
                session.ExecutionGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return Result<AssetBundleReadResultDto>.Cancelled();
        }
        catch (Exception)
        {
            return Result<AssetBundleReadResultDto>.Failure(
                ApplicationErrors.BundleReadUnexpected(request.BundlePath));
        }
        finally
        {
            sessionLease?.Dispose();
        }
    }

    private static BundleReadGraph ReadBundleGraph(
        AssetsToolsBundleLoadedSession session,
        CancellationToken cancellationToken)
    {
        var serializedFiles = session.SerializedFiles;
        var containerLookup = BuildContainerLookup(session.AssetsManager, serializedFiles, cancellationToken);
        var rawAssets = CollectRawAssets(session.AssetsManager, serializedFiles, containerLookup, cancellationToken);
        var assetLookup = rawAssets.ToDictionary(
            static asset => new AssetsToolsAssetObjectKey(asset.SerializedFileName, asset.PathId),
            static asset => new AssetsToolsResolvedAssetDescriptor(
                AssetId: asset.AssetId,
                AssetName: asset.AssetName,
                AssetType: asset.AssetType.ToString(),
                AssetTypeDisplayName: asset.AssetTypeDisplayName,
                SerializedFileName: asset.SerializedFileName,
                PathId: asset.PathId,
                SerializedFile: asset.SerializedFile,
                BaseField: asset.BaseField));

        var assets = rawAssets
            .Select(asset => new AssetBundleReadAssetDto(
                AssetId: asset.AssetId,
                AssetName: asset.AssetName,
                AssetType: asset.AssetType.ToString(),
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
                Dependencies: AssetsToolsAssetDependencyResolver.ResolveDependencies(
                    asset.AssetId,
                    asset.SerializedFile,
                    asset.BaseField,
                    asset.AssetTypeDisplayName,
                    assetLookup,
                    session.InternalSerializedFileLookup),
                IncludeInAssetList: asset.IncludeInAssetList))
            .OrderBy(static asset => asset.AssetPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BundleReadGraph(
            SerializedFileNames: serializedFiles
                .Select(static file => file.name)
                .Where(static fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static fileName => fileName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Assets: assets,
            BundleDependencies: AssetsToolsBundleDependencyResolver.ResolveBundleDependencies(serializedFiles));
    }

    private static IReadOnlyDictionary<AssetsToolsAssetObjectKey, string> BuildContainerLookup(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        CancellationToken cancellationToken)
    {
        var containerLookup = new Dictionary<AssetsToolsAssetObjectKey, string>();

        foreach (var serializedFile in serializedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var assetBundleInfo in serializedFile.file.GetAssetsOfType(AssetClassID.AssetBundle))
            {
                var assetBundleField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetBundleInfo);
                var containerArray = AssetsToolsFieldReader.TryGetField(assetBundleField, "m_Container", "Array");
                if (containerArray is null)
                {
                    continue;
                }

                foreach (var entry in containerArray.Children)
                {
                    var containerPath = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "first")) ??
                                        AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "key"));
                    if (string.IsNullOrWhiteSpace(containerPath))
                    {
                        continue;
                    }

                    var assetPointer = AssetsToolsFieldReader.TryGetField(entry, "second", "asset") ??
                                       AssetsToolsFieldReader.TryGetField(entry, "value", "asset") ??
                                       AssetsToolsFieldReader.TryGetField(entry, "asset");
                    if (assetPointer is null)
                    {
                        continue;
                    }

                    var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(assetPointer, "m_FileID")) ?? 0;
                    var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(assetPointer, "m_PathID"));
                    if (pathId is null || fileId != 0)
                    {
                        continue;
                    }

                    var assetKey = new AssetsToolsAssetObjectKey(serializedFile.name, pathId.Value);
                    containerLookup[assetKey] = containerPath.Trim().Replace('\\', '/');
                }
            }
        }

        return containerLookup;
    }

    private static IReadOnlyList<RawAssetReadEntry> CollectRawAssets(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        IReadOnlyDictionary<AssetsToolsAssetObjectKey, string> containerLookup,
        CancellationToken cancellationToken)
    {
        var rawAssets = new List<RawAssetReadEntry>();

        foreach (var serializedFile in serializedFiles)
        {
            foreach (var assetInfo in serializedFile.file.AssetInfos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var classId = ResolveClassId(assetInfo.TypeId);
                if (MetadataOnlyAssetClassIds.Contains(classId))
                {
                    continue;
                }

                var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
                var containerPath = ResolveContainerPath(containerLookup, serializedFile.name, assetInfo.PathId);
                var assetType = MapAssetType(classId);
                var assetTypeDisplayName = ResolveAssetTypeDisplayName(classId, assetInfo.TypeId);
                var rawAssetName = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name"));
                var assetName = ResolveAssetName(rawAssetName, containerPath, assetTypeDisplayName, assetInfo.PathId);

                rawAssets.Add(new RawAssetReadEntry(
                    AssetId: AssetsToolsAssetIdFactory.CreateAssetId(serializedFile.name, assetInfo.PathId),
                    SerializedFile: serializedFile,
                    SerializedFileName: serializedFile.name,
                    PathId: assetInfo.PathId,
                    BaseField: baseField,
                    AssetName: assetName,
                    AssetPath: ResolveAssetPath(containerPath, serializedFile.name, assetName, assetTypeDisplayName, assetInfo.PathId),
                    ContainerPath: containerPath,
                    AssetType: assetType,
                    AssetTypeDisplayName: assetTypeDisplayName,
                    SizeInBytes: assetInfo.ByteSize,
                    PreferredDisplaySizeInBytes: AssetsToolsAssetDisplayMetadataResolver.ResolvePreferredDisplaySizeInBytes(classId, baseField),
                    HasPreview: IsPreviewAvailable(classId),
                    CanExtract: CanExtract(classId),
                    CanReplace: CanReplace(assetType),
                    IncludeInAssetList: ShouldIncludeAsset(classId, rawAssetName, containerPath)));
            }
        }

        return rawAssets;
    }

    private static string? ResolveContainerPath(
        IReadOnlyDictionary<AssetsToolsAssetObjectKey, string> containerLookup,
        string serializedFileName,
        long pathId)
    {
        return containerLookup.TryGetValue(new AssetsToolsAssetObjectKey(serializedFileName, pathId), out var containerPath)
            ? containerPath
            : null;
    }

    private static string ResolveAssetName(
        string? rawAssetName,
        string? containerPath,
        string assetTypeDisplayName,
        long pathId)
    {
        if (!string.IsNullOrWhiteSpace(rawAssetName))
        {
            return rawAssetName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(containerPath))
        {
            var containerName = Path.GetFileNameWithoutExtension(containerPath);
            if (string.IsNullOrWhiteSpace(containerName))
            {
                containerName = containerPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(containerName))
            {
                return containerName;
            }
        }

        return $"{assetTypeDisplayName} #{pathId}";
    }

    private static string ResolveAssetPath(
        string? containerPath,
        string serializedFileName,
        string assetName,
        string assetTypeDisplayName,
        long pathId)
    {
        if (!string.IsNullOrWhiteSpace(containerPath))
        {
            return containerPath;
        }

        var normalizedSerializedFileName = string.IsNullOrWhiteSpace(serializedFileName)
            ? "bundle"
            : serializedFileName.Trim().Replace('\\', '/');
        var safeName = string.IsNullOrWhiteSpace(assetName)
            ? $"{assetTypeDisplayName} #{pathId}"
            : assetName.Trim();
        return $"{normalizedSerializedFileName}/{safeName}";
    }

    private static string ResolveAssetTypeDisplayName(AssetClassID classId, int rawTypeId)
    {
        if (classId != default || rawTypeId == 0)
        {
            return classId.ToString();
        }

        return $"Type {rawTypeId}";
    }

    private static AssetClassID ResolveClassId(int rawTypeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), rawTypeId)
            ? (AssetClassID)rawTypeId
            : 0;
    }

    private static AssetType MapAssetType(AssetClassID classId)
    {
        return classId switch
        {
            AssetClassID.Texture2D => AssetType.Texture2D,
            AssetClassID.Sprite => AssetType.Sprite,
            AssetClassID.TextAsset => AssetType.TextAsset,
            AssetClassID.AudioClip => AssetType.AudioClip,
            AssetClassID.Mesh => AssetType.Mesh,
            AssetClassID.Material => AssetType.Material,
            _ => AssetType.Unknown
        };
    }

    private static bool ShouldIncludeAsset(
        AssetClassID classId,
        string? rawAssetName,
        string? containerPath)
    {
        if (AlwaysIncludeAssetClassIds.Contains(classId))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(containerPath) ||
               !string.IsNullOrWhiteSpace(rawAssetName);
    }

    private static bool IsPreviewAvailable(AssetClassID classId)
    {
        // 预览链路可以稳定回退到结构化摘要，因此只要不是纯元数据对象，
        // 就认为它具备“可预览”的基础能力。
        return !MetadataOnlyAssetClassIds.Contains(classId);
    }

    private static bool CanExtract(AssetClassID classId)
    {
        return !MetadataOnlyAssetClassIds.Contains(classId);
    }

    private static bool CanReplace(AssetType assetType)
    {
        return assetType is not AssetType.Unknown;
    }

    private sealed record BundleReadGraph(
        IReadOnlyList<string> SerializedFileNames,
        IReadOnlyList<AssetBundleReadAssetDto> Assets,
        IReadOnlyList<string> BundleDependencies);

    private sealed record RawAssetReadEntry(
        string AssetId,
        AssetsFileInstance SerializedFile,
        string SerializedFileName,
        long PathId,
        AssetTypeValueField? BaseField,
        string AssetName,
        string AssetPath,
        string? ContainerPath,
        AssetType AssetType,
        string AssetTypeDisplayName,
        long SizeInBytes,
        long? PreferredDisplaySizeInBytes,
        bool HasPreview,
        bool CanExtract,
        bool CanReplace,
        bool IncludeInAssetList);
}
