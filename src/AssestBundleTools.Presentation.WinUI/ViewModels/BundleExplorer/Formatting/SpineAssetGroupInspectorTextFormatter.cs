using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class SpineAssetGroupInspectorTextFormatter
{
    private readonly ILocalizationService _localizationService;

    public SpineAssetGroupInspectorTextFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string BuildRuntimeAdapterText(SpineAnimationRuntimeDto runtime)
    {
        if (runtime.SupportKind == SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable &&
            !string.IsNullOrWhiteSpace(runtime.AdapterKey))
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupRuntimeAdapterFormat,
                runtime.SpineVersion ?? string.Empty,
                runtime.AdapterKey);
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupRuntimeAdapterUnavailableFormat,
            runtime.SpineVersion ?? string.Empty);
    }

    public string BuildSummaryText(
        SpineAssetGroupDto group,
        SpineAssetGroupDetailsDto? details,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics)
    {
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupSummaryFormat,
            group.SkeletonAssets.Count.ToString(CultureInfo.CurrentCulture),
            group.AtlasAssets.Count.ToString(CultureInfo.CurrentCulture),
            group.TexturePageAssets.Count.ToString(CultureInfo.CurrentCulture),
            (details?.AtlasPages.Count ?? 0).ToString(CultureInfo.CurrentCulture),
            (details?.AtlasPages.Sum(static page => page.Regions.Count) ?? 0)
                .ToString(CultureInfo.CurrentCulture),
            diagnostics.Count.ToString(CultureInfo.CurrentCulture));
    }

    public string ResolvePartRoleText(SpineAssetGroupPartRole role)
    {
        return role switch
        {
            SpineAssetGroupPartRole.Skeleton => GetString(ResourceKeys.InspectorSpineGroupPartRoleSkeleton),
            SpineAssetGroupPartRole.Atlas => GetString(ResourceKeys.InspectorSpineGroupPartRoleAtlas),
            SpineAssetGroupPartRole.TexturePage => GetString(ResourceKeys.InspectorSpineGroupPartRoleTexturePage),
            _ => role.ToString()
        };
    }

    public string BuildSkeletonStructureText(SpineSkeletonSummaryDto summary)
    {
        if (summary.Format == SpineSkeletonAssetFormat.Binary)
        {
            return GetString(ResourceKeys.InspectorSpineGroupSkeletonBinarySummaryText);
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupSkeletonStructureFormat,
            ResolveSkeletonFormatText(summary.Format),
            ResolveNullableCountText(summary.BoneCount),
            ResolveNullableCountText(summary.SlotCount),
            ResolveNullableCountText(summary.SkinCount),
            ResolveNullableCountText(summary.AnimationCount));
    }

    public string BuildSkeletonConstraintText(SpineSkeletonSummaryDto summary)
    {
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupSkeletonConstraintFormat,
            ResolveNullableCountText(summary.EventCount),
            ResolveNullableCountText(summary.IkConstraintCount),
            ResolveNullableCountText(summary.TransformConstraintCount),
            ResolveNullableCountText(summary.PathConstraintCount));
    }

    public string ResolveAssetTypeDisplayName(AssetType assetType, string? assetTypeDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(assetTypeDisplayName))
        {
            return assetTypeDisplayName.Trim();
        }

        return assetType switch
        {
            AssetType.Texture2D => GetString(ResourceKeys.AssetTypeDisplayTexture2D),
            AssetType.TextAsset => GetString(ResourceKeys.AssetTypeDisplayTextAsset),
            AssetType.Sprite => GetString(ResourceKeys.AssetTypeDisplaySprite),
            AssetType.AudioClip => GetString(ResourceKeys.AssetTypeDisplayAudioClip),
            AssetType.Mesh => GetString(ResourceKeys.AssetTypeDisplayMesh),
            AssetType.Material => GetString(ResourceKeys.AssetTypeDisplayMaterial),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };
    }

    private string ResolveSkeletonFormatText(SpineSkeletonAssetFormat format)
    {
        return format switch
        {
            SpineSkeletonAssetFormat.Json => GetString(ResourceKeys.InspectorSpineGroupSkeletonFormatJson),
            SpineSkeletonAssetFormat.Binary => GetString(ResourceKeys.InspectorSpineGroupSkeletonFormatBinary),
            _ => GetString(ResourceKeys.InspectorSpineGroupSkeletonFormatUnknown)
        };
    }

    private string ResolveNullableCountText(int? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.CurrentCulture)
            : GetString(ResourceKeys.InspectorSpineGroupSkeletonCountUnknownText);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
