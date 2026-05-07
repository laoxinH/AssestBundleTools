using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示 Spine atlas 中可供预览切换的一张 page。
/// </summary>
public sealed class SpineAtlasPageOptionViewModel
{
    public SpineAtlasPageOptionViewModel(int index, SpineAtlasPageDto atlasPage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        AtlasPage = atlasPage ?? throw new ArgumentNullException(nameof(atlasPage));
        Index = index;
        DisplayName = ResolveDisplayName(index, atlasPage);
    }

    public int Index { get; }

    public SpineAtlasPageDto AtlasPage { get; }

    public string DisplayName { get; }

    private static string ResolveDisplayName(int index, SpineAtlasPageDto atlasPage)
    {
        return string.IsNullOrWhiteSpace(atlasPage.Name)
            ? $"#{index + 1}"
            : atlasPage.Name.Trim();
    }
}
