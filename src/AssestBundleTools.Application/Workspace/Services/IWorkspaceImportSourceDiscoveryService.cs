using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责把工作区导入来源展开成真实文件列表。
/// </summary>
public interface IWorkspaceImportSourceDiscoveryService
{
    IReadOnlyList<DiscoveredWorkspaceImportFileDto> Discover(
        IReadOnlyList<WorkspaceImportedSourceDto> importedSources,
        IReadOnlyList<string>? excludedSourcePaths = null);
}
