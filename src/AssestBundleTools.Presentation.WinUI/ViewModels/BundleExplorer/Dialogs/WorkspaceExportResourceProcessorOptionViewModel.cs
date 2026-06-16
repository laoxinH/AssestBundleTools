using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportResourceProcessorOptionViewModel : ObservableObject
{
    private bool _isEnabled = true;

    public WorkspaceExportResourceProcessorOptionViewModel(
        string id,
        string displayName,
        string description)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Processor option id is required.", nameof(id)) : id;
        DisplayName = displayName;
        Description = description;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
