namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 资产文档组在不同宿主之间移动时携带的页面级标签状态。
/// </summary>
public sealed class AssetDocumentGroupTransfer
{
    public AssetDocumentGroupTransfer(
        IReadOnlyList<AssetDocumentTabViewModel> assetTabs,
        AssetDocumentTabViewModel? activeAssetTab)
        : this(CreateTransferParts(assetTabs, activeAssetTab))
    {
    }

    private AssetDocumentGroupTransfer(TransferParts parts)
        : this(parts.DocumentTabs, parts.ActiveDocumentTab)
    {
    }

    internal AssetDocumentGroupTransfer(
        IReadOnlyList<AssetDocumentTabItemViewModel> documentTabs,
        AssetDocumentTabItemViewModel? activeDocumentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTabs);
        if (documentTabs.Count == 0)
        {
            throw new ArgumentException("The asset document group must contain at least one tab.", nameof(documentTabs));
        }

        DocumentTabs = documentTabs;
        AssetTabs = documentTabs
            .Select(static tab => tab.AssetTab)
            .Where(static tab => tab is not null)
            .Cast<AssetDocumentTabViewModel>()
            .ToArray();
        SpineGroupTabs = documentTabs
            .Select(static tab => tab.SpineGroupTab)
            .Where(static tab => tab is not null)
            .Cast<SpineAssetGroupDocumentTabViewModel>()
            .ToArray();
        ActiveDocumentTab = activeDocumentTab is not null && documentTabs.Contains(activeDocumentTab)
            ? activeDocumentTab
            : documentTabs[0];
        ActiveAssetTab = ActiveDocumentTab.AssetTab ?? AssetTabs.FirstOrDefault();
        ActiveSpineGroupTab = ActiveDocumentTab.SpineGroupTab ?? SpineGroupTabs.FirstOrDefault();
    }

    public IReadOnlyList<AssetDocumentTabItemViewModel> DocumentTabs { get; }

    public IReadOnlyList<AssetDocumentTabViewModel> AssetTabs { get; }

    public IReadOnlyList<SpineAssetGroupDocumentTabViewModel> SpineGroupTabs { get; }

    public AssetDocumentTabItemViewModel ActiveDocumentTab { get; }

    public AssetDocumentTabViewModel? ActiveAssetTab { get; }

    public SpineAssetGroupDocumentTabViewModel? ActiveSpineGroupTab { get; }

    private static TransferParts CreateTransferParts(
        IReadOnlyList<AssetDocumentTabViewModel> assetTabs,
        AssetDocumentTabViewModel? activeAssetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTabs);
        var documentTabs = assetTabs
            .Select(AssetDocumentTabItemViewModel.FromAsset)
            .ToArray();
        var activeDocumentTab = activeAssetTab is null
            ? null
            : documentTabs.FirstOrDefault(tab => ReferenceEquals(tab.AssetTab, activeAssetTab));
        return new TransferParts(documentTabs, activeDocumentTab);
    }

    private sealed record TransferParts(
        IReadOnlyList<AssetDocumentTabItemViewModel> DocumentTabs,
        AssetDocumentTabItemViewModel? ActiveDocumentTab);
}
