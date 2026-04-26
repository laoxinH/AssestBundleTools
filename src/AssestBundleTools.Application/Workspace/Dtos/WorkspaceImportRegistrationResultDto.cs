namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一次工作区导入来源登记的结果摘要。
/// </summary>
public sealed record WorkspaceImportRegistrationResultDto(
    string WorkspaceKey,
    string WorkspaceDisplayName,
    WorkspaceImportOriginKind ImportOrigin,
    int RequestedPathCount,
    int AddedPathCount,
    int UpdatedPathCount,
    int FileCount,
    int DirectoryCount,
    int TotalRegisteredSourceCount,
    DateTimeOffset RecordedAtUtc);
