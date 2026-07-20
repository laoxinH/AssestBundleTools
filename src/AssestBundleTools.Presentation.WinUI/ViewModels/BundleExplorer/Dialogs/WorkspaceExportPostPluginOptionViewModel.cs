namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed record WorkspaceExportPostPluginOptionViewModel(
    string Id,
    string DisplayName,
    string Description,
    bool IsNone = false);
