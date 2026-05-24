using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    private sealed class WorkspaceSessionState
    {
        public WorkspaceSessionState(
            WorkspaceSummaryDto summary,
            IReadOnlyList<BundleTreeNodeDto> bundleTree,
            IReadOnlyDictionary<string, CachedBundleState> bundlesById,
            IReadOnlyDictionary<string, AssetGraphNodeState> assetsById)
        {
            Summary = summary;
            BundleTree = bundleTree;
            BundlesById = bundlesById;
            AssetsById = assetsById;
            ReverseDependenciesByAssetId = new Dictionary<string, IReadOnlyList<AssetDependencyDto>>(StringComparer.Ordinal);
            TextureAssociationsByAssetId = new Dictionary<string, IReadOnlyList<AssetDependencyDto>>(StringComparer.Ordinal);
        }

        public WorkspaceSummaryDto Summary { get; }

        public IReadOnlyList<BundleTreeNodeDto> BundleTree { get; }

        public IReadOnlyDictionary<string, CachedBundleState> BundlesById { get; }

        public IReadOnlyDictionary<string, AssetGraphNodeState> AssetsById { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> ReverseDependenciesByAssetId { get; private set; }

        public IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> TextureAssociationsByAssetId { get; private set; }

        public void InitializeDependencyIndexes()
        {
            ReverseDependenciesByAssetId = BuildReverseDependenciesByAssetId(AssetsById);
            TextureAssociationsByAssetId = BuildTextureAssociationsByAssetId(AssetsById, ReverseDependenciesByAssetId);
        }

        public bool TryGetReverseDependencies(
            AssetId assetId,
            out IReadOnlyList<AssetDependencyDto> reverseDependencies)
        {
            if (!string.IsNullOrWhiteSpace(assetId.Value) &&
                ReverseDependenciesByAssetId.TryGetValue(assetId.Value, out var cachedReverseDependencies))
            {
                reverseDependencies = cachedReverseDependencies;
                return true;
            }

            reverseDependencies = Array.Empty<AssetDependencyDto>();
            return false;
        }

        public bool TryGetTextureAssociations(
            AssetId assetId,
            out IReadOnlyList<AssetDependencyDto> textureAssociations)
        {
            if (!string.IsNullOrWhiteSpace(assetId.Value) &&
                TextureAssociationsByAssetId.TryGetValue(assetId.Value, out var cachedTextureAssociations))
            {
                textureAssociations = cachedTextureAssociations;
                return true;
            }

            textureAssociations = Array.Empty<AssetDependencyDto>();
            return false;
        }
    }

    private sealed record AssetGraphNodeState(
        BundleId BundleId,
        CachedAssetState AssetState);

    private sealed record CachedBundleState(
        BundleId BundleId,
        string DisplayName,
        string LogicalPath,
        string SourcePath,
        IReadOnlyList<BundleExternalDependencyDto> BundleExternalDependencies,
        IReadOnlyList<CachedAssetState> Assets);

    /// <summary>
    /// 表示当前工作区内某个 serialized file 对应到的资源包目标。
    /// 用于把 `archive:/CAB-*` 这类外部依赖尽量还原为用户更熟悉的 `xxx.ab` 文件名。
    /// </summary>
    private sealed record WorkspaceSerializedFileBundleTarget(
        BundleId BundleId,
        string DisplayName,
        string LogicalPath);

    private sealed record WorkspaceAssetDependencyTarget(
        AssetId AssetId,
        BundleId BundleId,
        string Name,
        AssetType AssetType,
        string? AssetTypeDisplayName,
        long PathId);

    private readonly record struct WorkspaceAssetObjectKey(string SerializedFileName, long PathId);

    private readonly record struct AssetTypeFacetKey(
        AssetType AssetType,
        string AssetTypeDisplayName);

    private sealed class AssetTypeFacetKeyComparer : IEqualityComparer<AssetTypeFacetKey>
    {
        public static AssetTypeFacetKeyComparer Instance { get; } = new();

        public bool Equals(AssetTypeFacetKey x, AssetTypeFacetKey y)
        {
            return x.AssetType == y.AssetType &&
                   string.Equals(
                       x.AssetTypeDisplayName,
                       y.AssetTypeDisplayName,
                       StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(AssetTypeFacetKey obj)
        {
            return HashCode.Combine(
                obj.AssetType,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AssetTypeDisplayName));
        }
    }

    private sealed class WorkspaceAssetDependencyTargetLookup
    {
        private readonly IReadOnlyDictionary<string, WorkspaceAssetDependencyTarget> _targetsByAssetId;
        private readonly IReadOnlyDictionary<WorkspaceAssetObjectKey, WorkspaceAssetDependencyTarget> _targetsByObjectKey;

        public WorkspaceAssetDependencyTargetLookup(
            IReadOnlyDictionary<string, WorkspaceAssetDependencyTarget> targetsByAssetId,
            IReadOnlyDictionary<WorkspaceAssetObjectKey, WorkspaceAssetDependencyTarget> targetsByObjectKey)
        {
            _targetsByAssetId = targetsByAssetId;
            _targetsByObjectKey = targetsByObjectKey;
        }

        public bool TryResolve(
            BundleIndexAssetDependencyCacheEntryDto dependency,
            out WorkspaceAssetDependencyTarget target)
        {
            if (!string.IsNullOrWhiteSpace(dependency.AssetId) &&
                _targetsByAssetId.TryGetValue(dependency.AssetId, out target!))
            {
                return true;
            }

            foreach (var objectKey in CreateDependencyObjectKeys(dependency))
            {
                if (_targetsByObjectKey.TryGetValue(objectKey, out target!))
                {
                    return true;
                }
            }

            target = null!;
            return false;
        }
    }

    private sealed class CachedAssetState
    {
        public CachedAssetState(
            AssetId assetId,
            string name,
            string assetPath,
            string containerPath,
            string serializedFileName,
            AssetType assetType,
            string assetTypeDisplayName,
            long sizeInBytes,
            long? preferredDisplaySizeInBytes,
            long pathId,
            bool hasPreview,
            bool canExtract,
            bool canReplace,
            IReadOnlyList<AssetDependencyDto> dependencies)
        {
            AssetId = assetId;
            Name = name;
            AssetPath = assetPath;
            ContainerPath = containerPath;
            SerializedFileName = string.IsNullOrWhiteSpace(serializedFileName)
                ? string.Empty
                : serializedFileName.Trim().Replace('\\', '/');
            AssetType = assetType;
            AssetTypeDisplayName = assetTypeDisplayName;
            SizeInBytes = sizeInBytes;
            PreferredDisplaySizeInBytes = preferredDisplaySizeInBytes;
            PathId = pathId;
            HasPreview = hasPreview;
            CanExtract = canExtract;
            CanReplace = canReplace;
            Dependencies = dependencies;
            IncludeInAssetList = true;
        }

        public AssetId AssetId { get; }

        public string Name { get; }

        public string AssetPath { get; }

        public string ContainerPath { get; }

        public string SerializedFileName { get; }

        public AssetType AssetType { get; }

        public string AssetTypeDisplayName { get; }

        public long SizeInBytes { get; }

        public long? PreferredDisplaySizeInBytes { get; }

        public long PathId { get; }

        public bool HasPreview { get; }

        public bool CanExtract { get; }

        public bool CanReplace { get; }

        public IReadOnlyList<AssetDependencyDto> Dependencies { get; }

        public bool IncludeInAssetList { get; init; }

        public bool IsModified { get; set; }

        public DateTimeOffset? LastExtractedAtUtc { get; set; }

        public AppliedWorkspaceReplacementState? AppliedWorkspaceReplacement { get; set; }

        public AppliedFileReplacementState? AppliedFileReplacement { get; set; }

        public AppliedSpriteEditState? AppliedSpriteEdit { get; set; }

        public List<AppliedReplacementSnapshot?> ReplacementUndoStack { get; } = [];

        public List<AppliedReplacementSnapshot?> ReplacementRedoStack { get; } = [];
    }

    private sealed record AppliedWorkspaceReplacementState(
        BundleId SourceBundleId,
        AssetId SourceAssetId,
        string SourceBundleDisplayName,
        string SourceAssetName,
        AssetType SourceAssetType,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedFileReplacementState(
        string SourceFilePath,
        string SourceFileName,
        AssetType SourceAssetType,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedSpriteEditState(
        string DraftId,
        string DraftMode,
        WorkspaceSpriteEditGeometryDto Geometry,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedReplacementSnapshot(
        AppliedWorkspaceReplacementState? WorkspaceReplacement,
        AppliedFileReplacementState? FileReplacement);

    private static void ApplyReplacementSnapshot(
        CachedAssetState assetState,
        AppliedReplacementSnapshot replacementSnapshot)
    {
        assetState.ReplacementUndoStack.Add(CaptureReplacementSnapshot(assetState));
        assetState.ReplacementRedoStack.Clear();
        RestoreReplacementSnapshot(assetState, replacementSnapshot);
    }

    private static void UndoReplacementSnapshot(CachedAssetState assetState)
    {
        var currentReplacement = CaptureReplacementSnapshot(assetState);
        if (currentReplacement is null)
        {
            return;
        }

        assetState.ReplacementRedoStack.Add(currentReplacement);
        RestoreReplacementSnapshot(assetState, PopReplacementSnapshot(assetState.ReplacementUndoStack));
    }

    private static void RedoReplacementSnapshot(CachedAssetState assetState)
    {
        var redoReplacement = PopReplacementSnapshot(assetState.ReplacementRedoStack);
        if (redoReplacement is null)
        {
            return;
        }

        assetState.ReplacementUndoStack.Add(CaptureReplacementSnapshot(assetState));
        RestoreReplacementSnapshot(assetState, redoReplacement);
    }

    private static AppliedReplacementSnapshot? CaptureReplacementSnapshot(CachedAssetState assetState)
    {
        if (assetState.AppliedWorkspaceReplacement is null &&
            assetState.AppliedFileReplacement is null)
        {
            return null;
        }

        return new AppliedReplacementSnapshot(
            assetState.AppliedWorkspaceReplacement,
            assetState.AppliedFileReplacement);
    }

    private static void RestoreReplacementSnapshot(
        CachedAssetState assetState,
        AppliedReplacementSnapshot? replacementSnapshot)
    {
        assetState.AppliedWorkspaceReplacement = replacementSnapshot?.WorkspaceReplacement;
        assetState.AppliedFileReplacement = replacementSnapshot?.FileReplacement;
        RefreshModifiedState(assetState);
    }

    private static void ApplySpriteEditState(
        CachedAssetState assetState,
        AppliedSpriteEditState spriteEdit)
    {
        assetState.AppliedSpriteEdit = spriteEdit;
        RefreshModifiedState(assetState);
    }

    private static void ClearSpriteEditState(CachedAssetState assetState)
    {
        assetState.AppliedSpriteEdit = null;
        RefreshModifiedState(assetState);
    }

    private static void RefreshModifiedState(CachedAssetState assetState)
    {
        assetState.IsModified =
            assetState.AppliedWorkspaceReplacement is not null ||
            assetState.AppliedFileReplacement is not null ||
            assetState.AppliedSpriteEdit is not null;
    }

    private static AppliedReplacementSnapshot? PopReplacementSnapshot(
        IList<AppliedReplacementSnapshot?> stack)
    {
        if (stack.Count == 0)
        {
            return null;
        }

        var snapshot = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return snapshot;
    }
}
