using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    private static readonly JsonSerializerOptions TestMeshPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions TestMaterialPreviewJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private static GetAssetPreviewQuery CreateQuery()
    {
        return new GetAssetPreviewQuery(
            WorkspaceId: new WorkspaceId("workspace-session-1"),
            WorkspaceKey: "workspace-key-1",
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            BundleId: new BundleId("bundle.001"),
            AssetId: new AssetId("asset.001"));
    }

    private static BundleIndexCacheEntryDto CreateBundleCacheEntry(
        string workspaceKey,
        string bundleId,
        string assetId)
    {
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: "bundle-cache-001",
            BundleId: bundleId,
            DisplayName: "characters",
            LogicalPath: "characters.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: @"C:\Bundles\characters.ab",
                FileSizeInBytes: 1024,
                LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero),
                ContentHash: "hash-characters"),
            CachedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 5, 0, TimeSpan.Zero),
            ParserVersion: "parser-1",
            ToolVersion: "tool-1",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                new BundleIndexAssetCacheEntryDto(
                    AssetId: assetId,
                    AssetName: "HeroPortrait",
                    AssetType: AssetType.Texture2D.ToString(),
                    AssetPath: "Assets/HeroPortrait.png",
                    ContainerPath: null,
                    SizeInBytes: 256,
                    PreferredDisplaySizeInBytes: 256,
                    HasPreview: true,
                    CanExtract: true,
                    CanReplace: true,
                    SerializedFileName: "characters.assets",
                    PathId: 12345,
                    AssetTypeDisplayName: "Texture2D")
            ],
            DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            DecryptorVersion: "1.0.0");
    }

    private static AssetBundleDecryptorDescriptorDto CreatePassThroughDecryptorDescriptor()
    {
        return new AssetBundleDecryptorDescriptorDto(
            AssetBundleDecryptorIds.BuiltInPassThrough,
            "PassThrough",
            "1.0.0",
            AssetBundleDecryptorKind.BuiltIn,
            SupportsAutoDetection: false,
            IsFallback: true,
            Description: "fallback");
    }

    private static PreviewCachePayloadDto CreateTextPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string textContent,
        string generatorVersion)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Text.ToString(),
                ContentType: "text/plain; charset=utf-8",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.txt"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion),
            Encoding.UTF8.GetBytes(textContent));
    }

    private static PreviewCachePayloadDto CreateImagePreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewImageOverlayDto? imageOverlay,
        AssetPreviewImageFocusDto? imageFocus = null,
        byte[]? imageFocusContent = null)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Image.ToString(),
                ContentType: "image/png",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.png"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                ImageOverlay: imageOverlay,
                ImageFocus: imageFocus),
            [0x89, 0x50, 0x4E, 0x47],
            imageFocusContent);
    }

    private static PreviewCachePayloadDto CreateMeshPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewMeshGeometryDto meshGeometry)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Mesh.ToString(),
                ContentType: "application/vnd.assestbundletools.mesh-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.mesh.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.MeshVertexCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: meshGeometry.SourceVertexCount)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                meshGeometry,
                TestMeshPreviewJsonSerializerOptions));
    }

    private static PreviewCachePayloadDto CreateMaterialPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string generatorVersion,
        AssetPreviewMaterialDto material)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Material.ToString(),
                ContentType: "application/vnd.assestbundletools.material-preview+json",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.material.json"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion,
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        AssetPreviewInfoKey.MaterialTexturePropertyCount,
                        AssetPreviewInfoValueKind.Integer,
                        Int64Value: material.TextureProperties.Count)
                ]),
            JsonSerializer.SerializeToUtf8Bytes(
                material,
                TestMaterialPreviewJsonSerializerOptions));
    }

    private static AssetPreviewMaterialDto CreateMaterialPreview()
    {
        return new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: new AssetPreviewMaterialShaderDto(
                FileId: 0,
                PathId: 100,
                Name: "Particles/Additive",
                SerializedFileName: "materials.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            TextureProperties:
            [
                new AssetPreviewMaterialTexturePropertyDto(
                    Name: "_MainTex",
                    FileId: 2,
                    PathId: 200,
                    SerializedFileName: "CAB-textures",
                    ExternalReferencePath: "archive:/CAB-textures/CAB-textures",
                    IsExternal: true,
                    ScaleX: 1,
                    ScaleY: 1,
                    OffsetX: 0,
                    OffsetY: 0)
            ],
            FloatProperties:
            [
                new AssetPreviewMaterialFloatPropertyDto("_Glossiness", 0.5d)
            ],
            IntegerProperties:
            [
                new AssetPreviewMaterialIntegerPropertyDto("_Cull", 2)
            ],
            ColorProperties:
            [
                new AssetPreviewMaterialColorPropertyDto("_Color", 1, 0.25d, 0.5d, 1)
            ]);
    }

    private static AssetPreviewMeshGeometryDto CreateMeshGeometry()
    {
        return new AssetPreviewMeshGeometryDto(
            SchemaVersion: 3,
            SourceVertexCount: 3,
            SourceIndexCount: 3,
            SourceTriangleCount: 1,
            IsGeometryTruncated: false,
            Vertices:
            [
                new AssetPreviewMeshVertexDto(0, 0, 0),
                new AssetPreviewMeshVertexDto(1, 0, 0),
                new AssetPreviewMeshVertexDto(0, 1, 0)
            ],
            Edges:
            [
                new AssetPreviewMeshEdgeDto(0, 1),
                new AssetPreviewMeshEdgeDto(1, 2),
                new AssetPreviewMeshEdgeDto(0, 2)
            ],
            SubMeshes:
            [
                new AssetPreviewMeshSubMeshDto(
                    Index: 0,
                    FirstIndexByte: 0,
                    IndexCount: 3,
                    Topology: 0,
                    BaseVertex: 0,
                    FirstVertex: 0,
                    VertexCount: 3)
            ],
            Bounds: new AssetPreviewMeshBoundsDto(
                new AssetPreviewMeshVertexDto(0.5, 0.5, 0),
                new AssetPreviewMeshVertexDto(0.5, 0.5, 0)),
            Uv0:
            [
                new AssetPreviewMeshUvDto(0, 0),
                new AssetPreviewMeshUvDto(1, 0),
                new AssetPreviewMeshUvDto(0, 1)
            ],
            Channels:
            [
                new AssetPreviewMeshChannelDto(
                    Index: 0,
                    Stream: 0,
                    Offset: 0,
                    Format: 0,
                    Dimension: 3,
                    IsPreviewReadable: true),
                new AssetPreviewMeshChannelDto(
                    Index: 4,
                    Stream: 0,
                    Offset: 12,
                    Format: 0,
                    Dimension: 2,
                    IsPreviewReadable: true)
            ],
            Triangles:
            [
                new AssetPreviewMeshTriangleDto(0, 1, 2, 0)
            ]);
    }

    private static string CreatePreviewCacheKey(string bundleCacheKey, string assetId)
    {
        var normalizedKey = $"{bundleCacheKey.ToLowerInvariant()}::{assetId.ToLowerInvariant()}";
        var bytes = Encoding.UTF8.GetBytes(normalizedKey);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) => throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => true;

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => true;

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) => true;

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            bundleTree = null!;
            throw new NotSupportedException();
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            out AssetListResultDto? assetListResult)
        {
            assetListResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            assetDetails = null;
            throw new NotSupportedException();
        }

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = null;
            throw new NotSupportedException();
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            extractionResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
        {
            candidates = null!;
            throw new NotSupportedException();
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
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            undoResult = null;
            throw new NotSupportedException();
        }

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext)
        {
            paneContext = null;
            throw new NotSupportedException();
        }
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IPreviewCacheStore _previewCacheStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IPreviewCacheStore previewCacheStore)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _previewCacheStore = previewCacheStore;
        }

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) => _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) => _previewCacheStore;

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly IReadOnlyList<BundleIndexCacheEntryDto> _entries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> entries)
        {
            _entries = entries;
        }

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) => _entries;

        public void Save(BundleIndexCacheEntryDto cacheEntry) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestPreviewCacheStore : IPreviewCacheStore
    {
        private PreviewCachePayloadDto? _payload;
        private string? _resolvedPayloadFilePath;

        public TestPreviewCacheStore(
            PreviewCachePayloadDto? payload = null,
            string? resolvedPayloadFilePath = null)
        {
            _payload = payload;
            _resolvedPayloadFilePath = resolvedPayloadFilePath;
        }

        public int SaveCallCount { get; private set; }

        public PreviewCachePayloadDto? LastSavedPayload { get; private set; }

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey)
        {
            return _payload is not null &&
                   string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(_payload.Descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase)
                ? _payload
                : null;
        }

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey)
        {
            return _payload is not null &&
                   string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase)
                ? [_payload.Descriptor]
                : Array.Empty<PreviewCacheDescriptorDto>();
        }

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor)
        {
            return _payload is not null &&
                   string.Equals(_payload.Descriptor.PreviewCacheKey, descriptor.PreviewCacheKey, StringComparison.OrdinalIgnoreCase)
                ? _resolvedPayloadFilePath
                : null;
        }

        public void Save(PreviewCachePayloadDto previewCachePayload)
        {
            SaveCallCount++;
            LastSavedPayload = previewCachePayload;
            _payload = previewCachePayload;
            _resolvedPayloadFilePath ??= Path.Combine(@"D:\WorkspaceData\preview-cache", previewCachePayload.Descriptor.RelativePayloadPath);
        }

        public void Delete(string workspaceKey, string previewCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();

        public string? GetSavedTextContent()
        {
            return LastSavedPayload is null
                ? null
                : Encoding.UTF8.GetString(LastSavedPayload.Content);
        }
    }

    private sealed class TestAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
    {
        private readonly IAssetBundleDecryptor _decryptor;

        public TestAssetBundleDecryptorRegistry(IAssetBundleDecryptor decryptor)
        {
            _decryptor = decryptor;
        }

        public IReadOnlyList<IAssetBundleDecryptor> GetAll() => [_decryptor];

        public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
        {
            decryptor = _decryptor;
            return string.Equals(_decryptor.Descriptor.Id, decryptorId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class TestAssetBundleDecryptor : IAssetBundleDecryptor
    {
        public TestAssetBundleDecryptor(AssetBundleDecryptorDescriptorDto descriptor)
        {
            Descriptor = descriptor;
        }

        public AssetBundleDecryptorDescriptorDto Descriptor { get; }

        public Func<AssetBundleDecryptionRequestDto, Task<Result<AssetBundleDecryptionResultDto>>>? DecryptResultFactory { get; init; }

        public int DecryptCallCount { get; private set; }

        public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
            AssetBundleDecryptorProbeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
            AssetBundleDecryptionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            DecryptCallCount++;

            if (DecryptResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return DecryptResultFactory.Invoke(request);
        }
    }

    private sealed class TestAssetPreviewGenerator : IAssetPreviewGenerator
    {
        public string GeneratorVersion => "generator-1";

        public Func<AssetPreviewBuildRequestDto, Task<Result<AssetPreviewContentDto>>>? GenerateResultFactory { get; init; }

        public int GenerateCallCount { get; private set; }

        public Task<Result<AssetPreviewContentDto>> GenerateAsync(
            AssetPreviewBuildRequestDto request,
            CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;

            if (GenerateResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return GenerateResultFactory.Invoke(request);
        }
    }

    private sealed class TestAudioPayloadTranscodingService : IAudioPayloadTranscodingService
    {
        private readonly IReadOnlyList<AudioPayloadTranscoderDescriptorDto> _descriptors;

        public TestAudioPayloadTranscodingService(
            IReadOnlyList<AudioPayloadTranscoderDescriptorDto>? descriptors = null)
        {
            _descriptors = descriptors ?? Array.Empty<AudioPayloadTranscoderDescriptorDto>();
        }

        public IReadOnlyList<AudioPayloadTranscoderDescriptorDto> GetAvailableDescriptors() => _descriptors;

        public AudioPayloadTranscoderDescriptorDto? ResolveDescriptor(string transcoderId)
        {
            return _descriptors.FirstOrDefault(descriptor =>
                string.Equals(descriptor.Id, transcoderId, StringComparison.OrdinalIgnoreCase));
        }

        public Task<AudioPayloadTranscodeResultDto> TryTranscodeAsync(
            AudioPayloadTranscodeRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
