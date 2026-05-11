namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 描述一次可恢复操作指向的资源目标。
/// </summary>
public sealed record WorkspaceOperationTargetDto(
    string? BundleId = null,
    string? AssetId = null,
    string? AssetName = null,
    string? AssetType = null,
    string? AssetTypeDisplayName = null,
    string? SerializedFileName = null,
    long? PathId = null,
    string? SourceFingerprint = null);
