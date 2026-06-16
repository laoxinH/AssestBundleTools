using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportBundleSortOptionViewModel : ObservableObject
{
    public WorkspaceExportBundleSortOptionViewModel(
        string id,
        string displayName)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Sort option id is required.", nameof(id)) : id;
        DisplayName = displayName;
    }

    public string Id { get; }

    public string DisplayName { get; }
}
