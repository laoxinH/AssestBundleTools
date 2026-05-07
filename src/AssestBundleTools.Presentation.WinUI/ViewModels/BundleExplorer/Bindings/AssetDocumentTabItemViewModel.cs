using System.ComponentModel;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 将普通资源文档和 Spine 组文档投影为同一个标签栏可消费的文档项。
/// </summary>
public sealed class AssetDocumentTabItemViewModel : ObservableObject, IDisposable
{
    private AssetDocumentTabItemViewModel(
        AssetDocumentTabKind kind,
        AssetDocumentTabViewModel? assetTab,
        SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        Kind = kind;
        AssetTab = assetTab;
        SpineGroupTab = spineGroupTab;

        if (AssetTab is not null)
        {
            AssetTab.PropertyChanged += OnInnerTabPropertyChanged;
        }

        if (SpineGroupTab is not null)
        {
            SpineGroupTab.PropertyChanged += OnInnerTabPropertyChanged;
        }
    }

    public AssetDocumentTabKind Kind { get; }

    public AssetDocumentTabViewModel? AssetTab { get; }

    public SpineAssetGroupDocumentTabViewModel? SpineGroupTab { get; }

    public BundleId BundleId =>
        AssetTab?.BundleId ??
        SpineGroupTab?.BundleId ??
        throw new InvalidOperationException("Document tab item has no backing document.");

    public string DocumentId => AssetTab?.DocumentId ?? SpineGroupTab?.DocumentId ?? string.Empty;

    public string DisplayName => AssetTab?.DisplayName ?? SpineGroupTab?.DisplayName ?? string.Empty;

    public string ToolTipText => AssetTab?.ToolTipText ?? SpineGroupTab?.ToolTipText ?? string.Empty;

    public InspectorPanelViewModel Inspector =>
        AssetTab?.Inspector ??
        SpineGroupTab?.Inspector ??
        throw new InvalidOperationException("Document tab item has no inspector.");

    public bool IsAsset => Kind == AssetDocumentTabKind.Asset;

    public bool IsSpineGroup => Kind == AssetDocumentTabKind.SpineGroup;

    public Visibility SpineMarkerVisibility => IsSpineGroup ? Visibility.Visible : Visibility.Collapsed;

    public static AssetDocumentTabItemViewModel FromAsset(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        return new AssetDocumentTabItemViewModel(AssetDocumentTabKind.Asset, assetTab, null);
    }

    public static AssetDocumentTabItemViewModel FromSpineGroup(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);
        return new AssetDocumentTabItemViewModel(AssetDocumentTabKind.SpineGroup, null, spineGroupTab);
    }

    public bool Wraps(AssetDocumentTabViewModel assetTab) =>
        ReferenceEquals(AssetTab, assetTab);

    public bool Wraps(SpineAssetGroupDocumentTabViewModel spineGroupTab) =>
        ReferenceEquals(SpineGroupTab, spineGroupTab);

    public void Dispose()
    {
        if (AssetTab is not null)
        {
            AssetTab.PropertyChanged -= OnInnerTabPropertyChanged;
        }

        if (SpineGroupTab is not null)
        {
            SpineGroupTab.PropertyChanged -= OnInnerTabPropertyChanged;
        }
    }

    private void OnInnerTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetDocumentTabViewModel.DisplayName) or
            nameof(SpineAssetGroupDocumentTabViewModel.DisplayName))
        {
            OnPropertyChanged(nameof(DisplayName));
        }

        if (e.PropertyName is nameof(AssetDocumentTabViewModel.ToolTipText) or
            nameof(SpineAssetGroupDocumentTabViewModel.ToolTipText))
        {
            OnPropertyChanged(nameof(ToolTipText));
        }
    }
}
