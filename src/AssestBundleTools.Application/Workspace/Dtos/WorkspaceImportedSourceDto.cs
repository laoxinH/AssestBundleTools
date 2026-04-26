namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个工作区已登记的单条导入来源描述。
/// </summary>
/// <remarks>
/// 这里保存的是“导入入口层已经记录了哪些外部来源”，
/// 不是实际 AB 解析后的资源索引结果。
/// 真正的解析数据仍应落到缓存层，而不是混入元数据文件。
/// </remarks>
public sealed record WorkspaceImportedSourceDto(
    string Path,
    WorkspaceImportOriginKind ImportOrigin,
    WorkspaceImportPathKind PathKind,
    DateTimeOffset RecordedAtUtc);
