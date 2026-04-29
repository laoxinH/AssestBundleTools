using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 为 Presentation 提供工作区目录恢复、登记和浏览状态元数据编排语义。
/// </summary>
public interface IWorkspaceContextCatalogService
{
    WorkspaceContextCatalogStateDto LoadCatalogState(IReadOnlyList<WorkspaceContextLiveSessionDto> liveSessions);

    WorkspaceCatalogSnapshotDto SaveCatalogState(
        IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries,
        WorkspaceSummaryDto? currentWorkspaceSummary);

    WorkspaceBrowserStateSnapshotDto? LoadWorkspaceBrowserState(string workspaceKey, string? workspaceDataRootPath);

    WorkspaceContextEntryRegistrationResultDto CreateRegisteredEntry(
        string workspaceKey,
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath,
        string? assetBundleDecryptorId,
        WorkspaceContextEntryDto? existingEntry,
        DateTimeOffset activatedAtUtc);

    WorkspaceContextEntryDto RefreshActivatedEntry(
        WorkspaceContextEntryDto existingEntry,
        DateTimeOffset activatedAtUtc);

    WorkspaceContextRestoreRequestDto? ResolveLastActiveWorkspaceRestoreRequest(
        string? lastActiveWorkspaceKey,
        IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries);

    void SaveWorkspaceActivationMetadata(
        WorkspaceContextEntryDto workspaceEntry,
        DateTimeOffset openedAtUtc,
        WorkspaceMetadataDto? existingMetadata = null);

    void SaveWorkspaceBrowserState(
        WorkspaceContextEntryDto workspaceEntry,
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceBrowserStateSnapshotDto browserStateSnapshot);

    IReadOnlyList<WorkspaceContextEntryDto> SortEntries(IEnumerable<WorkspaceContextEntryDto> workspaceEntries);
}
