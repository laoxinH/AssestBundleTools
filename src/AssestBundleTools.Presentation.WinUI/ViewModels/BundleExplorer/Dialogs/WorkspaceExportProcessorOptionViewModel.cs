using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportProcessorOptionViewModel : ObservableObject
{
    public WorkspaceExportProcessorOptionViewModel(
        string id,
        string displayName,
        string description)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Processor id is required.", nameof(id)) : id;
        DisplayName = displayName;
        Description = description;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }
}
