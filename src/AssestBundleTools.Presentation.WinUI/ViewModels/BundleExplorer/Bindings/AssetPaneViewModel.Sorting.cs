namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetPaneViewModel
{
    private const string NeutralSortGlyph = "\uE8CB";
    private const string AscendingSortGlyph = "\uE70E";
    private const string DescendingSortGlyph = "\uE70D";

    private AssetPaneSortColumn _assetSortColumn;
    private AssetPaneSortDirection _assetSortDirection = AssetPaneSortDirection.Ascending;

    public AssetPaneSortColumn AssetSortColumn => _assetSortColumn;

    public AssetPaneSortDirection AssetSortDirection => _assetSortDirection;

    public string NameSortGlyph => ResolveSortGlyph(AssetPaneSortColumn.Name);

    public string SizeSortGlyph => ResolveSortGlyph(AssetPaneSortColumn.Size);

    public double NameSortIndicatorOpacity => ResolveSortIndicatorOpacity(AssetPaneSortColumn.Name);

    public double SizeSortIndicatorOpacity => ResolveSortIndicatorOpacity(AssetPaneSortColumn.Size);

    public void ToggleAssetSort(AssetPaneSortColumn column)
    {
        if (column is not (AssetPaneSortColumn.Name or AssetPaneSortColumn.Size))
        {
            return;
        }

        if (_assetSortColumn == column)
        {
            _assetSortDirection = _assetSortDirection == AssetPaneSortDirection.Ascending
                ? AssetPaneSortDirection.Descending
                : AssetPaneSortDirection.Ascending;
        }
        else
        {
            _assetSortColumn = column;
            _assetSortDirection = AssetPaneSortDirection.Ascending;
        }

        _assetList.ApplySort(_assetSortColumn, _assetSortDirection);
        OnPropertyChanged(nameof(AssetSortColumn));
        OnPropertyChanged(nameof(AssetSortDirection));
        OnPropertyChanged(nameof(NameSortGlyph));
        OnPropertyChanged(nameof(SizeSortGlyph));
        OnPropertyChanged(nameof(NameSortIndicatorOpacity));
        OnPropertyChanged(nameof(SizeSortIndicatorOpacity));
    }

    private string ResolveSortGlyph(AssetPaneSortColumn column)
    {
        if (_assetSortColumn != column)
        {
            return NeutralSortGlyph;
        }

        return _assetSortDirection == AssetPaneSortDirection.Ascending
            ? AscendingSortGlyph
            : DescendingSortGlyph;
    }

    private double ResolveSortIndicatorOpacity(AssetPaneSortColumn column) =>
        _assetSortColumn == column ? 1d : 0.45d;
}
