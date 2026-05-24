using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一条工作区操作记录已从可恢复记录流中删除。
/// </summary>
public sealed record WorkspaceOperationRecordDeletedResultDto(
    WorkspaceId WorkspaceId,
    string OperationId,
    BundleId? BundleId = null,
    AssetId? AssetId = null,
    DateTimeOffset DeletedAtUtc = default,
    IReadOnlyList<WorkspaceOperationTargetDto>? AffectedTargets = null);
