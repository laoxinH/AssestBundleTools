using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetPaneViewModel
{
    /// <summary>
    /// 按资源标识恢复主选中项。
    /// 如果目标资源已经不在当前筛选结果中，则保持空选中。
    /// 避免程序自动把焦点跳到筛选结果第一项。
    /// </summary>
    public bool TrySelectAsset(AssetId assetId)
    {
        if (!_assetList.TrySelectAsset(assetId))
        {
            return false;
        }

        SetAssetMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
        return true;
    }

    public bool ApplyAssetOperationState(
        AssetDetailsDto details,
        string stateText)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (!_assetList.TryGetSourceAsset(details.AssetId, out var assetEntry) ||
            assetEntry is null)
        {
            return false;
        }

        assetEntry.ApplyOperationState(
            details.IsModified,
            details.LastExtractedAtUtc,
            stateText);

        if (ShowModifiedOnly || ShowPreviewableOnly)
        {
            ApplyLocalFilters(details.AssetId);
        }
        else
        {
            NotifySelectionStateChanged();
            NotifyListStateChanged();
        }

        return true;
    }

    public bool TrySelectSpineGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return false;
        }

        var target = SpineGroupItems.FirstOrDefault(item =>
            string.Equals(item.Item.GroupId, groupId, StringComparison.Ordinal));
        if (target is null)
        {
            return false;
        }

        _spineGroupList.SetSelectedSpineGroup(target);
        SetSpineGroupMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedSpineGroup));
        NotifySelectionStateChanged();
        return true;
    }
}
