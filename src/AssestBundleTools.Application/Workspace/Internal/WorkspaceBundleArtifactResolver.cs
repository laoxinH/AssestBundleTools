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
        CancellationToken cancellationToken)
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
                    cancellationToken).ConfigureAwait(false);
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

    private static async Task<Result<WorkspaceBundleArtifactResolutionDto>> ResolveCoreAsync(
        IBundleArtifactStore bundleArtifactStore,
        IAssetBundleDecryptor decryptor,
        string workspaceKey,
        string workspaceDataRootPath,
        string bundleCacheKey,
        string sourcePath,
        WorkspaceFileFingerprintDto sourceFingerprint,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var manifest = bundleArtifactStore.Load(workspaceKey, bundleCacheKey);
        if (CanReuseManifest(manifest, workspaceKey, bundleCacheKey, sourceFingerprint, decryptor.Descriptor))
        {
            var cachedBundlePath = bundleArtifactStore.ResolveBundlePath(manifest!);
            if (!string.IsNullOrWhiteSpace(cachedBundlePath))
            {
                return Result<WorkspaceBundleArtifactResolutionDto>.Success(
                    new WorkspaceBundleArtifactResolutionDto(
                        cachedBundlePath,
                        manifest!,
                        CreateBundleSessionCacheKey(manifest!),
                        decryptor.Descriptor,
                        IsFromCache: true,
                        DiagnosticMessage: null));
            }
        }

        if (manifest is not null)
        {
            bundleArtifactStore.Delete(workspaceKey, bundleCacheKey);
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
            CreatedAtUtc: createdAtUtc));

        var resolvedBundlePath = bundleArtifactStore.ResolveBundlePath(savedManifest)
                                 ?? savedManifest.ResolvedBundlePath;

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

    private static string CreateBundleSessionCacheKey(BundleArtifactManifestDto manifest)
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
        return manifest is not null &&
               string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
               string.Equals(manifest.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(manifest.DecryptorId, decryptor.Id, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(manifest.DecryptorVersion, decryptor.Version, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(manifest.NormalizerVersion, NormalizerVersion, StringComparison.OrdinalIgnoreCase) &&
               WorkspaceFileFingerprintComparer.Matches(manifest.SourceFingerprint, sourceFingerprint);
    }

    private sealed class ResolutionGateEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }
}
