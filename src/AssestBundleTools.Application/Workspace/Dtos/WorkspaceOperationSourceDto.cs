namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 描述一次可恢复操作使用的来源。
/// </summary>
public sealed record WorkspaceOperationSourceDto(
    string? SourceKind = null,
    string? SourceBundleId = null,
    string? SourceBundleDisplayName = null,
    string? SourceAssetId = null,
    string? SourceAssetName = null,
    string? SourceAssetType = null,
    string? SourceFilePath = null,
    string? SourceFileName = null,
    long? SourceFileSizeInBytes = null,
    DateTimeOffset? SourceFileLastWriteTimeUtc = null,
    string? SourceFileFingerprint = null,
    string? DraftId = null,
    string? DraftMode = null,
    string? OutputDirectory = null);
