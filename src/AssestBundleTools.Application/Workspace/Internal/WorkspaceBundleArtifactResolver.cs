using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 将“解密 / 标准化 / artifact manifest 复用”收束为导入、预览和提取共享的一条链路。
/// </summary>
internal static class WorkspaceBundleArtifactResolver
{
    public const string NormalizerVersion = "bundle-artifact/1";

    private static readonly object ResolutionGatesSyncRoot = new();
    private static readonly Dictionary<string, ResolutionGateEntry> ResolutionGates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<Result<WorkspaceBundleArtifactResolutionDto>> ResolveAsync(
        IBundleArtifactStore bundleArtifactStore,
        IAssetBundleDecryptor decryptor,
        string workspaceKey,
        string workspaceDataRootPath,
        string bundleCacheKey,
        string sourcePath,
        WorkspaceFileFingerprintDto sourceFingerprint,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null,
        IBundleArtifactCachePrunePolicy? bundleArtifactCachePrunePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(bundleArtifactStore);
        ArgumentNullException.ThrowIfNull(decryptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDataRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleCacheKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(sourceFingerprint);

        cancellationToken.ThrowIfCancellationRequested();

        var resolutionGateKey = CreateResolutionGateKey(workspaceKey, workspaceDataRootPath, bundleCacheKey);
        var resolutionGate = RentResolutionGate(resolutionGateKey);
        var hasEnteredGate = false;
        try
        {
            await resolutionGate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            hasEnteredGate = true;
            try
            {
                return await ResolveCoreAsync(
                    bundleArtifactStore,
                    decryptor,
                    workspaceKey,
                    workspaceDataRootPath,
                    bundleCacheKey,
                    sourcePath,
                    sourceFingerprint,
                    createdAtUtc,
                    cancellationToken,
                    bundleArtifactSessionInvalidator ?? NoOpBundleArtifactSessionInvalidator.Instance,
                    bundleArtifactCachePrunePolicy ?? NoOpBundleArtifactCachePrunePolicy.Instance).ConfigureAwait(false);
            }
            finally
            {
                if (hasEnteredGate)
                {
                    resolutionGate.Semaphore.Release();
                }

                ReturnResolutionGate(resolutionGateKey, resolutionGate);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!hasEnteredGate)
            {
                ReturnResolutionGate(resolutionGateKey, resolutionGate);
            }

            return Result<WorkspaceBundleArtifactResolutionDto>.Cancelled();
        }
    }

    public static WorkspaceBundleArtifactExistingInputResolutionDto TryResolveExistingInput(
        IBundleArtifactStore bundleArtifactStore,
        string workspaceKey,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        string? decryptorId,
        string? decryptorVersion,
        DateTimeOffset accessedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bundleArtifactStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleCacheKey);
        ArgumentNullException.ThrowIfNull(sourceFingerprint);

        var manifest = bundleArtifactStore.Load(workspaceKey, bundleCacheKey);
        if (manifest is null)
        {
            return new WorkspaceBundleArtifactExistingInputResolutionDto(
                WorkspaceBundleArtifactExistingInputStatus.MissingManifest,
                Input: null);
        }

        if (!CanReuseManifest(manifest, workspaceKey, bundleCacheKey, sourceFingerprint, decryptorId, decryptorVersion))
        {
            return new WorkspaceBundleArtifactExistingInputResolutionDto(
                WorkspaceBundleArtifactExistingInputStatus.StaleManifest,
                Input: null);
        }

        var cachedBundlePath = bundleArtifactStore.ResolveBundlePath(manifest);
        if (string.IsNullOrWhiteSpace(cachedBundlePath))
        {
            return new WorkspaceBundleArtifactExistingInputResolutionDto(
                WorkspaceBundleArtifactExistingInputStatus.MissingBundlePath,
                Input: null);
        }

        var accessedManifest = bundleArtifactStore.UpdateLastAccessed(manifest, accessedAtUtc);
        return new WorkspaceBundleArtifactExistingInputResolutionDto(
            WorkspaceBundleArtifactExistingInputStatus.Resolved,
            new WorkspaceBundleArtifactInputDto(
                cachedBundlePath,
                accessedManifest,
                CreateBundleSessionCacheKey(accessedManifest)));
    }

    private static async Task<Result<WorkspaceBundleArtifactResolutionDto>> ResolveCoreAsync(
        IBundleArtifactStore bundleArtifactStore,
        IAssetBundleDecryptor decryptor,
        string workspaceKey,
        string workspaceDataRootPath,
        string bundleCacheKey,
        string sourcePath,
        WorkspaceFileFingerprintDto sourceFingerprint,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken,
        IBundleArtifactSessionInvalidator bundleArtifactSessionInvalidator,
        IBundleArtifactCachePrunePolicy bundleArtifactCachePrunePolicy)
    {
        var manifest = bundleArtifactStore.Load(workspaceKey, bundleCacheKey);
        if (CanReuseManifest(manifest, workspaceKey, bundleCacheKey, sourceFingerprint, decryptor.Descriptor))
        {
            var cachedBundlePath = bundleArtifactStore.ResolveBundlePath(manifest!);
            if (!string.IsNullOrWhiteSpace(cachedBundlePath))
            {
                var accessedManifest = bundleArtifactStore.UpdateLastAccessed(manifest!, createdAtUtc);
                return Result<WorkspaceBundleArtifactResolutionDto>.Success(
                    new WorkspaceBundleArtifactResolutionDto(
                        cachedBundlePath,
                        accessedManifest,
                        CreateBundleSessionCacheKey(accessedManifest),
                        decryptor.Descriptor,
                        IsFromCache: true,
                        DiagnosticMessage: null));
            }
        }

        if (manifest is not null)
        {
            bundleArtifactStore.Delete(workspaceKey, bundleCacheKey);
            bundleArtifactSessionInvalidator.InvalidateBundleArtifact(workspaceKey, bundleCacheKey);
        }

        var decryptionResult = await decryptor.DecryptAsync(
            new AssetBundleDecryptionRequestDto(
                SourcePath: sourcePath,
                WorkspaceKey: workspaceKey,
                WorkspaceDataRootPath: workspaceDataRootPath,
                BundleCacheKey: bundleCacheKey),
            cancellationToken);
        if (decryptionResult.IsCancelled)
        {
            return Result<WorkspaceBundleArtifactResolutionDto>.Cancelled();
        }

        if (!decryptionResult.IsSuccess || decryptionResult.Value is null)
        {
            return Result<WorkspaceBundleArtifactResolutionDto>.Failure(decryptionResult.Errors);
        }

        var savedManifest = bundleArtifactStore.Save(new BundleArtifactSaveRequestDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: bundleCacheKey,
            SourceFingerprint: sourceFingerprint,
            DecryptorId: decryptionResult.Value.Decryptor.Id,
            DecryptorVersion: decryptionResult.Value.Decryptor.Version,
            NormalizerVersion: NormalizerVersion,
            ResolvedBundlePath: decryptionResult.Value.ResolvedBundlePath,
            ProducedDerivedFile: decryptionResult.Value.ProducedDerivedFile,
            ResolvedBundleFingerprint: decryptionResult.Value.BundleFingerprint,
            CreatedAtUtc: createdAtUtc,
            CodecParameters: decryptionResult.Value.CodecParameters));

        var resolvedBundlePath = bundleArtifactStore.ResolveBundlePath(savedManifest)
                                 ?? savedManifest.ResolvedBundlePath;
        PruneAfterSave(
            bundleArtifactStore,
            savedManifest.WorkspaceKey,
            savedManifest.BundleCacheKey,
            bundleArtifactSessionInvalidator,
            bundleArtifactCachePrunePolicy);

        return Result<WorkspaceBundleArtifactResolutionDto>.Success(
            new WorkspaceBundleArtifactResolutionDto(
                resolvedBundlePath,
                savedManifest,
                CreateBundleSessionCacheKey(savedManifest),
                decryptionResult.Value.Decryptor,
                IsFromCache: false,
                DiagnosticMessage: decryptionResult.Value.DiagnosticMessage));
    }

    private static string CreateResolutionGateKey(
        string workspaceKey,
        string workspaceDataRootPath,
        string bundleCacheKey)
    {
        return string.Join(
            '|',
            workspaceKey.Trim(),
            workspaceDataRootPath.Trim(),
            bundleCacheKey.Trim());
    }

    internal static string CreateBundleSessionCacheKey(BundleArtifactManifestDto manifest)
    {
        var fingerprint = manifest.BundleFingerprint;
        return string.Join(
            '|',
            "bundle-artifact",
            manifest.WorkspaceKey.Trim(),
            manifest.BundleCacheKey.Trim(),
            manifest.NormalizerVersion.Trim(),
            fingerprint.SourcePath.Trim(),
            fingerprint.FileSizeInBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fingerprint.LastWriteTimeUtc.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fingerprint.ContentHash?.Trim() ?? string.Empty);
    }

    private static ResolutionGateEntry RentResolutionGate(string key)
    {
        lock (ResolutionGatesSyncRoot)
        {
            if (!ResolutionGates.TryGetValue(key, out var entry))
            {
                entry = new ResolutionGateEntry();
                ResolutionGates[key] = entry;
            }

            entry.ReferenceCount++;
            return entry;
        }
    }

    private static void ReturnResolutionGate(string key, ResolutionGateEntry entry)
    {
        var shouldDispose = false;
        lock (ResolutionGatesSyncRoot)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount <= 0 &&
                entry.Semaphore.CurrentCount == 1 &&
                ResolutionGates.TryGetValue(key, out var currentEntry) &&
                ReferenceEquals(currentEntry, entry))
            {
                ResolutionGates.Remove(key);
                shouldDispose = true;
            }
        }

        if (shouldDispose)
        {
            entry.Semaphore.Dispose();
        }
    }

    private static bool CanReuseManifest(
        BundleArtifactManifestDto? manifest,
        string workspaceKey,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        AssetBundleDecryptorDescriptorDto decryptor)
    {
        return CanReuseManifest(
            manifest,
            workspaceKey,
            bundleCacheKey,
            sourceFingerprint,
            decryptor.Id,
            decryptor.Version);
    }

    private static bool CanReuseManifest(
        BundleArtifactManifestDto? manifest,
        string workspaceKey,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        string? decryptorId,
        string? decryptorVersion)
    {
        return manifest is not null &&
               string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
               string.Equals(manifest.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(decryptorId) &&
               string.Equals(manifest.DecryptorId, decryptorId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(manifest.DecryptorVersion ?? string.Empty, decryptorVersion ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(manifest.NormalizerVersion, NormalizerVersion, StringComparison.OrdinalIgnoreCase) &&
               WorkspaceFileFingerprintComparer.Matches(manifest.SourceFingerprint, sourceFingerprint);
    }

    private static void PruneAfterSave(
        IBundleArtifactStore bundleArtifactStore,
        string workspaceKey,
        string bundleCacheKey,
        IBundleArtifactSessionInvalidator bundleArtifactSessionInvalidator,
        IBundleArtifactCachePrunePolicy bundleArtifactCachePrunePolicy)
    {
        var request = bundleArtifactCachePrunePolicy.CreateRequest(workspaceKey, bundleCacheKey);
        if (request is null)
        {
            return;
        }

        var result = bundleArtifactStore.Prune(request);
        foreach (var prunedEntry in result.PrunedEntries)
        {
            bundleArtifactSessionInvalidator.InvalidateBundleArtifact(
                prunedEntry.WorkspaceKey,
                prunedEntry.BundleCacheKey);
        }
    }

    private sealed class ResolutionGateEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }
}
