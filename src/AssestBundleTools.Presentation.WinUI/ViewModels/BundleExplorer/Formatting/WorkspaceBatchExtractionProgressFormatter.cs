using System.Globalization;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

/// <summary>
/// 将工作区级批量提取进度快照格式化为 Shell 状态条的短提示。
/// </summary>
public sealed class WorkspaceBatchExtractionProgressFormatter
{
    private readonly ILocalizationService _localizationService;

    public WorkspaceBatchExtractionProgressFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string Format(WorkspaceBatchExtractionProgressDto progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var memorySuffix = progress.IsMemoryThrottled
            ? GetString(ResourceKeys.WorkspaceBatchExtractionDialogProgressMemoryThrottledSuffix)
            : string.Empty;
        return progress.Kind switch
        {
            WorkspaceBatchExtractionProgressKind.Preparing => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchExtractionDialogProgressPreparingFormat,
                FormatCount(progress.TotalItemCount),
                FormatCount(progress.CandidateBundleCount)),
            WorkspaceBatchExtractionProgressKind.SpineGroup => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchExtractionDialogProgressSpineGroupFormat,
                NormalizeDisplayText(progress.BundleDisplayName),
                NormalizeDisplayText(progress.ItemDisplayName),
                FormatCount(progress.SucceededItemCount),
                FormatCount(progress.FailedItemCount),
                memorySuffix),
            _ => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceBatchExtractionDialogProgressAssetFormat,
                NormalizeDisplayText(progress.BundleDisplayName),
                NormalizeDisplayText(progress.ItemDisplayName),
                ResolveAssetTypeDisplayName(progress.AssetType, progress.AssetTypeDisplayName),
                FormatCount(progress.SucceededItemCount),
                FormatCount(progress.FailedItemCount),
                memorySuffix)
        };
    }

    private string ResolveAssetTypeDisplayName(AssetType? assetType, string? assetTypeDisplayName)
    {
        if (assetType == AssetType.Unknown && !string.IsNullOrWhiteSpace(assetTypeDisplayName))
        {
            return assetTypeDisplayName.Trim();
        }

        return assetType switch
        {
            AssetType.Texture2D => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryTexture2D),
            AssetType.Sprite => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategorySprite),
            AssetType.TextAsset => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryTextAsset),
            AssetType.AudioClip => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryAudioClip),
            AssetType.Mesh => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryMesh),
            AssetType.Material => GetString(ResourceKeys.WorkspaceBatchExtractionDialogCategoryMaterial),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };
    }

    private static string NormalizeDisplayText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string FormatCount(int value) =>
        Math.Max(0, value).ToString(CultureInfo.CurrentCulture);

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
