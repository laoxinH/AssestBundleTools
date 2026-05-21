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
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null);
}

/// <summary>
/// 表示一次操作通知中可精确定位的资产目标。
/// </summary>
public sealed record WorkspaceOperationAffectedAsset(
    BundleId BundleId,
    AssetId AssetId);

[Flags]
public enum WorkspaceOperationChangeKind
{
    None = 0,
    Extraction = 1,
    Replacement = 2,
    Save = 4
}

public sealed class WorkspaceOperationChangedEventArgs : EventArgs
{
    public WorkspaceOperationChangedEventArgs(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null)
    {
        WorkspaceId = workspaceId;
        ChangeKind = changeKind;
        AffectedAssets = NormalizeAffectedAssets(affectedAssets);
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceOperationChangeKind ChangeKind { get; }

    public IReadOnlyList<WorkspaceOperationAffectedAsset> AffectedAssets { get; }

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
            MergeAffectedAssets(AffectedAssets, other.AffectedAssets));
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
            .Select(static group => group.First())
            .ToArray();
    }
}
