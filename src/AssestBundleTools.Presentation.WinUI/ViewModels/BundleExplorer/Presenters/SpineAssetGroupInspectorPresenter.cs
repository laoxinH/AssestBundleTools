using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 负责把 Spine 资源组 DTO 投影到现有资源详情检查器模板。
/// </summary>
internal sealed class SpineAssetGroupInspectorPresenter
{
    private readonly IBundleExplorerBundlePaneHost _bundlePane;
    private readonly ILocalizationService _localizationService;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;
    private readonly PreviewDiagnosticDisplayTextResolver _previewDiagnosticDisplayTextResolver;
    private readonly SpineAssetGroupDiagnosticDisplayFormatter _diagnosticDisplayFormatter;

    public SpineAssetGroupInspectorPresenter(
        IBundleExplorerBundlePaneHost bundlePane,
        ILocalizationService localizationService)
    {
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _assetPreviewDisplayFormatter = new AssetPreviewDisplayFormatter(_localizationService);
        _previewDiagnosticDisplayTextResolver = new PreviewDiagnosticDisplayTextResolver(_localizationService);
        _diagnosticDisplayFormatter = new SpineAssetGroupDiagnosticDisplayFormatter(_localizationService);
    }

    public void PresentSpineGroupDocumentTab(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        var group = groupTab.Item;
        var details = groupTab.Details;
        var parts = EnumerateParts(group).ToArray();
        var diagnostics = details?.Diagnostics ?? group.Diagnostics;
        groupTab.Inspector.Apply(
            assetName: groupTab.DisplayName,
            bundleDisplayName: _bundlePane.ResolveBundleDisplayName(group.BundleId),
            assetTypeText: GetString(ResourceKeys.InspectorSpineGroupAssetType),
            containerPath: group.GroupKey,
            serializedFileName: string.Empty,
            sizeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupAssetCountFormat,
                parts.Length.ToString(CultureInfo.CurrentCulture)),
            pathIdText: string.Empty,
            extractCapabilityText: GetString(ResourceKeys.InspectorCapabilityUnavailable),
            fileReplacementCapabilityText: GetString(ResourceKeys.InspectorCapabilityUnavailable),
            workspaceReplacementCapabilityText: GetString(ResourceKeys.InspectorCapabilityUnavailable),
            replacementSourceText: GetString(ResourceKeys.InspectorReplacementSourceNone),
            replacementStatusText: GetString(ResourceKeys.InspectorReplacementStatusNone),
            changeSummaryText: GetString(ResourceKeys.InspectorChangeSummaryNone),
            canExtract: false,
            canSelectFileReplacementSource: false,
            canSelectReplacementSource: false,
            canApplyReplacement: false,
            canUndoReplacement: false,
            assetDependencies: parts.Select(CreatePartDependency),
            referencedByDependencies: Array.Empty<AssetDependencyViewModel>(),
            dependencyClosureItems: Array.Empty<AssetDependencyViewModel>(),
            bundleExternalDependencies: Array.Empty<BundleExternalDependencyViewModel>());
        groupTab.Inspector.SpineAtlasPageSelector.Apply(
            groupTab.AtlasPageOptions,
            groupTab.SelectedAtlasPageOption);
        if (details?.AnimationPreview is { } animationPreview &&
            groupTab.AnimationTextures.Count > 0)
        {
            ApplyAnimationPreview(groupTab.Inspector, group, details, diagnostics, animationPreview, groupTab.AnimationTextures);
            return;
        }

        if (groupTab.TexturePreview is { } texturePreview)
        {
            ApplyTexturePreview(groupTab.Inspector, group, details, diagnostics, texturePreview);
            return;
        }

        groupTab.Inspector.ApplySummaryPreview(
            BuildSummaryText(group, details, diagnostics),
            BuildGroupPreviewInfoRows(group, details, diagnostics));
    }

    private AssetDependencyViewModel CreatePartDependency(SpineAssetGroupPartDto part)
    {
        var assetTypeText = ResolveAssetTypeDisplayName(part.AssetType, part.AssetTypeDisplayName);
        return new AssetDependencyViewModel(
            Name: ResolvePartName(part),
            BundleName: ResolveBundleDisplayName(part),
            MetadataText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupPartMetadataFormat,
                ResolvePartRoleText(part.Role),
                assetTypeText),
            SupplementaryText: NormalizePartPath(part),
            Target: new ResolvedAssetNavigationTarget(
                part.AssetId,
                part.BundleId,
                PathId: 0,
                ResolvePartName(part),
                assetTypeText),
            OpenButtonText: GetString(ResourceKeys.InspectorResolvedAssetOpenButton),
            OpenButtonToolTipText: GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip));
    }

    private void ApplyTexturePreview(
        InspectorPanelViewModel inspector,
        SpineAssetGroupDto group,
        SpineAssetGroupDetailsDto? details,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics,
        SpineAssetGroupTexturePreviewState texturePreview)
    {
        inspector.ApplyImagePreview(
            texturePreview.ImageSource,
            ResolveTexturePreviewCanvasWidth(texturePreview),
            ResolveTexturePreviewCanvasHeight(texturePreview),
            previewInfoRows: BuildTexturePreviewInfoRows(group, details, diagnostics, texturePreview),
            previewPayloadFilePath: texturePreview.Preview.PreviewPayloadFilePath,
            imagePreviewInfoDisplayState: _assetPreviewDisplayFormatter.BuildImagePreviewInfoDisplayState(
                texturePreview.Preview.PreviewInfoItems),
            imageRegionOverlayDisplayState: BuildRegionOverlayDisplayState(texturePreview));
        ApplyPreviewDiagnostic(inspector, texturePreview.Preview.Diagnostic);
    }

    private void ApplyAnimationPreview(
        InspectorPanelViewModel inspector,
        SpineAssetGroupDto group,
        SpineAssetGroupDetailsDto details,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics,
        SpineAnimationPreviewDto animationPreview,
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> animationTextures)
    {
        var rows = new List<InspectorInfoRowViewModel>(
            BuildGroupPreviewInfoRows(group, details, diagnostics))
        {
            new(
                GetString(ResourceKeys.InspectorSpineGroupAnimationPreviewLabel),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorSpineGroupAnimationPreviewFormat,
                    animationPreview.Animations.Count.ToString(CultureInfo.CurrentCulture),
                    animationPreview.Attachments.Count.ToString(CultureInfo.CurrentCulture),
                    animationTextures.Count.ToString(CultureInfo.CurrentCulture)))
        };
        if (animationPreview.Runtime is { } runtime)
        {
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorSpineGroupRuntimeAdapterLabel),
                BuildRuntimeAdapterText(runtime)));
        }

        inspector.ApplySpineAnimationPreview(
            animationPreview,
            animationTextures,
            rows);
    }

    private string BuildRuntimeAdapterText(SpineAnimationRuntimeDto runtime)
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

    private IReadOnlyList<InspectorInfoRowViewModel> BuildGroupPreviewInfoRows(
        SpineAssetGroupDto group,
        SpineAssetGroupDetailsDto? details,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics)
    {
        var rows = new List<InspectorInfoRowViewModel>
        {
            new(GetString(ResourceKeys.InspectorSpineGroupGroupIdLabel), group.GroupId),
            new(GetString(ResourceKeys.InspectorSpineGroupGroupKeyLabel), group.GroupKey),
            new(
                GetString(ResourceKeys.InspectorSpineGroupSkeletonCountLabel),
                group.SkeletonAssets.Count.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorSpineGroupAtlasCountLabel),
                group.AtlasAssets.Count.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorSpineGroupTexturePageCountLabel),
                group.TexturePageAssets.Count.ToString(CultureInfo.CurrentCulture))
        };
        if (details is not null)
        {
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorSpineGroupAtlasPageCountLabel),
                details.AtlasPages.Count.ToString(CultureInfo.CurrentCulture)));
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorSpineGroupAtlasRegionCountLabel),
                CountAtlasRegions(details).ToString(CultureInfo.CurrentCulture)));
            AddSkeletonSummaryRows(rows, details.SkeletonSummaries);
        }

        if (diagnostics.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorSpineGroupDiagnosticsLabel),
                GetString(ResourceKeys.InspectorSpineGroupReadyText)));
            return rows;
        }

        for (var index = 0; index < diagnostics.Count; index++)
        {
            rows.Add(new InspectorInfoRowViewModel(
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorSpineGroupDiagnosticLabelFormat,
                    (index + 1).ToString(CultureInfo.CurrentCulture)),
                _diagnosticDisplayFormatter.BuildDiagnosticText(diagnostics[index])));
        }

        return rows;
    }

    private void AddSkeletonSummaryRows(
        List<InspectorInfoRowViewModel> rows,
        IReadOnlyList<SpineSkeletonSummaryDto> summaries)
    {
        for (var index = 0; index < summaries.Count; index++)
        {
            var label = summaries.Count == 1
                ? GetString(ResourceKeys.InspectorSpineGroupSkeletonStructureLabel)
                : _localizationService.GetFormattedString(
                    ResourceKeys.InspectorSpineGroupSkeletonStructureLabelFormat,
                    (index + 1).ToString(CultureInfo.CurrentCulture));
            rows.Add(new InspectorInfoRowViewModel(label, BuildSkeletonStructureText(summaries[index])));

            if (summaries[index].Format != SpineSkeletonAssetFormat.Json)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(summaries[index].SpineVersion))
            {
                rows.Add(new InspectorInfoRowViewModel(
                    GetString(ResourceKeys.InspectorSpineGroupSkeletonVersionLabel),
                    summaries[index].SpineVersion!));
            }

            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorSpineGroupSkeletonConstraintLabel),
                BuildSkeletonConstraintText(summaries[index])));
        }
    }

    private IReadOnlyList<InspectorInfoRowViewModel> BuildTexturePreviewInfoRows(
        SpineAssetGroupDto group,
        SpineAssetGroupDetailsDto? details,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics,
        SpineAssetGroupTexturePreviewState texturePreview)
    {
        var rows = new List<InspectorInfoRowViewModel>(
            BuildGroupPreviewInfoRows(group, details, diagnostics));
        rows.AddRange(_assetPreviewDisplayFormatter.BuildPreviewInfoRows(
            texturePreview.Preview.PreviewInfoItems));
        return rows;
    }

    private ImageRegionOverlayDisplayState BuildRegionOverlayDisplayState(
        SpineAssetGroupTexturePreviewState texturePreview)
    {
        var regions = texturePreview.AtlasPage.Regions
            .Where(static region => region.Width > 0 && region.Height > 0)
            .Select(static region => new ImageRegionOverlayItemDisplayState(
                region.X,
                region.Y,
                region.Width,
                region.Height,
                region.Name))
            .ToArray();
        return new ImageRegionOverlayDisplayState(
            ResolveTexturePreviewCanvasWidth(texturePreview),
            ResolveTexturePreviewCanvasHeight(texturePreview),
            regions,
            _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupRegionOverlayBadgeFormat,
                ResolveAtlasPageDisplayName(texturePreview.AtlasPage),
                regions.Length.ToString(CultureInfo.CurrentCulture)));
    }

    private string BuildSummaryText(
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
            (details is null ? 0 : CountAtlasRegions(details)).ToString(CultureInfo.CurrentCulture),
            diagnostics.Count.ToString(CultureInfo.CurrentCulture));
    }

    private void ApplyPreviewDiagnostic(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ApplyPreviewDiagnostic(
            cacheStatusText: _previewDiagnosticDisplayTextResolver.GetCacheStatusText(diagnostic.CacheStatus),
            fallbackReasonText: _previewDiagnosticDisplayTextResolver.GetFallbackReasonText(diagnostic.FallbackReason),
            diagnosticDetailText: LocalizedMessageDisplayFormatter.Format(
                diagnostic.DetailMessage,
                _localizationService));
    }

    private string ResolvePartRoleText(SpineAssetGroupPartRole role)
    {
        return role switch
        {
            SpineAssetGroupPartRole.Skeleton => GetString(ResourceKeys.InspectorSpineGroupPartRoleSkeleton),
            SpineAssetGroupPartRole.Atlas => GetString(ResourceKeys.InspectorSpineGroupPartRoleAtlas),
            SpineAssetGroupPartRole.TexturePage => GetString(ResourceKeys.InspectorSpineGroupPartRoleTexturePage),
            _ => role.ToString()
        };
    }

    private string BuildSkeletonStructureText(SpineSkeletonSummaryDto summary)
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

    private string BuildSkeletonConstraintText(SpineSkeletonSummaryDto summary)
    {
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupSkeletonConstraintFormat,
            ResolveNullableCountText(summary.EventCount),
            ResolveNullableCountText(summary.IkConstraintCount),
            ResolveNullableCountText(summary.TransformConstraintCount),
            ResolveNullableCountText(summary.PathConstraintCount));
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

    private string ResolveAssetTypeDisplayName(AssetType assetType, string? assetTypeDisplayName)
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

    private string ResolveBundleDisplayName(SpineAssetGroupPartDto part)
    {
        return string.IsNullOrWhiteSpace(part.BundleDisplayName)
            ? _bundlePane.ResolveBundleDisplayName(part.BundleId)
            : part.BundleDisplayName.Trim();
    }

    private static IEnumerable<SpineAssetGroupPartDto> EnumerateParts(SpineAssetGroupDto group)
    {
        foreach (var part in group.SkeletonAssets)
        {
            yield return part;
        }

        foreach (var part in group.AtlasAssets)
        {
            yield return part;
        }

        foreach (var part in group.TexturePageAssets)
        {
            yield return part;
        }
    }

    private static string ResolvePartName(SpineAssetGroupPartDto part)
    {
        return string.IsNullOrWhiteSpace(part.Name)
            ? part.AssetId.Value
            : part.Name.Trim();
    }

    private static string NormalizePartPath(SpineAssetGroupPartDto part)
    {
        return string.IsNullOrWhiteSpace(part.AssetPath)
            ? part.AssetId.Value
            : part.AssetPath.Trim().Replace('\\', '/');
    }

    private static int CountAtlasRegions(SpineAssetGroupDetailsDto details) =>
        details.AtlasPages.Sum(static page => page.Regions.Count);

    private static int ResolveTexturePreviewCanvasWidth(SpineAssetGroupTexturePreviewState texturePreview) =>
        texturePreview.AtlasPage.Width is > 0
            ? texturePreview.AtlasPage.Width.Value
            : Math.Max(1, texturePreview.PixelWidth);

    private static int ResolveTexturePreviewCanvasHeight(SpineAssetGroupTexturePreviewState texturePreview) =>
        texturePreview.AtlasPage.Height is > 0
            ? texturePreview.AtlasPage.Height.Value
            : Math.Max(1, texturePreview.PixelHeight);

    private static string ResolveAtlasPageDisplayName(SpineAtlasPageDto atlasPage) =>
        string.IsNullOrWhiteSpace(atlasPage.Name)
            ? string.Empty
            : atlasPage.Name.Trim();

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
