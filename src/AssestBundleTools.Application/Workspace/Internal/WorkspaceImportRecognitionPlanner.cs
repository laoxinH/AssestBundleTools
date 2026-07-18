using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportRecognitionPlanner
{
    private readonly IWorkspaceImportFileSystemService _fileSystemService;
    private readonly IAssetBundleDecryptorRegistry _decryptorRegistry;
    private readonly IAssetBundleReader _reader;

    public WorkspaceImportRecognitionPlanner(
        IWorkspaceImportFileSystemService fileSystemService,
        IAssetBundleDecryptorRegistry decryptorRegistry,
        IAssetBundleReader reader)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _decryptorRegistry = decryptorRegistry ?? throw new ArgumentNullException(nameof(decryptorRegistry));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public WorkspaceImportRecognitionPlan Prepare(
        string configuredDecryptorId,
        IReadOnlyList<DiscoveredWorkspaceImportFileDto> discoveredFiles,
        IReadOnlyDictionary<string, BundleIndexCacheEntryDto> existingIndexEntries,
        IReadOnlyDictionary<string, WorkspaceImportRecognitionCacheEntryDto> existingRecognitionEntries,
        string recognitionVersion,
        string currentToolVersion)
    {
        var catalogFingerprint = CreateCodecCatalogFingerprint(configuredDecryptorId);
        var fingerprints = new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase);
        var cachedCandidates = new List<WorkspaceImportBundleCandidate>();
        var cachedScreenedOutDiagnostics = new List<WorkspaceImportIssueDiagnosticDto>();
        var cachedWorkerResults = new List<WorkspaceImportBundleWorkResult>();
        var cachedStableInvalidSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesRequiringScreening = new List<DiscoveredWorkspaceImportFileDto>();

        foreach (var file in discoveredFiles)
        {
            var fingerprint = _fileSystemService.CreateFingerprint(file.SourcePath);
            fingerprints[file.SourcePath] = fingerprint;
            var bundleCacheKey = WorkspaceImportPathUtility.CreateBundleCacheKey(file.SourcePath);

            if (existingRecognitionEntries.TryGetValue(bundleCacheKey, out var recognitionEntry) &&
                TryReuseRecognitionEntry(
                    file,
                    fingerprint,
                    recognitionEntry,
                    recognitionVersion,
                    catalogFingerprint,
                    cachedCandidates,
                    cachedScreenedOutDiagnostics,
                    cachedWorkerResults,
                    cachedStableInvalidSourcePaths))
            {
                continue;
            }

            if (!IsAutoDetectionMode(configuredDecryptorId) &&
                existingIndexEntries.TryGetValue(bundleCacheKey, out var indexEntry) &&
                string.Equals(indexEntry.DecryptorId, configuredDecryptorId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                TryResolveCompatibleDecryptor(indexEntry.DecryptorId, indexEntry.DecryptorVersion, out var decryptor) &&
                WorkspaceFileFingerprintComparer.Matches(indexEntry.SourceFingerprint, fingerprint) &&
                string.Equals(indexEntry.ToolVersion, currentToolVersion, StringComparison.OrdinalIgnoreCase))
            {
                cachedCandidates.Add(new WorkspaceImportBundleCandidate(file, decryptor));
                continue;
            }

            filesRequiringScreening.Add(file);
        }

        return new WorkspaceImportRecognitionPlan(
            catalogFingerprint,
            fingerprints,
            cachedCandidates,
            cachedScreenedOutDiagnostics,
            cachedWorkerResults,
            filesRequiringScreening,
            cachedCandidates.Count + cachedScreenedOutDiagnostics.Count + cachedWorkerResults.Count,
            cachedStableInvalidSourcePaths);
    }

    private bool TryReuseRecognitionEntry(
        DiscoveredWorkspaceImportFileDto file,
        WorkspaceFileFingerprintDto fingerprint,
        WorkspaceImportRecognitionCacheEntryDto entry,
        string recognitionVersion,
        string catalogFingerprint,
        ICollection<WorkspaceImportBundleCandidate> candidates,
        ICollection<WorkspaceImportIssueDiagnosticDto> screenedOutDiagnostics,
        ICollection<WorkspaceImportBundleWorkResult> workerResults,
        ISet<string> stableInvalidSourcePaths)
    {
        if (!WorkspaceFileFingerprintComparer.Matches(entry.SourceFingerprint, fingerprint) ||
            !string.Equals(entry.RecognitionVersion, recognitionVersion, StringComparison.Ordinal) ||
            !string.Equals(entry.CodecCatalogFingerprint, catalogFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        switch (entry.Disposition)
        {
            case WorkspaceImportRecognitionDisposition.Candidate:
                if (!TryResolveCompatibleDecryptor(entry.DecryptorId, entry.DecryptorVersion, out var decryptor))
                {
                    return false;
                }

                candidates.Add(new WorkspaceImportBundleCandidate(file, decryptor));
                return true;
            case WorkspaceImportRecognitionDisposition.ScreenedOut:
                screenedOutDiagnostics.Add(CreateIssue(entry, WorkspaceImportIssueKind.CandidateScreenedOut));
                stableInvalidSourcePaths.Add(file.SourcePath);
                return true;
            case WorkspaceImportRecognitionDisposition.ReaderRejected:
                if (!string.Equals(entry.ReaderParserVersion, _reader.CacheParserVersion, StringComparison.Ordinal))
                {
                    return false;
                }

                workerResults.Add(new WorkspaceImportBundleWorkResult(
                    BundleCacheKey: entry.BundleCacheKey,
                    SourcePath: file.SourcePath,
                    LogicalPath: file.LogicalPath,
                    ShouldDeleteExistingArtifacts: false,
                    Outcome: WorkspaceImportBundleWorkOutcome.Skipped,
                    CacheDiagnostic: new WorkspaceImportCacheDecisionDiagnosticDto(
                        file.SourcePath,
                        file.LogicalPath,
                        entry.BundleCacheKey,
                        WorkspaceImportCacheDecisionKind.Reused,
                        DetailMessage: null),
                    CacheEntryToSave: null,
                    IssueDiagnostic: CreateIssue(entry, WorkspaceImportIssueKind.ReaderRejected),
                    RecognitionCacheEntryToSave: null));
                stableInvalidSourcePaths.Add(file.SourcePath);
                return true;
            default:
                return false;
        }
    }

    private bool TryResolveCompatibleDecryptor(
        string? decryptorId,
        string? decryptorVersion,
        out IAssetBundleDecryptor decryptor)
    {
        if (!string.IsNullOrWhiteSpace(decryptorId) &&
            _decryptorRegistry.TryGet(decryptorId, out decryptor!) &&
            string.Equals(decryptor.Descriptor.Version, decryptorVersion, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        decryptor = null!;
        return false;
    }

    private static WorkspaceImportIssueDiagnosticDto CreateIssue(
        WorkspaceImportRecognitionCacheEntryDto entry,
        WorkspaceImportIssueKind fallbackKind) =>
        new(
            entry.SourcePath,
            entry.LogicalPath,
            entry.IssueKind ?? fallbackKind,
            entry.DecryptorId,
            entry.DetailMessage,
            Errors: []);

    private string CreateCodecCatalogFingerprint(string? configuredDecryptorId)
    {
        var normalizedId = string.IsNullOrWhiteSpace(configuredDecryptorId)
            ? AssetBundleDecryptorIds.BuiltInAutoDetect
            : configuredDecryptorId.Trim();
        var descriptors = IsAutoDetectionMode(normalizedId)
            ? _decryptorRegistry.GetAll()
                .Where(static decryptor => decryptor.Descriptor.IsFallback || decryptor.Descriptor.SupportsAutoDetection)
                .Select(static decryptor => decryptor.Descriptor)
            : _decryptorRegistry.GetAll()
                .Where(decryptor => string.Equals(
                    decryptor.Descriptor.Id,
                    normalizedId,
                    StringComparison.OrdinalIgnoreCase))
                .Select(static decryptor => decryptor.Descriptor);
        var catalogText = string.Join(
            "\n",
            descriptors
                .OrderBy(static descriptor => descriptor.Id, StringComparer.OrdinalIgnoreCase)
                .Select(static descriptor => string.Join(
                    "|",
                    descriptor.Id,
                    descriptor.Version,
                    descriptor.SupportsAutoDetection,
                    descriptor.IsFallback)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(catalogText))).ToLowerInvariant();
    }

    private static bool IsAutoDetectionMode(string? decryptorId) =>
        string.IsNullOrWhiteSpace(decryptorId) ||
        string.Equals(decryptorId.Trim(), AssetBundleDecryptorIds.BuiltInAutoDetect, StringComparison.OrdinalIgnoreCase);
}

internal sealed record WorkspaceImportRecognitionPlan(
    string CodecCatalogFingerprint,
    IReadOnlyDictionary<string, WorkspaceFileFingerprintDto> SourceFingerprintsByPath,
    IReadOnlyList<WorkspaceImportBundleCandidate> CachedCandidates,
    IReadOnlyList<WorkspaceImportIssueDiagnosticDto> CachedScreenedOutDiagnostics,
    IReadOnlyList<WorkspaceImportBundleWorkResult> CachedWorkerResults,
    IReadOnlyList<DiscoveredWorkspaceImportFileDto> FilesRequiringScreening,
    int CacheHitCount,
    IReadOnlySet<string> CachedStableInvalidSourcePaths);
