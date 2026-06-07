using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 在 Presentation 层广播工作区操作完成后的读模型失效事件。
/// </summary>
public interface IWorkspaceOperationChangeNotifier
{
    event EventHandler<WorkspaceOperationChangedEventArgs>? WorkspaceOperationsChanged;

    IDisposable BeginNotificationBatch();

    void NotifyWorkspaceOperationsChanged(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null,
        WorkspaceOperationChangeReason reason = WorkspaceOperationChangeReason.Unspecified,
        IReadOnlyList<string>? operationIds = null);
}

/// <summary>
/// 表示一次操作通知中可精确定位的资产目标。
/// </summary>
public sealed record WorkspaceOperationAffectedAsset(
    BundleId BundleId,
    AssetId AssetId,
    WorkspaceOperationAffectedAssetRole Role = WorkspaceOperationAffectedAssetRole.PrimaryTarget);

public enum WorkspaceOperationAffectedAssetRole
{
    Affected = 0,
    PrimaryTarget = 1,
    RelatedTarget = 2,
    ImplicitAffectedTarget = 3
}

[Flags]
public enum WorkspaceOperationChangeKind
{
    None = 0,
    Extraction = 1,
    Replacement = 2,
    Save = 4,
    Edit = 8
}

public enum WorkspaceOperationChangeReason
{
    Unspecified = 0,
    Mixed = 1,
    ExtractionRecorded = 2,
    ReplacementApplied = 3,
    ReplacementUndone = 4,
    ReplacementRedone = 5,
    RecordDeleted = 6,
    EditApplied = 7,
    SaveCheckpointChanged = 8,
    BatchExtractionRecorded = 9,
    BatchReplacementApplied = 10
}

public sealed class WorkspaceOperationChangedEventArgs : EventArgs
{
    public WorkspaceOperationChangedEventArgs(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null,
        WorkspaceOperationChangeReason reason = WorkspaceOperationChangeReason.Unspecified,
        IReadOnlyList<string>? operationIds = null)
    {
        WorkspaceId = workspaceId;
        ChangeKind = changeKind;
        AffectedAssets = NormalizeAffectedAssets(affectedAssets);
        Reason = reason;
        OperationIds = NormalizeOperationIds(operationIds);
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceOperationChangeKind ChangeKind { get; }

    public IReadOnlyList<WorkspaceOperationAffectedAsset> AffectedAssets { get; }

    public IReadOnlyList<string> OperationIds { get; }

    public WorkspaceOperationChangeReason Reason { get; }

    public IReadOnlyList<WorkspaceOperationAffectedAsset> PrimaryTargets =>
        AffectedAssets.Where(static asset => asset.Role == WorkspaceOperationAffectedAssetRole.PrimaryTarget).ToArray();

    public IReadOnlyList<WorkspaceOperationAffectedAsset> RelatedTargets =>
        AffectedAssets.Where(static asset => asset.Role == WorkspaceOperationAffectedAssetRole.RelatedTarget).ToArray();

    public IReadOnlyList<WorkspaceOperationAffectedAsset> ImplicitAffectedTargets =>
        AffectedAssets.Where(static asset => asset.Role == WorkspaceOperationAffectedAssetRole.ImplicitAffectedTarget).ToArray();

    public bool IsWorkspaceWide => AffectedAssets.Count == 0;

    public WorkspaceOperationChangedEventArgs Merge(WorkspaceOperationChangedEventArgs other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!WorkspaceId.Equals(other.WorkspaceId))
        {
            return other;
        }

        return new WorkspaceOperationChangedEventArgs(
            WorkspaceId,
            ChangeKind | other.ChangeKind,
            MergeAffectedAssets(AffectedAssets, other.AffectedAssets),
            MergeReason(Reason, other.Reason),
            MergeOperationIds(OperationIds, other.OperationIds));
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> MergeAffectedAssets(
        IReadOnlyList<WorkspaceOperationAffectedAsset> first,
        IReadOnlyList<WorkspaceOperationAffectedAsset> second)
    {
        if (first.Count == 0 || second.Count == 0)
        {
            return Array.Empty<WorkspaceOperationAffectedAsset>();
        }

        return NormalizeAffectedAssets(first.Concat(second));
    }

    private static IReadOnlyList<string> MergeOperationIds(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second) =>
        NormalizeOperationIds(first.Concat(second));

    private static WorkspaceOperationChangeReason MergeReason(
        WorkspaceOperationChangeReason first,
        WorkspaceOperationChangeReason second)
    {
        if (first == second)
        {
            return first;
        }

        if (first == WorkspaceOperationChangeReason.Unspecified)
        {
            return second;
        }

        if (second == WorkspaceOperationChangeReason.Unspecified)
        {
            return first;
        }

        return WorkspaceOperationChangeReason.Mixed;
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> NormalizeAffectedAssets(
        IEnumerable<WorkspaceOperationAffectedAsset>? affectedAssets)
    {
        if (affectedAssets is null)
        {
            return Array.Empty<WorkspaceOperationAffectedAsset>();
        }

        return affectedAssets
            .Where(static item => item is not null)
            .Where(static item =>
                !string.IsNullOrWhiteSpace(item.BundleId.Value) &&
                !string.IsNullOrWhiteSpace(item.AssetId.Value))
            .GroupBy(
                static item => $"{item.BundleId.Value}\u001f{item.AssetId.Value}",
                StringComparer.Ordinal)
            .Select(static group => group
                .OrderBy(static item => GetRolePriority(item.Role))
                .First())
            .ToArray();
    }

    private static int GetRolePriority(WorkspaceOperationAffectedAssetRole role) =>
        role switch
        {
            WorkspaceOperationAffectedAssetRole.PrimaryTarget => 0,
            WorkspaceOperationAffectedAssetRole.RelatedTarget => 1,
            WorkspaceOperationAffectedAssetRole.ImplicitAffectedTarget => 2,
            _ => 3
        };

    private static IReadOnlyList<string> NormalizeOperationIds(IEnumerable<string>? operationIds)
    {
        if (operationIds is null)
        {
            return Array.Empty<string>();
        }

        return operationIds
            .Where(static operationId => !string.IsNullOrWhiteSpace(operationId))
            .Select(static operationId => operationId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
