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
        Assert.Equal(1, prompt.PendingConflictAssetTabCount);
        Assert.Equal(Visibility.Visible, prompt.PendingRestoreVisibility);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingButtonFormat}:1",
            prompt.RestoreButtonText);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingToolTipFormat}:1|2",
            prompt.RestoreToolTip);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionWithDraftsAndConflictsFormat}:1|2|1|1",
            prompt.RestoreDialogDescription);
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
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-1", "Asset 1")
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-1")
        ]);

        var takenSnapshots = prompt.TakePendingSnapshots();

        Assert.Single(takenSnapshots);
        Assert.Empty(prompt.PendingWindows);
        Assert.False(prompt.HasPendingWindows);
        Assert.Equal(0, prompt.PendingWindowCount);
        Assert.Equal(0, prompt.PendingAssetTabCount);
        Assert.Equal(0, prompt.PendingDraftTabCount);
        Assert.Equal(0, prompt.PendingConflictAssetTabCount);
        Assert.Equal(Visibility.Collapsed, prompt.PendingRestoreVisibility);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingDialogDescriptionFormat}:0|0",
            prompt.RestoreDialogDescription);
    }
}
