using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示资产列表中用于展示的 Spine 资源组行。
/// </summary>
public sealed class SpineAssetGroupEntryViewModel : ObservableObject
{
    private bool _isSelected;

    public SpineAssetGroupEntryViewModel(
        SpineAssetGroupDto item,
        string partsText,
        string texturePagesText,
        string stateText)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        PartsText = partsText;
        TexturePagesText = texturePagesText;
        StateText = stateText;
    }

    public SpineAssetGroupDto Item { get; }

    public string DisplayName => Item.DisplayName;

    public string DisplayGroupKey => Item.GroupKey;

    public string DisplayNameToolTipText => Item.GroupKey;

    public string PartsText { get; }

    public string TexturePagesText { get; }

    public string StateText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(SelectionIndicatorVisibility));
                OnPropertyChanged(nameof(SelectionBackgroundOpacity));
            }
        }
    }

    public Visibility SelectionIndicatorVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public double SelectionBackgroundOpacity => IsSelected ? 1d : 0d;
}
