using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static partial class ProcessWorkspaceImportsHandlerTests
{
    private sealed class TestWorkspaceMetadataStore : IWorkspaceMetadataStore
    {
        public TestWorkspaceMetadataStore(WorkspaceMetadataDto? metadata)
        {
            Metadata = metadata;
        }

        public WorkspaceMetadataDto? Metadata { get; private set; }

        public int SaveCallCount { get; private set; }

        public WorkspaceMetadataDto? Load(string workspaceKey) => Metadata;

        public void Save(WorkspaceMetadataDto metadata)
        {
            Metadata = metadata;
            SaveCallCount++;
        }

        public void Delete(string workspaceKey)
        {
            Metadata = null;
        }
    }

    private sealed class TestWorkspaceMetadataStoreFactory : IWorkspaceMetadataStoreFactory
    {
        private readonly IWorkspaceMetadataStore _store;

        public TestWorkspaceMetadataStoreFactory(IWorkspaceMetadataStore store)
        {
            _store = store;
        }

        public IWorkspaceMetadataStore Create(string? rootDirectoryPath = null) => _store;

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
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

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => new TestWorkspaceChangeStore();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly List<BundleIndexCacheEntryDto> _cacheEntries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> cacheEntries)
        {
            _cacheEntries = cacheEntries.ToList();
        }

        public List<(string WorkspaceKey, string BundleCacheKey)> DeleteOperations { get; } = [];

        public IReadOnlyList<BundleIndexCacheEntryDto> SavedEntries => _cacheEntries;

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) =>
            _cacheEntries.FirstOrDefault(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) =>
            _cacheEntries
                .Where(entry => string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        public void Save(BundleIndexCacheEntryDto cacheEntry)
        {
            _cacheEntries.RemoveAll(entry =>
                string.Equals(entry.WorkspaceKey, cacheEntry.WorkspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.BundleCacheKey, cacheEntry.BundleCacheKey, StringComparison.OrdinalIgnoreCase));
            _cacheEntries.Add(cacheEntry);
        }

        public void Delete(string workspaceKey, string bundleCacheKey)
        {
            DeleteOperations.Add((workspaceKey, bundleCacheKey));
            _cacheEntries.RemoveAll(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));
        }

        public void DeleteWorkspace(string workspaceKey)
        {
            _cacheEntries.RemoveAll(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class TestPreviewCacheStore : IPreviewCacheStore
    {
        private readonly List<PreviewCacheDescriptorDto> _descriptors;

        public TestPreviewCacheStore(IReadOnlyList<PreviewCacheDescriptorDto> descriptors)
        {
            _descriptors = descriptors.ToList();
        }

        public List<(string WorkspaceKey, string PreviewCacheKey)> DeleteOperations { get; } = [];

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey) => null;

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey) =>
            _descriptors
                .Where(descriptor => string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor) => null;

        public void Save(PreviewCachePayloadDto previewCachePayload)
        {
        }

        public void Delete(string workspaceKey, string previewCacheKey)
        {
            DeleteOperations.Add((workspaceKey, previewCacheKey));
            _descriptors.RemoveAll(descriptor =>
                string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase));
        }

        public void DeleteWorkspace(string workspaceKey)
        {
            _descriptors.RemoveAll(descriptor =>
                string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? Load(string workspaceKey) => null;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
        }

        public void Delete(string workspaceKey)
        {
        }
    }

    private sealed class TestWorkspaceImportSourceDiscoveryService : IWorkspaceImportSourceDiscoveryService
    {
        private readonly IReadOnlyList<DiscoveredWorkspaceImportFileDto> _discoveredFiles;

        public TestWorkspaceImportSourceDiscoveryService(IReadOnlyList<DiscoveredWorkspaceImportFileDto> discoveredFiles)
        {
            _discoveredFiles = discoveredFiles;
        }

        public IReadOnlyList<DiscoveredWorkspaceImportFileDto> Discover(
            IReadOnlyList<WorkspaceImportedSourceDto> importedSources,
            IReadOnlyList<string>? excludedSourcePaths = null) => _discoveredFiles;
    }

    private sealed class TestWorkspaceImportFileSystemService : IWorkspaceImportFileSystemService
    {
        private readonly IReadOnlyDictionary<string, WorkspaceFileFingerprintDto> _fingerprintsBySourcePath;

        public TestWorkspaceImportFileSystemService(
            IReadOnlyDictionary<string, WorkspaceFileFingerprintDto>? fingerprintsBySourcePath = null)
        {
            _fingerprintsBySourcePath = fingerprintsBySourcePath ??
                                        new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase);
        }

        public int CreateFingerprintCallCount { get; private set; }

        public WorkspaceImportPathKind ResolvePathKind(string path) => WorkspaceImportPathKind.File;

        public WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath)
        {
            CreateFingerprintCallCount++;

            if (_fingerprintsBySourcePath.TryGetValue(sourcePath, out var fingerprint))
            {
                return fingerprint;
            }

            throw new InvalidOperationException($"Missing test fingerprint for '{sourcePath}'.");
        }
    }

    private sealed class TestAssetBundleSourceInspector : IAssetBundleSourceInspector
    {
        public Func<string, AssetBundleSourceInspectionResultDto>? InspectResultFactory { get; init; }

        public AssetBundleSourceInspectionResultDto Inspect(string sourcePath) =>
            InspectResultFactory?.Invoke(sourcePath) ??
            new AssetBundleSourceInspectionResultDto(AssetBundleSourceInspectionKind.Bundle);
    }

    private sealed class TestAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
    {
        private readonly IReadOnlyList<IAssetBundleDecryptor> _decryptors;

        public TestAssetBundleDecryptorRegistry(params IAssetBundleDecryptor[] decryptors)
        {
            _decryptors = decryptors.Length == 0
                ? throw new ArgumentException("At least one decryptor is required.", nameof(decryptors))
                : decryptors;
        }

        public IReadOnlyList<IAssetBundleDecryptor> GetAll() => _decryptors;

        public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
        {
            foreach (var candidate in _decryptors)
            {
                if (string.Equals(candidate.Descriptor.Id, decryptorId, StringComparison.OrdinalIgnoreCase))
                {
                    decryptor = candidate;
                    return true;
                }
            }

            decryptor = _decryptors[0];
            return false;
        }
    }

    private sealed class TestAssetBundleDecryptor : IAssetBundleDecryptor
    {
        public TestAssetBundleDecryptor(AssetBundleDecryptorDescriptorDto descriptor)
        {
            Descriptor = descriptor;
        }

        public AssetBundleDecryptorDescriptorDto Descriptor { get; }

        public Func<AssetBundleDecryptorProbeRequestDto, Task<AssetBundleDecryptorProbeResultDto>>? ProbeResultFactory { get; init; }

        public Func<AssetBundleDecryptionRequestDto, Task<Result<AssetBundleDecryptionResultDto>>>? DecryptResultFactory { get; init; }

        public int ProbeCallCount { get; private set; }

        public int DecryptCallCount { get; private set; }

        public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
            AssetBundleDecryptorProbeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProbeCallCount++;
            if (ProbeResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return ProbeResultFactory.Invoke(request);
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

    private sealed class TestAssetBundleReader : IAssetBundleReader
    {
        public string CacheParserVersion => "parser-1";

        public bool CanReadResult { get; init; } = true;

        public bool SupportsCacheParserVersionResult { get; init; } = true;

        public Func<AssetBundleReadRequestDto, Task<Result<AssetBundleReadResultDto>>>? ReadResultFactory { get; init; }

        public int SupportsCacheParserVersionCallCount { get; private set; }

        public int CanReadCallCount { get; private set; }

        public int ReadCallCount { get; private set; }

        public bool CanRead(string bundlePath)
        {
            CanReadCallCount++;
            return CanReadResult;
        }

        public bool SupportsCacheParserVersion(string parserVersion)
        {
            SupportsCacheParserVersionCallCount++;
            return SupportsCacheParserVersionResult;
        }

        public Task<Result<AssetBundleReadResultDto>> ReadAsync(
            AssetBundleReadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            if (ReadResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return ReadResultFactory.Invoke(request);
        }
    }

    private sealed class FixedWorkspaceImportExecutionOptionsResolver : IWorkspaceImportExecutionOptionsResolver
    {
        private readonly WorkspaceImportExecutionOptions _options;

        public FixedWorkspaceImportExecutionOptionsResolver(int bundleWorkerCount)
        {
            _options = new WorkspaceImportExecutionOptions(bundleWorkerCount);
        }

        public WorkspaceImportExecutionOptions ResolveOptions() => _options;
    }

    private sealed class ProgressRecorder : IProgress<WorkspaceImportProgressDto>
    {
        private readonly List<WorkspaceImportProgressDto> _snapshots;

        public ProgressRecorder(List<WorkspaceImportProgressDto> snapshots)
        {
            _snapshots = snapshots;
        }

        public void Report(WorkspaceImportProgressDto value)
        {
            _snapshots.Add(value);
        }
    }

    private static void RecordMaximumConcurrency(ref int target, int candidate)
    {
        while (true)
        {
            var current = target;
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }

    private static string CreateBundleCacheKey(string sourcePath)
    {
        var normalizedPath = Path.GetFullPath(sourcePath).Trim();
        var bytes = Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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

    private static AssetBundleDecryptorDescriptorDto CreateAutoDetectDecryptorDescriptor(
        string id,
        string displayName)
    {
        return new AssetBundleDecryptorDescriptorDto(
            id,
            displayName,
            "1.0.0",
            AssetBundleDecryptorKind.BuiltIn,
            SupportsAutoDetection: true,
            IsFallback: false,
            Description: "auto-detect");
    }

    private static AssetBundleDecryptionResultDto CreateDecryptionResult(
        AssetBundleDecryptorDescriptorDto decryptorDescriptor,
        WorkspaceFileFingerprintDto bundleFingerprint,
        string resolvedBundlePath)
    {
        return new AssetBundleDecryptionResultDto(
            Decryptor: decryptorDescriptor,
            ResolvedBundlePath: resolvedBundlePath,
            ProducedDerivedFile: false,
            BundleFingerprint: bundleFingerprint,
            DiagnosticMessage: null);
    }

    private static string CreateCurrentToolVersion()
    {
        var assemblyVersion = typeof(ProcessWorkspaceImportsHandler).Assembly.GetName().Version?.ToString()
                              ?? "1.0.0";
        return $"{assemblyVersion}+workspace-cache/preview-1";
    }
}
