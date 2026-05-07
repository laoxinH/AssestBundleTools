using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetEditorWindowDraftRestorePlanCoordinatorTests
{
    [Fact]
    public async Task RefreshAsync_ShouldMapPendingDraftReferencesToWorkspaceScopedPlan()
    {
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("DraftRestore");
        workspaceContextService.RegisterOrActivateWorkspace(
            PresentationTestDataFactory.CreateWorkspaceSummary(workspaceRequest),
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var prompt = new AssetEditorWindowRestorePromptViewModel(localizationService);
        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 10,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto(
                        "bundle-a",
                        "asset-1",
                        "Asset 1",
                        EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                            "draft-1",
                            "text",
                            "source-v1",
                            "Text draft")),
                    new WorkspaceAssetEditorWindowAssetTabDto(
                        "bundle-a",
                        "asset-2",
                        "Asset 2",
                        EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                            "",
                            "text"))
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-1")
        ]);
        var gateway = new TestAssetEditDraftRestorePlanGateway
        {
            GetRestorePlanHandler = query => Task.FromResult(Result<AssetEditDraftRestorePlanDto>.Success(
                new AssetEditDraftRestorePlanDto(
                    WorkspaceKey: query.WorkspaceKey,
                    TotalReferenceCount: query.DraftReferences.Count,
                    RestorableCount: 1,
                    MissingCount: 0,
                    SourceMismatchCount: 0,
                    ExpiredCount: 0,
                    InvalidReferenceCount: 1,
                    Candidates:
                    [
                        new AssetEditDraftRestoreCandidateDto(
                            query.DraftReferences[0],
                            AssetEditDraftRestoreStatus.Restorable),
                        new AssetEditDraftRestoreCandidateDto(
                            query.DraftReferences[1],
                            AssetEditDraftRestoreStatus.InvalidReference)
                    ])))
        };
        var coordinator = new AssetEditorWindowDraftRestorePlanCoordinator(
            gateway,
            workspaceContextService,
            prompt);

        await coordinator.RefreshAsync();

        var query = Assert.Single(gateway.Queries);
        var expectedWorkspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            workspaceRequest.SourcePath,
            workspaceRequest.WorkspaceDataRootPath);
        Assert.Equal(expectedWorkspaceKey, query.WorkspaceKey);
        Assert.Equal(2, query.DraftReferences.Count);
        Assert.Equal("draft-1", query.DraftReferences[0].DraftId);
        Assert.Equal("text", query.DraftReferences[0].DraftMode);
        Assert.Equal("source-v1", query.DraftReferences[0].SourceFingerprint);
        Assert.Equal("Text draft", query.DraftReferences[0].SourceDescription);
        Assert.Equal(string.Empty, query.DraftReferences[1].DraftId);
        Assert.Equal(1, prompt.DraftRestorableCount);
        Assert.Equal(1, prompt.DraftInvalidReferenceCount);
        Assert.True(prompt.HasDraftRestoreDiagnostics);
    }

    [Fact]
    public async Task RefreshAsync_ShouldClearPlan_WhenWorkspaceEntryIsUnavailable()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());
        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 10,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto(
                        "bundle-a",
                        "asset-1",
                        "Asset 1",
                        EditDraftReference: new WorkspaceAssetEditDraftReferenceDto("draft-1", "text"))
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-1")
        ]);
        prompt.ReplaceDraftRestorePlan(new AssetEditDraftRestorePlanDto(
            WorkspaceKey: "previous",
            TotalReferenceCount: 1,
            RestorableCount: 1,
            MissingCount: 0,
            SourceMismatchCount: 0,
            ExpiredCount: 0,
            InvalidReferenceCount: 0,
            Candidates: Array.Empty<AssetEditDraftRestoreCandidateDto>()));
        var gateway = new TestAssetEditDraftRestorePlanGateway();
        var coordinator = new AssetEditorWindowDraftRestorePlanCoordinator(
            gateway,
            new TestWorkspaceContextService(),
            prompt);

        await coordinator.RefreshAsync();

        Assert.Empty(gateway.Queries);
        Assert.False(prompt.HasDraftRestoreDiagnostics);
    }
}
