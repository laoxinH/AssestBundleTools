using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceBatchExtractionBundleOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public WorkspaceBatchExtractionBundleOptionViewModel(
        BundleId bundleId,
        string displayName,
        string logicalPath,
        int assetCount,
        string assetCountText,
        bool isSelected)
    {
        BundleId = bundleId;
        DisplayName = displayName;
        LogicalPath = logicalPath;
        AssetCount = assetCount;
        AssetCountText = assetCountText;
        _isSelected = isSelected;
    }

    public event EventHandler? SelectionChanged;

    public BundleId BundleId { get; }

    public string DisplayName { get; }

    public string LogicalPath { get; }

    public int AssetCount { get; }

    public string AssetCountText { get; }

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
