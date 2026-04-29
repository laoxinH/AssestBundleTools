using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 在 Presentation 层共享当前主工作区上下文。
/// </summary>
/// <remarks>
/// 这个服务不负责业务计算，只负责跨页面共享“当前正在工作的工作区是谁”，
/// 便于批处理页、保存摘要页等页面读取同一份会话上下文。
/// </remarks>
public interface IWorkspaceContextService
{
    event EventHandler? CurrentWorkspaceChanged;

    event EventHandler? WorkspaceEntriesChanged;

    WorkspaceSummaryDto? CurrentWorkspaceSummary { get; }

    WorkspaceSummaryDto? ActiveWorkspaceSummary { get; }

    bool HasCurrentWorkspace { get; }

    IReadOnlyList<WorkspaceContextEntryDto> WorkspaceEntries { get; }

    int WorkspaceEntryCount { get; }

    bool TryGetCurrentWorkspaceEntry(out WorkspaceContextEntryDto? workspaceEntry);

    WorkspaceBrowserStateSnapshotDto? GetCurrentWorkspaceBrowserState();

    bool TryGetLastActiveWorkspaceNavigationRequest(out WorkspaceNavigationRequest? workspaceNavigationRequest);

    void RegisterOrActivateWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath = null,
        string? assetBundleDecryptorId = null);

    void ReloadWorkspaceCatalogEntries();

    void UpdateCurrentWorkspaceBrowserState(WorkspaceBrowserStateSnapshotDto browserStateSnapshot);

    bool TryActivateWorkspace(WorkspaceId workspaceId);

    void SetCurrentWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath = null,
        string? assetBundleDecryptorId = null);

    void ClearCurrentWorkspace();
}
