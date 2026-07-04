using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportCompressionOptionViewModel : ObservableObject
{
    public WorkspaceExportCompressionOptionViewModel(
        WorkspaceBundleExportCompressionModeDto mode,
        string displayName,
        string description)
    {
        Mode = mode;
        DisplayName = displayName;
        Description = description;
    }

    public WorkspaceBundleExportCompressionModeDto Mode { get; }

    public string DisplayName { get; }

    public string Description { get; }
}
