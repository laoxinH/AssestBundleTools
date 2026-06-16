using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportBundleOptionViewModel : ObservableObject
{
    private bool _isSelected = true;

    public WorkspaceExportBundleOptionViewModel(
        BundleId bundleId,
        string displayName,
        string outputPath,
        string summaryText,
        DateTimeOffset? lastChangedAtUtc)
    {
        BundleId = bundleId;
        DisplayName = displayName;
        OutputPath = outputPath;
        SummaryText = summaryText;
        LastChangedAtUtc = lastChangedAtUtc;
    }

    public event EventHandler? SelectionChanged;

    public BundleId BundleId { get; }

    public string DisplayName { get; }

    public string OutputPath { get; }

    public string SummaryText { get; }

    public DateTimeOffset? LastChangedAtUtc { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
