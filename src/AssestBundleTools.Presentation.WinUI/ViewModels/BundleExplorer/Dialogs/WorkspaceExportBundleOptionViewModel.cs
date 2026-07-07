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
        int changeCount,
        int modifiedAssetCount,
        int extractedAssetCount,
        int workspaceReplacementCount,
        int fileReplacementCount,
        DateTimeOffset? lastChangedAtUtc,
        bool isSelected = true)
    {
        BundleId = bundleId;
        DisplayName = displayName;
        OutputPath = outputPath;
        SummaryText = summaryText;
        ChangeCount = changeCount;
        ModifiedAssetCount = modifiedAssetCount;
        ExtractedAssetCount = extractedAssetCount;
        WorkspaceReplacementCount = workspaceReplacementCount;
        FileReplacementCount = fileReplacementCount;
        LastChangedAtUtc = lastChangedAtUtc;
        _isSelected = isSelected;
    }

    public event EventHandler? SelectionChanged;

    public BundleId BundleId { get; }

    public string DisplayName { get; }

    public string OutputPath { get; }

    public string SummaryText { get; }

    public int ChangeCount { get; }

    public int ModifiedAssetCount { get; }

    public int ExtractedAssetCount { get; }

    public int WorkspaceReplacementCount { get; }

    public int FileReplacementCount { get; }

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
