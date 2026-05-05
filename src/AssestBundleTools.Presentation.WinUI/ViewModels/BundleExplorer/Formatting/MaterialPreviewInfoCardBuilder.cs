using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class MaterialPreviewInfoCardBuilder
{
    private const string ShaderCardKey = "material-shader";
    private const string AppearanceCardKey = "material-appearance";
    private const string SemanticHintsCardKey = "material-semantic-hints";
    private const string TexturesCardKey = "material-textures";
    private const string ColorsCardKey = "material-colors";
    private const string NumbersCardKey = "material-numbers";

    private readonly ILocalizationService _localizationService;

    public MaterialPreviewInfoCardBuilder(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public IReadOnlyList<InspectorInfoCardViewModel> Build(
        string shaderNameText,
        string shaderReferenceText,
        string shaderResolutionText,
        AssetDependencyDto? resolvedShaderDependency,
        MaterialPreviewAppearanceDisplayState? appearance,
        IReadOnlyList<MaterialPreviewSemanticBadgeDisplayState> semanticBadges,
        IReadOnlyList<MaterialPreviewTextureRowDisplayState> textureRows,
        IReadOnlyList<MaterialPreviewColorRowDisplayState> colorRows,
        IReadOnlyList<MaterialPreviewNumberRowDisplayState> numberRows,
        string truncatedText,
        string openButtonText,
        string openButtonToolTipText,
        MaterialPreviewTintDisplayState? mainTextureTint,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources)
    {
        var cards = new List<InspectorInfoCardViewModel>
        {
            CreateCard(
                ShaderCardKey,
                ResourceKeys.InspectorMaterialPreviewShaderCardTitle,
                CreateShaderRows(
                    shaderNameText,
                    shaderReferenceText,
                    shaderResolutionText,
                    resolvedShaderDependency,
                    openButtonText,
                    openButtonToolTipText),
                isExpanded: true)
        };

        if (appearance is not null)
        {
            cards.Add(CreateCard(
                AppearanceCardKey,
                ResourceKeys.InspectorMaterialPreviewAppearanceTitle,
                Array.Empty<InspectorInfoRowViewModel>(),
                isExpanded: false,
                visual: InspectorInfoCardVisualViewModel.CreateMaterialAppearance(appearance),
                summaryItemCount: 5));
        }

        if (semanticBadges.Count > 0 || !string.IsNullOrWhiteSpace(truncatedText))
        {
            cards.Add(CreateCard(
                SemanticHintsCardKey,
                ResourceKeys.InspectorMaterialPreviewSemanticHintsTitle,
                CreateSemanticRows(semanticBadges, truncatedText),
                isExpanded: false));
        }

        if (textureRows.Count > 0)
        {
            cards.Add(CreateCard(
                TexturesCardKey,
                ResourceKeys.InspectorMaterialPreviewTexturesTitle,
                CreateTextureRows(textureRows, textureThumbnailSources),
                isExpanded: true,
                visual: CreateMainTextureVisual(textureRows, mainTextureTint, textureThumbnailSources)));
        }

        if (colorRows.Count > 0)
        {
            cards.Add(CreateCard(
                ColorsCardKey,
                ResourceKeys.InspectorMaterialPreviewColorsTitle,
                CreateColorRows(colorRows),
                isExpanded: false));
        }

        if (numberRows.Count > 0)
        {
            cards.Add(CreateCard(
                NumbersCardKey,
                ResourceKeys.InspectorMaterialPreviewNumbersTitle,
                numberRows
                    .Select(static row => new InspectorInfoRowViewModel(row.PropertyName, row.ValueText))
                    .ToArray(),
                isExpanded: false));
        }

        return cards;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateShaderRows(
        string shaderNameText,
        string shaderReferenceText,
        string shaderResolutionText,
        AssetDependencyDto? resolvedShaderDependency,
        string openButtonText,
        string openButtonToolTipText)
    {
        var rows = new List<InspectorInfoRowViewModel>
        {
            new(GetString(ResourceKeys.InspectorMaterialPreviewShaderNameLabel), shaderNameText)
        };

        if (!string.IsNullOrWhiteSpace(shaderReferenceText))
        {
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorMaterialPreviewShaderReferenceLabel),
                shaderReferenceText));
        }

        if (!string.IsNullOrWhiteSpace(shaderResolutionText))
        {
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorMaterialPreviewShaderResolutionLabel),
                shaderResolutionText,
                resolvedShaderDependency is null ? null : CreateNavigationTarget(resolvedShaderDependency),
                openButtonText,
                openButtonToolTipText));
        }

        return rows;
    }

    private static IReadOnlyList<InspectorInfoRowViewModel> CreateSemanticRows(
        IReadOnlyList<MaterialPreviewSemanticBadgeDisplayState> semanticBadges,
        string truncatedText)
    {
        var rows = semanticBadges
            .Select(static badge => new InspectorInfoRowViewModel(string.Empty, badge.Text))
            .ToList();

        if (!string.IsNullOrWhiteSpace(truncatedText))
        {
            rows.Add(new InspectorInfoRowViewModel(string.Empty, truncatedText));
        }

        return rows;
    }

    private static IReadOnlyList<InspectorInfoRowViewModel> CreateTextureRows(
        IReadOnlyList<MaterialPreviewTextureRowDisplayState> textureRows,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources)
    {
        return textureRows
            .Select(row => new InspectorInfoRowViewModel(
                row.PropertyName,
                JoinNonEmpty(row.ResolutionText, row.TargetText, row.TransformText),
                row.Target,
                row.OpenButtonText,
                row.OpenButtonToolTipText,
                ResolveThumbnailSource(row, textureThumbnailSources),
                showThumbnailSlot: true))
            .ToArray();
    }

    private static IReadOnlyList<InspectorInfoRowViewModel> CreateColorRows(
        IReadOnlyList<MaterialPreviewColorRowDisplayState> colorRows)
    {
        return colorRows
            .Select(static row => new InspectorInfoRowViewModel(
                row.PropertyName,
                row.ValueText,
                colorSwatchColor: CreateColorSwatchColor(row)))
            .ToArray();
    }

    private static InspectorInfoCardVisualViewModel? CreateMainTextureVisual(
        IReadOnlyList<MaterialPreviewTextureRowDisplayState> textureRows,
        MaterialPreviewTintDisplayState? mainTextureTint,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources)
    {
        var mainTextureRow = textureRows.FirstOrDefault(row =>
                                 row.IsMainTextureCandidate &&
                                 ResolveThumbnailSource(row, textureThumbnailSources) is not null)
                             ?? textureRows.FirstOrDefault(row => ResolveThumbnailSource(row, textureThumbnailSources) is not null);

        var imageSource = mainTextureRow is null
            ? null
            : ResolveThumbnailSource(mainTextureRow, textureThumbnailSources);
        if (imageSource is null)
        {
            return null;
        }

        return InspectorInfoCardVisualViewModel.CreateImagePreview(
            imageSource,
            mainTextureTint is null ? null : CreateTintColor(mainTextureTint));
    }

    private InspectorInfoCardViewModel CreateCard(
        string key,
        string titleResourceKey,
        IReadOnlyList<InspectorInfoRowViewModel> rows,
        bool isExpanded,
        InspectorInfoCardVisualViewModel? visual = null,
        int? summaryItemCount = null)
    {
        return new InspectorInfoCardViewModel(
            key,
            GetString(titleResourceKey),
            GetString(ResourceKeys.InspectorInfoCardCopyToolTip),
            _localizationService.GetFormattedString(
                ResourceKeys.InspectorInfoCardItemCountFormat,
                (summaryItemCount ?? rows.Count).ToString(CultureInfo.CurrentCulture)),
            isExpanded,
            rows,
            visual);
    }

    private static string JoinNonEmpty(params string[] values)
    {
        return string.Join(
            " · ",
            values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()));
    }

    private static string ResolveAssetTypeDisplayName(AssetDependencyDto dependency)
    {
        if (!string.IsNullOrWhiteSpace(dependency.AssetTypeDisplayName))
        {
            return dependency.AssetTypeDisplayName.Trim();
        }

        return dependency.AssetType.ToString();
    }

    private static ImageSource? ResolveThumbnailSource(
        MaterialPreviewTextureRowDisplayState row,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources)
    {
        return !string.IsNullOrWhiteSpace(row.ResolvedTextureAssetId) &&
               textureThumbnailSources is not null &&
               textureThumbnailSources.TryGetValue(row.ResolvedTextureAssetId, out var thumbnailSource)
            ? thumbnailSource
            : null;
    }

    private static Color CreateColorSwatchColor(MaterialPreviewColorRowDisplayState row)
    {
        return Color.FromArgb(
            ToByte(row.A),
            ToByte(row.R),
            ToByte(row.G),
            ToByte(row.B));
    }

    private static Color CreateTintColor(MaterialPreviewTintDisplayState tint)
    {
        return Color.FromArgb(
            ToByte(tint.A),
            ToByte(tint.R),
            ToByte(tint.G),
            ToByte(tint.B));
    }

    private static byte ToByte(double value)
    {
        var clamped = Math.Clamp(value, 0d, 1d);
        return (byte)Math.Round(clamped * byte.MaxValue);
    }

    private static ResolvedAssetNavigationTarget CreateNavigationTarget(AssetDependencyDto dependency)
    {
        return new ResolvedAssetNavigationTarget(
            dependency.AssetId,
            dependency.BundleId,
            dependency.PathId,
            dependency.Name,
            ResolveAssetTypeDisplayName(dependency));
    }

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);
}
