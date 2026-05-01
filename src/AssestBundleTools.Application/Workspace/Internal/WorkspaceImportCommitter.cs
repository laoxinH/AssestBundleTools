using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportCommitter
{
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
        var cacheDiagnostics = new List<WorkspaceImportCacheDecisionDiagnosticDto>(workerResults.Count);
        var issueDiagnostics = new List<WorkspaceImportIssueDiagnosticDto>(plan.DecryptorResolution.ScreenedOutDiagnostics);
        var indexedBundleCount = 0;
        var reusedBundleCount = 0;
        var skippedFileCount = 0;
        var failedFileCount = 0;

        foreach (var workerResult in workerResults)
        {
            observedBundleCacheKeys.Add(workerResult.BundleCacheKey);
            cacheDiagnostics.Add(workerResult.CacheDiagnostic);

            if (workerResult.ShouldDeleteExistingArtifacts &&
                existingCacheEntries.TryGetValue(workerResult.BundleCacheKey, out var existingCacheEntry))
            {
                DeleteBundleArtifacts(
                    plan.Command.WorkspaceKey,
                    existingCacheEntry.BundleCacheKey,
                    plan.BundleIndexCacheStore,
                    plan.PreviewCacheStore,
                    previewCacheKeysByBundle);
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
                plan.BundleIndexCacheStore,
                plan.PreviewCacheStore,
                previewCacheKeysByBundle);
        }

        var decryptor = plan.DecryptorResolution.Decryptor;
        if (plan.DecryptorResolution.ShouldPersistResolvedDecryptorId &&
            indexedBundleCount + reusedBundleCount > 0 &&
            !string.Equals(plan.Metadata.AssetBundleDecryptorId, decryptor.Descriptor.Id, StringComparison.OrdinalIgnoreCase))
        {
            plan.MetadataStore.Save(plan.Metadata with
            {
                AssetBundleDecryptorId = decryptor.Descriptor.Id
            });
        }

        return new WorkspaceImportProcessingResultDto(
            WorkspaceKey: plan.Metadata.WorkspaceKey,
            WorkspaceDisplayName: plan.Metadata.DisplayName,
            ResolvedDecryptorId: decryptor.Descriptor.Id,
            UsedAutoDetection: plan.DecryptorResolution.UsedAutoDetection,
            AutoDetectionDiagnostic: plan.DecryptorResolution.AutoDetectionDiagnostic,
            RegisteredSourceCount: plan.ImportedSources.Count,
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

    private static void DeleteBundleArtifacts(
        string workspaceKey,
        string bundleCacheKey,
        IBundleIndexCacheStore bundleIndexCacheStore,
        IPreviewCacheStore previewCacheStore,
        IDictionary<string, List<string>> previewCacheKeysByBundle)
    {
        WorkspaceBundleArtifactCleanupHelper.DeleteBundleArtifacts(
            workspaceKey,
            bundleCacheKey,
            bundleIndexCacheStore,
            previewCacheStore,
            previewCacheKeysByBundle.TryGetValue(bundleCacheKey, out var previewCacheKeys)
                ? previewCacheKeys
                : Array.Empty<string>());

        previewCacheKeysByBundle.Remove(bundleCacheKey);
    }
}
