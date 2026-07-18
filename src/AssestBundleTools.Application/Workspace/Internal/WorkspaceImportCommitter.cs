using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportCommitter
{
    private readonly IBundleArtifactSessionInvalidator _bundleArtifactSessionInvalidator;

    public WorkspaceImportCommitter(IBundleArtifactSessionInvalidator bundleArtifactSessionInvalidator)
    {
        _bundleArtifactSessionInvalidator = bundleArtifactSessionInvalidator
                                             ?? throw new ArgumentNullException(nameof(bundleArtifactSessionInvalidator));
    }

    public WorkspaceImportProcessingResultDto Commit(
        WorkspaceImportProcessingPlan plan,
        IReadOnlyList<WorkspaceImportBundleWorkResult> workerResults)
    {
        var existingCacheEntries = plan.ExistingCacheEntriesByBundleCacheKey
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var previewCacheKeysByBundle = plan.PreviewCacheKeysByBundleCacheKey
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);
        var observedBundleCacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var discoveredBundleCacheKeys = plan.DiscoveredFiles
            .Select(static file => WorkspaceImportPathUtility.CreateBundleCacheKey(file.SourcePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stableInvalidSourcePaths = new HashSet<string>(
            plan.CachedStableInvalidSourcePaths.Select(WorkspaceImportPathUtility.NormalizePath),
            StringComparer.OrdinalIgnoreCase);
        var cacheDiagnostics = new List<WorkspaceImportCacheDecisionDiagnosticDto>(workerResults.Count);
        var issueDiagnostics = new List<WorkspaceImportIssueDiagnosticDto>(plan.DecryptorResolution.ScreenedOutDiagnostics);
        var indexedBundleCount = 0;
        var reusedBundleCount = 0;
        var skippedFileCount = 0;
        var failedFileCount = 0;

        foreach (var recognitionEntry in plan.RecognitionCacheEntriesToSave)
        {
            SaveRecognitionEntry(plan, recognitionEntry, stableInvalidSourcePaths);
        }

        foreach (var workerResult in workerResults)
        {
            observedBundleCacheKeys.Add(workerResult.BundleCacheKey);
            cacheDiagnostics.Add(workerResult.CacheDiagnostic);
            if (workerResult.RecognitionCacheEntryToSave is { } recognitionEntry)
            {
                SaveRecognitionEntry(plan, recognitionEntry, stableInvalidSourcePaths);
            }

            if (workerResult.ShouldDeleteExistingArtifacts &&
                existingCacheEntries.TryGetValue(workerResult.BundleCacheKey, out var existingCacheEntry))
            {
                DeleteBundleArtifacts(
                    plan.Command.WorkspaceKey,
                    existingCacheEntry.BundleCacheKey,
                    plan.BundleArtifactStore,
                    false,
                    plan.BundleIndexCacheStore,
                    plan.PreviewCacheStore,
                    previewCacheKeysByBundle,
                    _bundleArtifactSessionInvalidator);
                existingCacheEntries.Remove(existingCacheEntry.BundleCacheKey);
            }

            switch (workerResult.Outcome)
            {
                case WorkspaceImportBundleWorkOutcome.Reused:
                    reusedBundleCount++;
                    break;
                case WorkspaceImportBundleWorkOutcome.Indexed:
                    if (workerResult.CacheEntryToSave is null)
                    {
                        throw new InvalidOperationException("Indexed worker result must carry a cache entry.");
                    }

                    plan.BundleIndexCacheStore.Save(workerResult.CacheEntryToSave);
                    existingCacheEntries[workerResult.BundleCacheKey] = workerResult.CacheEntryToSave;
                    indexedBundleCount++;
                    break;
                case WorkspaceImportBundleWorkOutcome.Skipped:
                    skippedFileCount++;
                    if (workerResult.IssueDiagnostic is not null)
                    {
                        issueDiagnostics.Add(workerResult.IssueDiagnostic);
                    }

                    break;
                case WorkspaceImportBundleWorkOutcome.Failed:
                    failedFileCount++;
                    if (workerResult.IssueDiagnostic is not null)
                    {
                        issueDiagnostics.Add(workerResult.IssueDiagnostic);
                    }

                    break;
            }
        }

        foreach (var staleRecognitionEntry in plan.ExistingRecognitionEntriesByBundleCacheKey.Values
                     .Where(entry => !discoveredBundleCacheKeys.Contains(entry.BundleCacheKey)))
        {
            plan.RecognitionCacheStore.Delete(
                plan.Command.WorkspaceKey,
                staleRecognitionEntry.BundleCacheKey);
        }

        foreach (var staleCacheEntry in existingCacheEntries.Values
                     .Where(entry => !observedBundleCacheKeys.Contains(entry.BundleCacheKey))
                     .ToArray())
        {
            cacheDiagnostics.Add(new WorkspaceImportCacheDecisionDiagnosticDto(
                SourcePath: staleCacheEntry.SourceFingerprint.SourcePath,
                LogicalPath: staleCacheEntry.LogicalPath,
                BundleCacheKey: staleCacheEntry.BundleCacheKey,
                Kind: WorkspaceImportCacheDecisionKind.SourceRemoved,
                DetailMessage: new LocalizedMessageDto(WorkspaceImportMessageTokens.CacheSourceRemoved)));
            DeleteBundleArtifacts(
                plan.Command.WorkspaceKey,
                staleCacheEntry.BundleCacheKey,
                plan.BundleArtifactStore,
                true,
                plan.BundleIndexCacheStore,
                plan.PreviewCacheStore,
                previewCacheKeysByBundle,
                _bundleArtifactSessionInvalidator);
        }

        var resolvedDecryptorId = plan.DecryptorResolution.UsedAutoDetection
            ? AssetBundleDecryptorIds.BuiltInAutoDetect
            : plan.DecryptorResolution.Decryptor.Descriptor.Id;

        var remainingImportedSources = RemoveStableInvalidDirectSources(plan, stableInvalidSourcePaths);

        return new WorkspaceImportProcessingResultDto(
            WorkspaceKey: plan.Metadata.WorkspaceKey,
            WorkspaceDisplayName: plan.Metadata.DisplayName,
            ResolvedDecryptorId: resolvedDecryptorId,
            UsedAutoDetection: plan.DecryptorResolution.UsedAutoDetection,
            AutoDetectionDiagnostic: plan.DecryptorResolution.AutoDetectionDiagnostic,
            RegisteredSourceCount: remainingImportedSources.Count,
            DiscoveredFileCount: plan.DiscoveredFiles.Count,
            ScreenedOutFileCount: plan.DecryptorResolution.ScreenedOutDiagnostics.Count,
            IndexedBundleCount: indexedBundleCount,
            ReusedBundleCount: reusedBundleCount,
            SkippedFileCount: skippedFileCount,
            FailedFileCount: failedFileCount,
            CacheDiagnostics: cacheDiagnostics,
            IssueDiagnostics: issueDiagnostics,
            ProcessedAtUtc: plan.ProcessedAtUtc);
    }

    private static void SaveRecognitionEntry(
        WorkspaceImportProcessingPlan plan,
        WorkspaceImportRecognitionCacheEntryDto recognitionEntry,
        ISet<string> stableInvalidSourcePaths)
    {
        plan.RecognitionCacheStore.Save(recognitionEntry);
        if (recognitionEntry.Disposition is WorkspaceImportRecognitionDisposition.ScreenedOut or
            WorkspaceImportRecognitionDisposition.ReaderRejected)
        {
            stableInvalidSourcePaths.Add(WorkspaceImportPathUtility.NormalizePath(recognitionEntry.SourcePath));
        }
    }

    private static IReadOnlyList<WorkspaceImportedSourceDto> RemoveStableInvalidDirectSources(
        WorkspaceImportProcessingPlan plan,
        IReadOnlySet<string> stableInvalidSourcePaths)
    {
        if (stableInvalidSourcePaths.Count == 0)
        {
            return plan.ImportedSources;
        }

        var remainingSources = plan.ImportedSources
            .Where(source => source.PathKind != WorkspaceImportPathKind.File ||
                             !stableInvalidSourcePaths.Contains(WorkspaceImportPathUtility.NormalizePath(source.Path)))
            .ToArray();
        if (remainingSources.Length != plan.ImportedSources.Count)
        {
            plan.MetadataStore.Save(plan.Metadata with { ImportedSources = remainingSources });
        }

        return remainingSources;
    }

    private static void DeleteBundleArtifacts(
        string workspaceKey,
        string bundleCacheKey,
        IBundleArtifactStore bundleArtifactStore,
        bool deleteBundleArtifact,
        IBundleIndexCacheStore bundleIndexCacheStore,
        IPreviewCacheStore previewCacheStore,
        IDictionary<string, List<string>> previewCacheKeysByBundle,
        IBundleArtifactSessionInvalidator bundleArtifactSessionInvalidator)
    {
        WorkspaceBundleArtifactCleanupHelper.DeleteBundleArtifacts(
            workspaceKey,
            bundleCacheKey,
            deleteBundleArtifact ? bundleArtifactStore : null,
            bundleIndexCacheStore,
            previewCacheStore,
            previewCacheKeysByBundle.TryGetValue(bundleCacheKey, out var previewCacheKeys)
                ? previewCacheKeys
                : Array.Empty<string>(),
            bundleArtifactSessionInvalidator);

        previewCacheKeysByBundle.Remove(bundleCacheKey);
    }
}
