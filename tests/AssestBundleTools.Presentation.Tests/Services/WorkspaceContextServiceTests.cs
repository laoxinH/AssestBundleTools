using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class WorkspaceContextServiceTests
{
    [Fact]
    public void ReloadWorkspaceCatalogEntries_ShouldClearCurrentWorkspace_WhenCurrentEntryWasDeleted()
    {
        var catalogService = new TestWorkspaceContextCatalogService();
        var service = new WorkspaceContextService(catalogService);
        var workspaceSummary = CreateWorkspaceSummary("DeletedWorkspace");

        service.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceDataRootPath: @"C:\Tests\Data\DeletedWorkspace",
            assetBundleDecryptorId: null);

        Assert.NotNull(service.CurrentWorkspaceSummary);

        catalogService.NextCatalogState = new WorkspaceContextCatalogStateDto(
            LastActiveWorkspaceKey: null,
            WorkspaceEntries: []);

        service.ReloadWorkspaceCatalogEntries();

        Assert.Null(service.CurrentWorkspaceSummary);
    }

    private static WorkspaceSummaryDto CreateWorkspaceSummary(string displayName)
    {
        return new WorkspaceSummaryDto(
            WorkspaceId: WorkspaceId.New(),
            DisplayName: displayName,
            SourcePath: string.Empty,
            Mode: WorkspaceMode.SinglePane,
            ActivePaneId: PaneId.New(),
            BundleCount: 0,
            AssetCount: 0,
            OpenedAtUtc: DateTimeOffset.UtcNow);
    }

    private sealed class TestWorkspaceContextCatalogService : IWorkspaceContextCatalogService
    {
        public WorkspaceContextCatalogStateDto NextCatalogState { get; set; } =
            new(LastActiveWorkspaceKey: null, WorkspaceEntries: []);

        public WorkspaceContextCatalogStateDto LoadCatalogState(IReadOnlyList<WorkspaceContextLiveSessionDto> liveSessions) =>
            NextCatalogState;

        public WorkspaceCatalogSnapshotDto SaveCatalogState(
            IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries,
            WorkspaceSummaryDto? currentWorkspaceSummary)
        {
            return new WorkspaceCatalogSnapshotDto(
                Version: 1,
                LastActiveWorkspaceKey: workspaceEntries.FirstOrDefault()?.WorkspaceKey,
                RecentWorkspaces: []);
        }

        public WorkspaceBrowserStateSnapshotDto? LoadWorkspaceBrowserState(
            string workspaceKey,
            string? workspaceDataRootPath) =>
            null;

        public WorkspaceContextEntryRegistrationResultDto CreateRegisteredEntry(
            string workspaceKey,
            WorkspaceSummaryDto workspaceSummary,
            string? workspaceDataRootPath,
            string? assetBundleDecryptorId,
            WorkspaceContextEntryDto? existingEntry,
            DateTimeOffset activatedAtUtc)
        {
            var entry = new WorkspaceContextEntryDto(
                WorkspaceKey: workspaceKey,
                DisplayName: workspaceSummary.DisplayName,
                SourcePath: workspaceSummary.SourcePath,
                WorkspaceDataRootPath: workspaceDataRootPath ?? string.Empty,
                AssetBundleDecryptorId: assetBundleDecryptorId,
                WorkspaceMode: workspaceSummary.Mode,
                LastActivatedAtUtc: activatedAtUtc,
                IsPinned: existingEntry?.IsPinned ?? false,
                AvailabilityStatus: WorkspaceAvailabilityStatus.Available,
                LastKnownState: null,
                WorkspaceSummary: workspaceSummary);

            return new WorkspaceContextEntryRegistrationResultDto(entry, ExistingMetadata: null);
        }

        public WorkspaceContextEntryDto RefreshActivatedEntry(
            WorkspaceContextEntryDto existingEntry,
            DateTimeOffset activatedAtUtc) =>
            existingEntry with { LastActivatedAtUtc = activatedAtUtc };

        public WorkspaceContextRestoreRequestDto? ResolveLastActiveWorkspaceRestoreRequest(
            string? lastActiveWorkspaceKey,
            IReadOnlyList<WorkspaceContextEntryDto> workspaceEntries) =>
            null;

        public void SaveWorkspaceActivationMetadata(
            WorkspaceContextEntryDto workspaceEntry,
            DateTimeOffset openedAtUtc,
            WorkspaceMetadataDto? existingMetadata = null)
        {
        }

        public void SaveWorkspaceBrowserState(
            WorkspaceContextEntryDto workspaceEntry,
            WorkspaceSummaryDto workspaceSummary,
            WorkspaceBrowserStateSnapshotDto browserStateSnapshot)
        {
        }

        public IReadOnlyList<WorkspaceContextEntryDto> SortEntries(IEnumerable<WorkspaceContextEntryDto> workspaceEntries) =>
            workspaceEntries.ToArray();
    }
}
