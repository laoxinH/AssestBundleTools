using System.Globalization;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

public sealed class WorkspaceExportBundleOptionViewModel : ObservableObject
{
    private bool _isSelected = true;

    public WorkspaceExportBundleOptionViewModel(
        BundleId bundleId,
        string displayName,
        string outputPath,
        string summaryText,
        string statusText,
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
        StatusText = statusText;
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

    public string StatusText { get; }

    public int ChangeCount { get; }

    public int ModifiedAssetCount { get; }

    public int ExtractedAssetCount { get; }

    public int WorkspaceReplacementCount { get; }

    public int FileReplacementCount { get; }

    public DateTimeOffset? LastChangedAtUtc { get; }

    public bool IsModified => ChangeCount > 0;

    public Visibility ModifiedStatusVisibility => IsModified ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UnmodifiedStatusVisibility => IsModified ? Visibility.Collapsed : Visibility.Visible;

    public string LastChangedLocalTimeText =>
        LastChangedAtUtc is null
            ? string.Empty
            : LastChangedAtUtc.Value.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    public Visibility LastChangedLocalTimeVisibility =>
        LastChangedAtUtc is null ? Visibility.Collapsed : Visibility.Visible;

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
