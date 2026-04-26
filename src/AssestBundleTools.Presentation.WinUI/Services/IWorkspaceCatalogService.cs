using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 聚合工作区目录管理相关的应用层处理器。
/// </summary>
public interface IWorkspaceCatalogService
{
    Task<Result<WorkspaceCatalogSnapshotDto>> GetCatalogSnapshotAsync(CancellationToken cancellationToken = default);

    Task<Result<WorkspaceCatalogEntryDto>> RegisterWorkspaceEntryAsync(
        RegisterWorkspaceCatalogEntryCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceCatalogSnapshotDto>> RemoveWorkspaceEntryAsync(
        RemoveWorkspaceCatalogEntryCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceCatalogSnapshotDto>> SetWorkspaceEntryPinnedStateAsync(
        SetWorkspaceCatalogEntryPinnedStateCommand command,
        CancellationToken cancellationToken = default);
}
