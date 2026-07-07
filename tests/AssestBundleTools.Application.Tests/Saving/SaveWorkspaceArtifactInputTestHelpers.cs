using System.IO;
using System.Linq;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IBundleArtifactStore _bundleArtifactStore;
        private readonly IWorkspaceChangeStore? _workspaceChangeStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IBundleArtifactStore bundleArtifactStore,
            IWorkspaceChangeStore? workspaceChangeStore = null)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _bundleArtifactStore = bundleArtifactStore;
            _workspaceChangeStore = workspaceChangeStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) => _bundleArtifactStore;

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) => _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) => throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) =>
            _workspaceChangeStore ?? throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly List<BundleIndexCacheEntryDto> _entries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> entries)
        {
            _entries = entries.ToList();
        }

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) =>
            _entries.FirstOrDefault(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) =>
            _entries
                .Where(entry => string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
                .ToArray();

        public void Save(BundleIndexCacheEntryDto cacheEntry) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestBundleArtifactStore : IBundleArtifactStore
    {
        private readonly List<BundleArtifactManifestDto> _manifests;

        public TestBundleArtifactStore(IReadOnlyList<BundleArtifactManifestDto> manifests)
        {
            _manifests = manifests.ToList();
        }

        public int UpdateLastAccessedCallCount { get; private set; }

        public BundleArtifactManifestDto? Load(string workspaceKey, string bundleCacheKey) =>
            _manifests.FirstOrDefault(manifest =>
                string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(manifest.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));

        public string? ResolveBundlePath(BundleArtifactManifestDto manifest) =>
            manifest.IsMaterialized && !string.IsNullOrWhiteSpace(manifest.ResolvedBundlePath)
                ? manifest.ResolvedBundlePath
                : null;

        public BundleArtifactManifestDto Save(BundleArtifactSaveRequestDto request) => throw new NotSupportedException();

        public BundleArtifactManifestDto UpdateLastAccessed(
            BundleArtifactManifestDto manifest,
            DateTimeOffset lastAccessedAtUtc)
        {
            UpdateLastAccessedCallCount++;
            var updatedManifest = manifest with { LastAccessedAtUtc = lastAccessedAtUtc };
            var index = _manifests.FindIndex(existing =>
                string.Equals(existing.WorkspaceKey, manifest.WorkspaceKey, StringComparison.Ordinal) &&
                string.Equals(existing.BundleCacheKey, manifest.BundleCacheKey, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _manifests[index] = updatedManifest;
            }

            return updatedManifest;
        }

        public BundleArtifactCacheUsageDto GetUsage(string? workspaceKey = null) => throw new NotSupportedException();

        public BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class ReadyWorkspaceSaveOutputProbe : IWorkspaceSaveOutputProbe
    {
        public WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request) =>
            WorkspaceSaveOutputProbeResult.Ready();
    }

    private sealed class BlockingWorkspaceSaveOutputProbe : IWorkspaceSaveOutputProbe
    {
        private readonly string _blockedFileName;

        public BlockingWorkspaceSaveOutputProbe(string blockedFileName)
        {
            _blockedFileName = blockedFileName;
        }

        public WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request) =>
            string.Equals(Path.GetFileName(request.OutputFilePath), _blockedFileName, StringComparison.OrdinalIgnoreCase)
                ? WorkspaceSaveOutputProbeResult.Blocked(
                    WorkspaceSaveOutputReadiness.FileAlreadyExists,
                    "Save.Output.FileAlreadyExists")
                : WorkspaceSaveOutputProbeResult.Ready();
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        private WorkspaceChangeSetDto? _changeSet;

        public TestWorkspaceChangeStore(WorkspaceChangeSetDto? changeSet)
        {
            _changeSet = changeSet;
        }

        public WorkspaceChangeSetDto? Load(string workspaceKey) => _changeSet;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            _changeSet = changeSet;
        }

        public void Delete(string workspaceKey)
        {
            _changeSet = null;
        }
    }

    private sealed class RecordingBundleExporter : IWorkspaceBundleExporter
    {
        private readonly BundleId? _failedBundleId;

        public RecordingBundleExporter(BundleId? failedBundleId = null)
        {
            _failedBundleId = failedBundleId;
        }

        public List<WorkspaceBundleExportRequestDto> Requests { get; } = [];

        public Task<Result<WorkspaceBundleExportResultDto>> ExportAsync(
            WorkspaceBundleExportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_failedBundleId == request.BundleId)
            {
                return Task.FromResult(Result<WorkspaceBundleExportResultDto>.Failure(
                    ApplicationErrors.BundleExportWriteFailed(request.OutputFilePath, "TestFailure")));
            }

            return Task.FromResult(Result<WorkspaceBundleExportResultDto>.Success(
                CreateSuccessfulExportResult(request)));
        }
    }

    private static WorkspaceBundleExportResultDto CreateSuccessfulExportResult(WorkspaceBundleExportRequestDto request) =>
        new(
            BundleId: request.BundleId,
            BundleDisplayName: request.BundleDisplayName,
            OutputFilePath: request.OutputFilePath,
            SourceArtifactPath: request.SourceArtifactPath,
            BundleCacheKey: request.BundleCacheKey,
            WrittenFileSizeInBytes: 4096,
            WrittenFileHashAlgorithm: "SHA256",
            WrittenFileHash: "abc123-sha256",
            WrittenFileLastWriteTimeUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
            WrittenFileMd5: "abc123-md5",
            WrittenFileSha256: "abc123-sha256",
            ProcessorResults: request.ProcessorResults
                .GroupBy(static result => new { result.ResourceKind, result.ProcessorId })
                .Select(static group => new WorkspaceBundleProcessorExecutionSummaryDto(
                    group.Key.ResourceKind,
                    group.Key.ProcessorId,
                    WorkspaceResourceProcessorStatusDto.Succeeded,
                    InputCount: group.Count(),
                    ProcessedCount: group.Count(),
                    FailedCount: 0,
                    ResultPaths: group
                        .Select(static result => result.ResultPath)
                        .Where(static path => !string.IsNullOrWhiteSpace(path))
                        .Select(static path => path!)
                        .ToArray(),
                    Diagnostics: Array.Empty<WorkspaceResourceProcessorDiagnosticDto>()))
                .ToArray(),
            CompressionMode: request.CompressionMode,
            OriginalCompression: "LZMA",
            WrittenCompression: "LZ4Fast");

    private static ResourceProcessorCatalogEntryDto CreateResourceProcessor(
        string processorId,
        string resourceKind)
    {
        return new ResourceProcessorCatalogEntryDto(
            processorId,
            processorId,
            Version: "1.0.0",
            Description: string.Empty,
            ResourceProcessorKind.ExternalProcess,
            [resourceKind],
            ResourceProcessorStatus.Available,
            IsBuiltIn: false,
            IsReadOnly: false,
            ManifestFilePath: @"D:\Processors\processor.json",
            ExecutablePath: @"D:\Processors\processor.exe",
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments: ["--input", "{inputPath}", "--output", "{outputPath}", "--result", "{resultPath}"]);
    }

    private sealed class TestResourceProcessorCatalogStore : IResourceProcessorCatalogStore
    {
        private readonly IReadOnlyList<ResourceProcessorCatalogEntryDto> _entries;

        public TestResourceProcessorCatalogStore(IReadOnlyList<ResourceProcessorCatalogEntryDto> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<ResourceProcessorCatalogEntryDto> Load() => _entries;

        public void SaveUserManagedProcessor(ResourceProcessorDefinitionDto definition, string? previousProcessorId = null) =>
            throw new NotSupportedException();

        public void DeleteUserManagedProcessor(string processorId) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingResourceProcessorExecutor : IWorkspaceResourceProcessorExecutor
    {
        private readonly bool _failExecution;

        public RecordingResourceProcessorExecutor(bool failExecution = false)
        {
            _failExecution = failExecution;
        }

        public List<WorkspaceResourceProcessorWorkItemDto> WorkItems { get; } = [];

        public Task<Result<WorkspaceResourceProcessorExecutionResultDto>> ExecuteAsync(
            WorkspaceResourceProcessorWorkItemDto workItem,
            CancellationToken cancellationToken = default)
        {
            WorkItems.Add(workItem);
            if (_failExecution)
            {
                return Task.FromResult(Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                    ApplicationErrors.ResourceProcessorExecutionFailed(workItem.Processor.Id, "TestFailure")));
            }

            return Task.FromResult(Result<WorkspaceResourceProcessorExecutionResultDto>.Success(
                new WorkspaceResourceProcessorExecutionResultDto(
                    BundleId: workItem.BundleId,
                    AssetId: workItem.AssetId,
                    AssetName: workItem.AssetName,
                    ResourceKind: workItem.ResourceKind,
                    ProcessorId: workItem.Processor.Id,
                    Status: WorkspaceResourceProcessorStatusDto.Succeeded,
                    InputPath: workItem.InputPayloadPath,
                    OutputPath: Path.Combine(workItem.StagingDirectoryPath, "output"),
                    ResultPath: Path.Combine(workItem.StagingDirectoryPath, "result.json"),
                    Diagnostics: Array.Empty<WorkspaceResourceProcessorDiagnosticDto>(),
                    ContentType: "image/png")));
        }
    }

    private sealed class RecordingBundleExportManifestStore : IWorkspaceBundleExportManifestStore
    {
        public int SaveCallCount { get; private set; }

        public WorkspaceBundleExportManifestSaveRequestDto? LastRequest { get; private set; }

        public Task<Result<WorkspaceBundleExportManifestSaveResultDto>> SaveAsync(
            WorkspaceBundleExportManifestSaveRequestDto request,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            LastRequest = request;
            var manifestPath = Path.Combine(request.OutputDirectory, "_abt-bundle-export-manifest.json");
            return Task.FromResult(Result<WorkspaceBundleExportManifestSaveResultDto>.Success(
                new WorkspaceBundleExportManifestSaveResultDto(
                    manifestPath,
                    new WorkspaceBundleExportManifestDto(
                        SchemaVersion: 1,
                        WorkspaceId: request.WorkspaceId.Value,
                        WorkspaceKey: request.WorkspaceKey,
                        WorkspaceDisplayName: request.WorkspaceDisplayName,
                        OutputDirectory: request.OutputDirectory,
                        ExportedAtUtc: request.ExportedAtUtc,
                        Bundles: Array.Empty<WorkspaceBundleExportManifestItemDto>(),
                        ProcessorSelections: Array.Empty<WorkspaceBundleExportManifestProcessorSelectionDto>()))));
        }
    }
}
