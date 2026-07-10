using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 解析保存 / 重建计划可复用的标准化 bundle 只读输入。
/// </summary>
/// <remarks>
/// 当前保存链路仍是骨架，因此这里只读取已有 artifact manifest，
/// 不触发解密、复制或 materialize，避免把真实写包语义提前耦合进保存预览。
/// </remarks>
internal sealed class WorkspaceSaveArtifactInputResolver
{
    private const string DiagnosticMissingContext = "Save.Artifact.MissingWorkspaceContext";
    private const string DiagnosticMissingBundleIndex = "Save.Artifact.MissingBundleIndex";
    private const string DiagnosticMissingManifest = "Save.Artifact.MissingManifest";
    private const string DiagnosticStaleManifest = "Save.Artifact.StaleManifest";
    private const string DiagnosticMissingBundlePath = "Save.Artifact.MissingBundlePath";

    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public WorkspaceSaveArtifactInputResolver(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public IReadOnlyDictionary<string, WorkspaceSaveBundleArtifactInput> ResolveInputs(
        string? workspaceKey,
        string? workspaceDataRootPath,
        IReadOnlyCollection<BundleId> bundleIds,
        DateTimeOffset accessedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(bundleIds);

        if (bundleIds.Count == 0)
        {
            return new Dictionary<string, WorkspaceSaveBundleArtifactInput>(StringComparer.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return CreateUnresolvedInputs(bundleIds, bundleCacheKey: null, DiagnosticMissingContext);
        }

        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(workspaceDataRootPath);
        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(workspaceDataRootPath);
        var indexEntriesByBundleId = bundleIndexCacheStore
            .LoadAll(workspaceKey)
            .GroupBy(static entry => entry.BundleId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).First(),
                StringComparer.Ordinal);

        var resolvedInputs = new Dictionary<string, WorkspaceSaveBundleArtifactInput>(StringComparer.Ordinal);
        foreach (var bundleId in bundleIds)
        {
            if (!indexEntriesByBundleId.TryGetValue(bundleId.Value, out var indexEntry))
            {
                resolvedInputs[bundleId.Value] = WorkspaceSaveBundleArtifactInput.Unresolved(
                    bundleCacheKey: null,
                    DiagnosticMissingBundleIndex);
                continue;
            }

            var artifactResolution = WorkspaceBundleArtifactResolver.TryResolveExistingInput(
                bundleArtifactStore,
                workspaceKey,
                indexEntry.BundleCacheKey,
                indexEntry.SourceFingerprint,
                indexEntry.DecryptorId,
                indexEntry.DecryptorVersion,
                accessedAtUtc);

            resolvedInputs[bundleId.Value] = ToSaveArtifactInput(
                indexEntry.BundleCacheKey,
                indexEntry.OriginalCompression,
                indexEntry.DecryptorId,
                indexEntry.DecryptorVersion,
                artifactResolution);
        }

        return resolvedInputs;
    }

    private static Dictionary<string, WorkspaceSaveBundleArtifactInput> CreateUnresolvedInputs(
        IEnumerable<BundleId> bundleIds,
        string? bundleCacheKey,
        string diagnosticCode)
    {
        return bundleIds.ToDictionary(
            static bundleId => bundleId.Value,
            _ => WorkspaceSaveBundleArtifactInput.Unresolved(bundleCacheKey, diagnosticCode),
            StringComparer.Ordinal);
    }

    private static WorkspaceSaveBundleArtifactInput ToSaveArtifactInput(
        string bundleCacheKey,
        string? originalCompression,
        string? sourceCodecId,
        string? sourceCodecVersion,
        WorkspaceBundleArtifactExistingInputResolutionDto artifactResolution)
    {
        if (artifactResolution.Input is not null)
        {
            return new WorkspaceSaveBundleArtifactInput(
                BundleCacheKey: bundleCacheKey,
                HasStandardizedArtifact: true,
                ArtifactBundlePath: artifactResolution.Input.ResolvedBundlePath,
                BundleSessionCacheKey: artifactResolution.Input.BundleSessionCacheKey,
                ArtifactDiagnosticCode: null,
                OriginalCompression: originalCompression,
                SourceCodecId: sourceCodecId,
                SourceCodecVersion: sourceCodecVersion);
        }

        return WorkspaceSaveBundleArtifactInput.Unresolved(
            bundleCacheKey,
            artifactResolution.Status switch
            {
                WorkspaceBundleArtifactExistingInputStatus.MissingManifest => DiagnosticMissingManifest,
                WorkspaceBundleArtifactExistingInputStatus.StaleManifest => DiagnosticStaleManifest,
                WorkspaceBundleArtifactExistingInputStatus.MissingBundlePath => DiagnosticMissingBundlePath,
                _ => DiagnosticMissingManifest
            });
    }
}

internal sealed record WorkspaceSaveBundleArtifactInput(
    string? BundleCacheKey,
    bool HasStandardizedArtifact,
    string? ArtifactBundlePath,
    string? BundleSessionCacheKey,
    string? ArtifactDiagnosticCode,
    string? OriginalCompression = null,
    string? SourceCodecId = null,
    string? SourceCodecVersion = null)
{
    public static WorkspaceSaveBundleArtifactInput Unresolved(
        string? bundleCacheKey,
        string diagnosticCode) =>
        new(
            BundleCacheKey: bundleCacheKey,
            HasStandardizedArtifact: false,
            ArtifactBundlePath: null,
            BundleSessionCacheKey: null,
            ArtifactDiagnosticCode: diagnosticCode,
            OriginalCompression: null,
            SourceCodecId: null,
            SourceCodecVersion: null);
}
