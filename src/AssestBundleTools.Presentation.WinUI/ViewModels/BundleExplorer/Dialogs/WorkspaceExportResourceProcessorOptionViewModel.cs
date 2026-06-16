using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportResourceProcessorOptionViewModel : ObservableObject
{
    private bool _isEnabled = true;
    private WorkspaceExportProcessorOptionViewModel? _selectedProcessor;

    public WorkspaceExportResourceProcessorOptionViewModel(
        string id,
        string displayName,
        string description,
        IEnumerable<WorkspaceExportProcessorOptionViewModel> processors)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Processor option id is required.", nameof(id)) : id;
        DisplayName = displayName;
        Description = description;
        Processors = new ObservableCollection<WorkspaceExportProcessorOptionViewModel>(
            processors ?? throw new ArgumentNullException(nameof(processors)));
        _selectedProcessor = Processors.FirstOrDefault();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public ObservableCollection<WorkspaceExportProcessorOptionViewModel> Processors { get; }

    public WorkspaceExportProcessorOptionViewModel? SelectedProcessor
    {
        get => _selectedProcessor;
        set => SetProperty(ref _selectedProcessor, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
