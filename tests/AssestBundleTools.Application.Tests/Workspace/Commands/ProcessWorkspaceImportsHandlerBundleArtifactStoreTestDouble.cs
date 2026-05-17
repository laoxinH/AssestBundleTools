using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static partial class ProcessWorkspaceImportsHandlerTests
{
    private sealed class TestBundleArtifactStore : IBundleArtifactStore
    {
        private readonly object _syncRoot = new();
        private readonly List<BundleArtifactManifestDto> _manifests = [];

        public List<(string WorkspaceKey, string BundleCacheKey)> DeleteOperations { get; } = [];

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
            var updatedManifest = manifest with
            {
                LastAccessedAtUtc = lastAccessedAtUtc,
            };
            lock (_syncRoot)
            {
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

        public BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request) =>
            new(0, 0, 0, 0, Array.Empty<BundleArtifactCacheEntryDto>());

        public void Delete(string workspaceKey, string bundleCacheKey)
        {
            lock (_syncRoot)
            {
                DeleteOperations.Add((workspaceKey, bundleCacheKey));
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
}
