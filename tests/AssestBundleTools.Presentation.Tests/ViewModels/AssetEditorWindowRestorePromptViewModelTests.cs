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
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-1", "Asset 1"),
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-2", "Asset 2"),
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

        var pendingWindow = Assert.Single(prompt.PendingWindows);
        Assert.Equal(2, pendingWindow.WindowStateId);
        Assert.Equal(2, pendingWindow.OpenAssetTabs.Count);
        Assert.True(prompt.HasPendingWindows);
        Assert.Equal(1, prompt.PendingWindowCount);
        Assert.Equal(2, prompt.PendingAssetTabCount);
        Assert.Equal(Visibility.Visible, prompt.PendingRestoreVisibility);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingButtonFormat}:1",
            prompt.RestoreButtonText);
        Assert.Equal(
            $"{ResourceKeys.AssetEditorWindowRestorePendingToolTipFormat}:1|2",
            prompt.RestoreToolTip);
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
        Assert.Equal(Visibility.Collapsed, prompt.PendingRestoreVisibility);
    }
}
