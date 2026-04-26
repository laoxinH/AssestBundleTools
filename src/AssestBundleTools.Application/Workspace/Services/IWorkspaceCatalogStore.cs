using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责持久化“工作区目录状态”。
/// </summary>
/// <remarks>
/// 该存储只处理最近工作区、固定项和最后活动工作区等轻量索引，
/// 不直接承载任何大体量运行时资源数据。
/// </remarks>
public interface IWorkspaceCatalogStore
{
    WorkspaceCatalogSnapshotDto Load();

    void Save(WorkspaceCatalogSnapshotDto snapshot);
}
