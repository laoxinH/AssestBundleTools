using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 提供操作记录与资源目标之间的通用关联匹配规则。
/// </summary>
public static class WorkspaceOperationRelationMatcher
{
    public static bool OperationConcernsTarget(
        WorkspaceOperationRecordDto operation,
        string bundleId,
        string assetId)
    {
        return OperationConcernsTarget(operation, bundleId, assetId, sourceFingerprint: null);
    }

    public static bool OperationConcernsTarget(
        WorkspaceOperationRecordDto operation,
        string bundleId,
        string? assetId,
        string? sourceFingerprint)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return TargetMatches(operation.Target, bundleId, assetId) ||
               TargetSourceFingerprintMatches(operation.Target, bundleId, sourceFingerprint) ||
               (operation.Relations ?? Array.Empty<WorkspaceOperationRelationDto>())
               .Any(relation =>
                   TargetMatches(relation.RelatedTarget, bundleId, assetId) ||
                   TargetSourceFingerprintMatches(relation.RelatedTarget, bundleId, sourceFingerprint));
    }

    public static bool TargetMatches(
        WorkspaceOperationTargetDto? target,
        string? bundleId,
        string? assetId) =>
        !string.IsNullOrWhiteSpace(target?.BundleId) &&
        !string.IsNullOrWhiteSpace(target.AssetId) &&
        !string.IsNullOrWhiteSpace(bundleId) &&
        !string.IsNullOrWhiteSpace(assetId) &&
        string.Equals(target.BundleId.Trim(), bundleId.Trim(), StringComparison.Ordinal) &&
        string.Equals(target.AssetId.Trim(), assetId.Trim(), StringComparison.Ordinal);

    private static bool TargetSourceFingerprintMatches(
        WorkspaceOperationTargetDto? target,
        string? bundleId,
        string? sourceFingerprint) =>
        !string.IsNullOrWhiteSpace(target?.BundleId) &&
        !string.IsNullOrWhiteSpace(target.SourceFingerprint) &&
        !string.IsNullOrWhiteSpace(bundleId) &&
        !string.IsNullOrWhiteSpace(sourceFingerprint) &&
        string.Equals(target.BundleId.Trim(), bundleId.Trim(), StringComparison.Ordinal) &&
        string.Equals(target.SourceFingerprint.Trim(), sourceFingerprint.Trim(), StringComparison.Ordinal);
}
