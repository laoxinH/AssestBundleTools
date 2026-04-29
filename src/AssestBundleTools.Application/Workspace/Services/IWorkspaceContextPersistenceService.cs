using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 为 Presentation 提供工作区目录与恢复元数据的应用层持久化入口。
/// </summary>
/// <remarks>
/// 这个服务负责把“目录快照读取/保存、工作区浏览状态读取/回写、基础元数据补齐”
/// 统一收口到 Application 边界后面，避免 UI 会话服务直接拼装仓储访问细节。
/// </remarks>
public interface IWorkspaceContextPersistenceService
{
    WorkspaceCatalogSnapshotDto LoadWorkspaceCatalogSnapshot();

    void SaveWorkspaceCatalogSnapshot(WorkspaceCatalogSnapshotDto snapshot);

    WorkspaceMetadataDto? LoadWorkspaceMetadata(string workspaceKey, string? workspaceDataRootPath);

    WorkspaceBrowserStateSnapshotDto? LoadWorkspaceBrowserState(string workspaceKey, string? workspaceDataRootPath);

    void SaveWorkspaceActivationMetadata(WorkspaceActivationMetadataSaveRequestDto request);

    void SaveWorkspaceBrowserState(WorkspaceBrowserStateSaveRequestDto request);

    string GetDefaultWorkspaceDataRootPath();

    WorkspaceImportPathKind ResolveOptionalPathKind(string? path);
}
