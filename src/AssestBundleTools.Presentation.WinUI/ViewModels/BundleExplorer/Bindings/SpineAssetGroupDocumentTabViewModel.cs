using System.Globalization;
using System.Collections.ObjectModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示右侧详情区域中的单个 Spine 资源组文档。
/// </summary>
public sealed class SpineAssetGroupDocumentTabViewModel : ObservableObject
{
    private SpineAssetGroupDto _item;
    private SpineAssetGroupDetailsDto? _details;
    private SpineAssetGroupTexturePreviewState? _texturePreview;
    private SpineAtlasPageOptionViewModel? _selectedAtlasPageOption;
    private string _displayName;
    private string _toolTipText;

    public SpineAssetGroupDocumentTabViewModel(
        SpineAssetGroupDto item,
        InspectorInformationTextSet inspectorInformationTextSet)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _displayName = ResolveDisplayName(item);
        _toolTipText = CreateToolTipText(item);
        Inspector = new InspectorPanelViewModel(
            inspectorInformationTextSet ?? throw new ArgumentNullException(nameof(inspectorInformationTextSet)));
    }

    public SpineAssetGroupDto Item
    {
        get => _item;
        private set => SetProperty(ref _item, value);
    }

    public BundleId BundleId => Item.BundleId;

    public string GroupId => Item.GroupId;

    public string DocumentId => GroupId;

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string ToolTipText
    {
        get => _toolTipText;
        private set => SetProperty(ref _toolTipText, value);
    }

    public SpineAssetGroupDetailsDto? Details
    {
        get => _details;
        private set => SetProperty(ref _details, value);
    }

    internal SpineAssetGroupTexturePreviewState? TexturePreview
    {
        get => _texturePreview;
        private set => SetProperty(ref _texturePreview, value);
    }

    public ObservableCollection<SpineAtlasPageOptionViewModel> AtlasPageOptions { get; } = new();

    public SpineAtlasPageOptionViewModel? SelectedAtlasPageOption
    {
        get => _selectedAtlasPageOption;
        private set => SetProperty(ref _selectedAtlasPageOption, value);
    }

    public InspectorPanelViewModel Inspector { get; }

    public void ApplyGroup(SpineAssetGroupDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Details = null;
        TexturePreview = null;
        ClearAtlasPageOptions();
        Item = item;
        DisplayName = ResolveDisplayName(item);
        ToolTipText = CreateToolTipText(item);
    }

    internal void ApplyDetails(
        SpineAssetGroupDetailsDto details,
        SpineAssetGroupTexturePreviewState? texturePreview = null)
    {
        ArgumentNullException.ThrowIfNull(details);

        Item = details.Group;
        Details = details;
        ReplaceAtlasPageOptions(details, texturePreview?.AtlasPage);
        TexturePreview = texturePreview;
        DisplayName = ResolveDisplayName(details.Group);
        ToolTipText = CreateToolTipText(details.Group);
    }

    internal bool SelectAtlasPageOption(SpineAtlasPageOptionViewModel option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (!AtlasPageOptions.Contains(option) ||
            ReferenceEquals(SelectedAtlasPageOption, option))
        {
            return false;
        }

        SelectedAtlasPageOption = option;
        TexturePreview = null;
        return true;
    }

    internal void ApplyTexturePreview(SpineAssetGroupTexturePreviewState? texturePreview)
    {
        if (texturePreview is not null &&
            TryFindAtlasPageOption(texturePreview.AtlasPage, out var atlasPageOption))
        {
            SelectedAtlasPageOption = atlasPageOption;
        }

        TexturePreview = texturePreview;
    }

    private void ReplaceAtlasPageOptions(
        SpineAssetGroupDetailsDto details,
        SpineAtlasPageDto? preferredAtlasPage)
    {
        var previousSelectedIndex = SelectedAtlasPageOption?.Index;
        AtlasPageOptions.Clear();
        for (var index = 0; index < details.AtlasPages.Count; index++)
        {
            AtlasPageOptions.Add(new SpineAtlasPageOptionViewModel(index, details.AtlasPages[index]));
        }

        SelectedAtlasPageOption = ResolveSelectedAtlasPageOption(
            preferredAtlasPage,
            previousSelectedIndex);
    }

    private SpineAtlasPageOptionViewModel? ResolveSelectedAtlasPageOption(
        SpineAtlasPageDto? preferredAtlasPage,
        int? previousSelectedIndex)
    {
        if (preferredAtlasPage is not null &&
            TryFindAtlasPageOption(preferredAtlasPage, out var preferredOption))
        {
            return preferredOption;
        }

        if (previousSelectedIndex is >= 0 &&
            previousSelectedIndex.Value < AtlasPageOptions.Count)
        {
            return AtlasPageOptions[previousSelectedIndex.Value];
        }

        return AtlasPageOptions.FirstOrDefault(static option =>
                   option.AtlasPage.TexturePageAsset is not null) ??
               AtlasPageOptions.FirstOrDefault();
    }

    private bool TryFindAtlasPageOption(
        SpineAtlasPageDto atlasPage,
        out SpineAtlasPageOptionViewModel option)
    {
        option = AtlasPageOptions.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.AtlasPage, atlasPage) ||
            string.Equals(candidate.AtlasPage.Name, atlasPage.Name, StringComparison.Ordinal))!;
        return option is not null;
    }

    private void ClearAtlasPageOptions()
    {
        AtlasPageOptions.Clear();
        SelectedAtlasPageOption = null;
    }

    private static string ResolveDisplayName(SpineAssetGroupDto item)
    {
        return string.IsNullOrWhiteSpace(item.DisplayName)
            ? item.GroupId
            : item.DisplayName.Trim();
    }

    private static string CreateToolTipText(SpineAssetGroupDto item)
    {
        var groupKey = string.IsNullOrWhiteSpace(item.GroupKey)
            ? item.GroupId
            : item.GroupKey.Trim();
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} [{1}]",
            groupKey,
            item.GroupId);
    }
}
