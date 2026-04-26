namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示由工作区导入来源展开得到的单个实际文件。
/// </summary>
public sealed record DiscoveredWorkspaceImportFileDto(
    string SourcePath,
    string LogicalPath);
