namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区目录项登记时的解析结果。
/// </summary>
public sealed record WorkspaceContextEntryRegistrationResultDto(
    WorkspaceContextEntryDto WorkspaceEntry,
    WorkspaceMetadataDto? ExistingMetadata);
