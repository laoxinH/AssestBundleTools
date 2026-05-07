using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetEditorWindowRestorePromptViewModelTests
{
    [Fact]
    public void ReplacePendingSnapshots_ShouldFilterInvalidWindowsAndUpdateDisplayState()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());

        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 2,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto(
                        "bundle-a",
                        "asset-1",
                        "Asset 1",
                        EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                            "draft-1",
                            "text")),
                    new WorkspaceAssetEditorWindowAssetTabDto(
                        "bundle-a",
                        "asset-2",
                        "Asset 2",
                        EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                            "",
                            "text")),
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-1", "Asset 1 Duplicate"),
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "", "Invalid")
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-1"),
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 3,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(160, 120, 1320, 820),
                OpenAssetTabs: Array.Empty<WorkspaceAssetEditorWindowAssetTabDto>(),
                ActiveBundleId: null,
                ActiveAssetId: null)
        ]);
        prompt.ReplacePotentialConflictAssetKeys(
        [
            AssetDocumentIdentityKey.Create("bundle-a", "asset-2")
        ]);

        var pendingWindow = Assert.Single(prompt.PendingWindows);
        Assert.Equal(2, pendingWindow.WindowStateId);
        Assert.Equal(2, pendingWindow.OpenAssetTabs.Count);
        Assert.True(prompt.HasPendingWindows);
        Assert.Equal(1, prompt.PendingWindowCount);
        Assert.Equal(2, prompt.PendingAssetTabCount);
        Assert.Equal(1, prompt.PendingDraftTabCount);
        Assert.True(prompt.HasDraftRestoreOption);
        Assert.Equal(Visibility.Visible, prompt.DraftRestoreOptionVisibility);
        Assert.True(prompt.RestoreDraftMarkers);
        Assert.Equal(
            ResourceKeys.AssetEditorWindowRestorePendingDialogRestoreDraftMarkersOption,
            prompt.RestoreDraftMarkersOptionText);
        Assert.Equal(1, prompt.PendingConflictAssetTabCount);
        Assert.Equal(Visibility.Visible, prompt.PendingRestoreVisibility);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingButtonFormat}:1",
            prompt.RestoreButtonText);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingToolTipWithConflictsFormat}:1|2|1",
            prompt.RestoreToolTip);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithDraftsAndConflictsFormat}:1|2|1|1",
            prompt.RestoreDialogDescription);

        prompt.ReplacePotentialConflictAssetKeys(null);

        Assert.Equal(0, prompt.PendingConflictAssetTabCount);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingToolTipFormat}:1|2",
            prompt.RestoreToolTip);

        prompt.SetRestoreDraftMarkers(false);

        Assert.False(prompt.RestoreDraftMarkers);
    }

    [Fact]
    public void RestoreDialogDescription_ShouldUseConflictText_WhenOnlyConflictsExist()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());
        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 5,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-1", "Asset 1")
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-1")
        ]);

        prompt.ReplacePotentialConflictAssetKeys(
        [
            AssetDocumentIdentityKey.Create("bundle-a", "asset-1")
        ]);

        Assert.Equal(1, prompt.PendingConflictAssetTabCount);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithConflictsFormat}:1|1|1",
            prompt.RestoreDialogDescription);
    }

    [Fact]
    public void ReplacePendingSnapshots_ShouldKeepSpineOnlyWindows()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());
        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 8,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs: Array.Empty<WorkspaceAssetEditorWindowAssetTabDto>(),
                ActiveBundleId: "bundle-a",
                ActiveAssetId: null,
                OpenDocumentTabs:
                [
                    new WorkspaceAssetEditorWindowDocumentTabDto(
                        WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                        SpineGroupTab: new WorkspaceAssetEditorWindowSpineGroupTabDto(
                            "bundle-a",
                            "spine-group:bundle-a:hero",
                            "hero",
                            "hero"))
                ],
                ActiveDocumentKind: WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                ActiveSpineGroupId: "spine-group:bundle-a:hero")
        ]);

        prompt.ReplacePotentialConflictDocumentKeys(
            assetKeys: null,
            spineGroupKeys:
            [
                SpineGroupDocumentIdentityKey.Create("bundle-a", "spine-group:bundle-a:hero")
            ]);

        var pendingWindow = Assert.Single(prompt.PendingWindows);

        Assert.Empty(pendingWindow.OpenAssetTabs);
        Assert.Single(pendingWindow.OpenDocumentTabs ?? []);
        Assert.Equal(1, prompt.PendingAssetTabCount);
        Assert.Equal(1, prompt.PendingConflictAssetTabCount);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithConflictsFormat}:1|1|1",
            prompt.RestoreDialogDescription);
    }

    [Fact]
    public void TakePendingSnapshots_ShouldClearPromptState()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());
        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 4,
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
        prompt.SetRestoreDraftMarkers(false);

        var takenSnapshots = prompt.TakePendingSnapshots();

        Assert.Single(takenSnapshots);
        Assert.Empty(prompt.PendingWindows);
        Assert.False(prompt.HasPendingWindows);
        Assert.Equal(0, prompt.PendingWindowCount);
        Assert.Equal(0, prompt.PendingAssetTabCount);
        Assert.Equal(0, prompt.PendingDraftTabCount);
        Assert.False(prompt.HasDraftRestoreOption);
        Assert.Equal(Visibility.Collapsed, prompt.DraftRestoreOptionVisibility);
        Assert.Equal(0, prompt.PendingConflictAssetTabCount);
        Assert.Equal(Visibility.Collapsed, prompt.PendingRestoreVisibility);
        Assert.True(prompt.RestoreDraftMarkers);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionFormat}:0|0",
            prompt.RestoreDialogDescription);
    }

    [Fact]
    public void ReplacePendingSnapshots_ShouldResetDraftMarkerOption_WhenNoDraftsRemain()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());
        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 6,
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
        prompt.SetRestoreDraftMarkers(false);

        prompt.ReplacePendingSnapshots(
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 7,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-2", "Asset 2")
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-2")
        ]);

        Assert.False(prompt.HasDraftRestoreOption);
        Assert.True(prompt.RestoreDraftMarkers);
    }

    [Fact]
    public void ReplaceDraftRestorePlan_ShouldUpdateDraftDiagnostics()
    {
        var prompt = new AssetEditorWindowRestorePromptViewModel(new TestLocalizationService());

        prompt.ReplaceDraftRestorePlan(new AssetEditDraftRestorePlanDto(
            WorkspaceKey: "workspace-a",
            TotalReferenceCount: 5,
            RestorableCount: 1,
            MissingCount: 2,
            SourceMismatchCount: 1,
            ExpiredCount: 1,
            InvalidReferenceCount: 0,
            Candidates:
            [
                new AssetEditDraftRestoreCandidateDto(
                    new AssetEditDraftReferenceDto(" draft-1 ", " text ", " source-v1 "),
                    AssetEditDraftRestoreStatus.Restorable),
                new AssetEditDraftRestoreCandidateDto(
                    new AssetEditDraftReferenceDto("draft-2", "text", "source-v2"),
                    AssetEditDraftRestoreStatus.Missing)
            ]));

        Assert.Equal(5, prompt.DraftRestoreReferenceCount);
        Assert.Equal(1, prompt.DraftRestorableCount);
        Assert.Equal(2, prompt.DraftMissingCount);
        Assert.Equal(1, prompt.DraftSourceMismatchCount);
        Assert.Equal(1, prompt.DraftExpiredCount);
        Assert.Equal(0, prompt.DraftInvalidReferenceCount);
        Assert.True(prompt.HasDraftRestoreDiagnostics);
        Assert.Equal(Visibility.Visible, prompt.DraftRestoreDiagnosticsVisibility);
        Assert.Equal("5:1:2:1:1:0", prompt.DraftRestoreDiagnosticsKey);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDraftDiagnosticsFormat}:1|2|1|1|0",
            prompt.DraftRestoreDiagnosticsText);
        var restorableDraftReferenceKeys = prompt.CreateRestorableDraftReferenceKeys()
            ?? new HashSet<AssetEditDraftReferenceKey>();
        var restorableDraftReferenceKey = Assert.Single(restorableDraftReferenceKeys);
        Assert.Equal(new AssetEditDraftReferenceKey("draft-1", "text", "source-v1"), restorableDraftReferenceKey);

        prompt.ClearDraftRestorePlan();

        Assert.False(prompt.HasDraftRestoreDiagnostics);
        Assert.Equal(Visibility.Collapsed, prompt.DraftRestoreDiagnosticsVisibility);
        Assert.Equal(string.Empty, prompt.DraftRestoreDiagnosticsText);
        Assert.Equal("0:0:0:0:0:0", prompt.DraftRestoreDiagnosticsKey);
    }
}
