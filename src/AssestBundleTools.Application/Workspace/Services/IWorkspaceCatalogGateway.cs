using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合工作区目录相关用例，向 Presentation 暴露稳定入口。
/// </summary>
public interface IWorkspaceCatalogGateway
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
