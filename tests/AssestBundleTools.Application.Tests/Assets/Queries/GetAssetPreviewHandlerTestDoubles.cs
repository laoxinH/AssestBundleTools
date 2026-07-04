using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly Dictionary<string, AssetDetailsDto> _assetDetailsByKey = new(StringComparer.Ordinal);

        public TestWorkspaceSessionRegistry(
            IReadOnlyList<AssetDetailsDto>? assetDetails = null)
        {
            foreach (var details in assetDetails ?? Array.Empty<AssetDetailsDto>())
            {
                _assetDetailsByKey[CreateAssetDetailsKey(details.BundleId, details.AssetId)] = details;
            }
        }

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
            string? assetTypeDisplayNameFilter,
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
            if (_assetDetailsByKey.TryGetValue(CreateAssetDetailsKey(bundleId, assetId), out assetDetails))
            {
                return true;
            }

            assetDetails = CreateDefaultAssetDetails(workspaceId, bundleId, assetId);
            return true;
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

        private static AssetDetailsDto CreateDefaultAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId) =>
            new(
                WorkspaceId: workspaceId,
                BundleId: bundleId,
                AssetId: assetId,
                Name: assetId.Value,
                AssetPath: $"Assets/{assetId.Value}.png",
                ContainerPath: "Assets",
                AssetType: AssetType.Texture2D,
                SizeInBytes: 256,
                PreferredDisplaySizeInBytes: 256,
                PathId: 12345,
                IsModified: false,
                CanExtract: true,
                CanReplaceFromFile: true,
                CanReplaceFromWorkspace: true,
                WorkspaceReplacementSourceBundleDisplayName: null,
                WorkspaceReplacementSourceAssetName: null,
                WorkspaceReplacementSourceAssetType: null,
                FileReplacementSourceFileName: null,
                FileReplacementSourcePath: null,
                FileReplacementSourceAssetType: null,
                LastExtractedAtUtc: null,
                Dependencies: Array.Empty<AssetDependencyDto>(),
                AssetTypeDisplayName: AssetType.Texture2D.ToString());

        private static string CreateAssetDetailsKey(BundleId bundleId, AssetId assetId) =>
            string.Concat(bundleId.Value, "\n", assetId.Value);
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleArtifactStore _bundleArtifactStore;
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IPreviewCacheStore _previewCacheStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IPreviewCacheStore previewCacheStore,
            IBundleArtifactStore? bundleArtifactStore = null)
        {
            _bundleArtifactStore = bundleArtifactStore ?? new TestBundleArtifactStore();
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _previewCacheStore = previewCacheStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) => _bundleArtifactStore;

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) => _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) => _previewCacheStore;

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleArtifactStore : IBundleArtifactStore
    {
        private readonly object _syncRoot = new();
        private readonly List<BundleArtifactManifestDto> _manifests;

        public TestBundleArtifactStore(IReadOnlyList<BundleArtifactManifestDto>? manifests = null)
        {
            _manifests = manifests?.ToList() ?? [];
        }

        private int _saveCallCount;

        public int SaveCallCount => Volatile.Read(ref _saveCallCount);

        private int _updateLastAccessedCallCount;

        public int UpdateLastAccessedCallCount => Volatile.Read(ref _updateLastAccessedCallCount);

        public BundleArtifactManifestDto? LastAccessedManifest { get; private set; }

        public BundleArtifactPruneRequestDto? LastPruneRequest { get; private set; }

        private int _pruneCallCount;

        public int PruneCallCount => Volatile.Read(ref _pruneCallCount);

        public BundleArtifactManifestDto? Load(string workspaceKey, string bundleCacheKey)
        {
            lock (_syncRoot)
            {
                return _manifests.FirstOrDefault(manifest =>
                    string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(manifest.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));
            }
        }

        public string? ResolveBundlePath(BundleArtifactManifestDto manifest) => manifest.ResolvedBundlePath;

        public BundleArtifactManifestDto Save(BundleArtifactSaveRequestDto request)
        {
            Interlocked.Increment(ref _saveCallCount);
            var manifest = new BundleArtifactManifestDto(
                WorkspaceKey: request.WorkspaceKey,
                BundleCacheKey: request.BundleCacheKey,
                ResolvedBundlePath: request.ResolvedBundlePath,
                IsMaterialized: request.ProducedDerivedFile,
                SourceFingerprint: request.SourceFingerprint,
                BundleFingerprint: request.ResolvedBundleFingerprint,
                DecryptorId: request.DecryptorId,
                DecryptorVersion: request.DecryptorVersion,
                NormalizerVersion: request.NormalizerVersion,
                CreatedAtUtc: request.CreatedAtUtc,
                LastAccessedAtUtc: request.CreatedAtUtc);
            lock (_syncRoot)
            {
                _manifests.RemoveAll(existingManifest =>
                    string.Equals(existingManifest.WorkspaceKey, request.WorkspaceKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existingManifest.BundleCacheKey, request.BundleCacheKey, StringComparison.OrdinalIgnoreCase));
                _manifests.Add(manifest);
            }

            return manifest;
        }

        public BundleArtifactManifestDto UpdateLastAccessed(
            BundleArtifactManifestDto manifest,
            DateTimeOffset lastAccessedAtUtc)
        {
            Interlocked.Increment(ref _updateLastAccessedCallCount);
            var updatedManifest = manifest with
            {
                LastAccessedAtUtc = lastAccessedAtUtc,
            };
            lock (_syncRoot)
            {
                LastAccessedManifest = updatedManifest;
                var index = _manifests.FindIndex(existingManifest =>
                    string.Equals(existingManifest.WorkspaceKey, manifest.WorkspaceKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existingManifest.BundleCacheKey, manifest.BundleCacheKey, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    _manifests[index] = updatedManifest;
                }
            }

            return updatedManifest;
        }

        public BundleArtifactCacheUsageDto GetUsage(string? workspaceKey = null)
        {
            lock (_syncRoot)
            {
                var entries = _manifests
                    .Where(manifest => string.IsNullOrWhiteSpace(workspaceKey) ||
                                       string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
                    .Select(static manifest => new BundleArtifactCacheEntryDto(
                        WorkspaceKey: manifest.WorkspaceKey,
                        BundleCacheKey: manifest.BundleCacheKey,
                        SizeInBytes: 0,
                        CreatedAtUtc: manifest.CreatedAtUtc,
                        LastAccessedAtUtc: manifest.LastAccessedAtUtc,
                        IsMaterialized: manifest.IsMaterialized))
                    .ToArray();
                return new BundleArtifactCacheUsageDto(0, entries.Length, entries);
            }
        }

        public BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request)
        {
            Interlocked.Increment(ref _pruneCallCount);
            LastPruneRequest = request;
            return new(0, 0, 0, 0, Array.Empty<BundleArtifactCacheEntryDto>());
        }

        public void Delete(string workspaceKey, string bundleCacheKey)
        {
            lock (_syncRoot)
            {
                _manifests.RemoveAll(manifest =>
                    string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(manifest.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void DeleteWorkspace(string workspaceKey)
        {
            lock (_syncRoot)
            {
                _manifests.RemoveAll(manifest =>
                    string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase));
            }
        }
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
        private readonly object _syncRoot = new();
        private PreviewCachePayloadDto? _payload;
        private string? _resolvedPayloadFilePath;

        public TestPreviewCacheStore(
            PreviewCachePayloadDto? payload = null,
            string? resolvedPayloadFilePath = null)
        {
            _payload = payload;
            _resolvedPayloadFilePath = resolvedPayloadFilePath;
        }

        private int _saveCallCount;

        public int SaveCallCount => Volatile.Read(ref _saveCallCount);

        private int _loadCallCount;

        public int LoadCallCount => Volatile.Read(ref _loadCallCount);

        public PreviewCachePayloadDto? LastSavedPayload { get; private set; }

        public PreviewCachePruneRequestDto? LastPruneRequest { get; private set; }

        private int _pruneCallCount;

        public int PruneCallCount => Volatile.Read(ref _pruneCallCount);

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey)
        {
            Interlocked.Increment(ref _loadCallCount);
            lock (_syncRoot)
            {
                return _payload is not null &&
                       string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(_payload.Descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase)
                    ? _payload
                    : null;
            }
        }

        public PreviewCacheTextChunkDto? LoadTextChunk(string workspaceKey, string previewCacheKey, int offset, int length)
        {
            lock (_syncRoot)
            {
                if (_payload is null ||
                    !string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(_payload.Descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase) ||
                    offset < 0 ||
                    length <= 0)
                {
                    return null;
                }

                var fullText = Encoding.UTF8.GetString(_payload.Content)
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');
                var safeOffset = Math.Min(offset, fullText.Length);
                var safeLength = Math.Min(length, fullText.Length - safeOffset);
                var text = safeLength <= 0
                    ? string.Empty
                    : fullText.Substring(safeOffset, safeLength);

                return new PreviewCacheTextChunkDto(
                    _payload.Descriptor,
                    text,
                    safeOffset,
                    safeLength,
                    fullText.Length,
                    _payload.Content.LongLength);
            }
        }

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey)
        {
            lock (_syncRoot)
            {
                return _payload is not null &&
                       string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase)
                    ? [_payload.Descriptor]
                    : Array.Empty<PreviewCacheDescriptorDto>();
            }
        }

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor)
        {
            lock (_syncRoot)
            {
                return _payload is not null &&
                       string.Equals(_payload.Descriptor.PreviewCacheKey, descriptor.PreviewCacheKey, StringComparison.OrdinalIgnoreCase)
                    ? _resolvedPayloadFilePath
                    : null;
            }
        }

        public void Save(PreviewCachePayloadDto previewCachePayload)
        {
            Interlocked.Increment(ref _saveCallCount);
            lock (_syncRoot)
            {
                LastSavedPayload = previewCachePayload;
                _payload = previewCachePayload;
                _resolvedPayloadFilePath ??= Path.Combine(@"D:\WorkspaceData\preview-cache", previewCachePayload.Descriptor.RelativePayloadPath);
            }
        }

        public PreviewCachePruneResultDto Prune(PreviewCachePruneRequestDto request)
        {
            Interlocked.Increment(ref _pruneCallCount);
            LastPruneRequest = request;
            return new PreviewCachePruneResultDto(
                SizeBeforeInBytes: 0,
                SizeAfterInBytes: 0,
                EntryCountBefore: 0,
                EntryCountAfter: 0,
                PrunedEntries: Array.Empty<PreviewCacheEntryDto>());
        }

        public void Delete(string workspaceKey, string previewCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();

        public string? GetSavedTextContent()
        {
            lock (_syncRoot)
            {
                return LastSavedPayload is null
                    ? null
                    : Encoding.UTF8.GetString(LastSavedPayload.Content);
            }
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

        private int _decryptCallCount;

        public int DecryptCallCount => Volatile.Read(ref _decryptCallCount);

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
            Interlocked.Increment(ref _decryptCallCount);

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

        private int _generateCallCount;

        public int GenerateCallCount => Volatile.Read(ref _generateCallCount);

        public AssetPreviewBuildRequestDto? LastRequest { get; private set; }

        public Task<Result<AssetPreviewContentDto>> GenerateAsync(
            AssetPreviewBuildRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _generateCallCount);
            LastRequest = request;

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

    private sealed class TestReplacementPreviewSourceProvider : IReplacementPreviewSourceProvider
    {
        private readonly Result<AssetPreviewContentDto> _result;
        private readonly Result<ReplacementTextChunkDto>? _textChunkResult;

        public TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto> result,
            Result<ReplacementTextChunkDto>? textChunkResult = null)
        {
            _result = result;
            _textChunkResult = textChunkResult;
        }

        public int LoadFilePreviewCallCount { get; private set; }

        public int LoadFileTextChunkCallCount { get; private set; }

        public AssetDetailsDto? LastTargetDetails { get; private set; }

        public string? LastSourceFilePath { get; private set; }

        public string? LastTextChunkSourceFilePath { get; private set; }

        public int LastTextChunkOffset { get; private set; }

        public int LastTextChunkLength { get; private set; }

        public Task<Result<AssetPreviewContentDto>> LoadFilePreviewAsync(
            AssetDetailsDto targetDetails,
            string sourceFilePath,
            CancellationToken cancellationToken = default)
        {
            LoadFilePreviewCallCount++;
            LastTargetDetails = targetDetails;
            LastSourceFilePath = sourceFilePath;
            return Task.FromResult(_result);
        }

        public Task<Result<ReplacementTextChunkDto>> LoadFileTextChunkAsync(
            string sourceFilePath,
            int offset,
            int length,
            CancellationToken cancellationToken = default)
        {
            LoadFileTextChunkCallCount++;
            LastTextChunkSourceFilePath = sourceFilePath;
            LastTextChunkOffset = offset;
            LastTextChunkLength = length;

            if (_textChunkResult is { } textChunkResult)
            {
                return Task.FromResult(textChunkResult);
            }

            if (!_result.IsSuccess ||
                _result.Value is not { Kind: AssetPreviewKind.Text } previewContent)
            {
                return Task.FromResult(Result<ReplacementTextChunkDto>.Failure(
                    ApplicationErrors.BundlePreviewGenerationFailed(sourceFilePath)));
            }

            var fullText = Encoding.UTF8.GetString(previewContent.Content)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            var safeOffset = Math.Min(offset, fullText.Length);
            var safeLength = Math.Min(length, fullText.Length - safeOffset);
            var text = safeLength <= 0
                ? string.Empty
                : fullText.Substring(safeOffset, safeLength);

            return Task.FromResult(Result<ReplacementTextChunkDto>.Success(new ReplacementTextChunkDto(
                sourceFilePath,
                previewContent.ContentType,
                text,
                safeOffset,
                safeLength,
                fullText.Length,
                previewContent.Content.LongLength)));
        }
    }
}
