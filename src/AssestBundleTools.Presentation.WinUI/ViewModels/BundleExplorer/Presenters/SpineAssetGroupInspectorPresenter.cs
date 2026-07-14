using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 负责把 Spine 资源组 DTO 投影到现有资源详情检查器模板。
/// </summary>
internal sealed class SpineAssetGroupInspectorPresenter
{
    private const string EditGlyph = "\uE837";

    private readonly IBundleExplorerBundlePaneHost _bundlePane;
    private readonly ILocalizationService _localizationService;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;
    private readonly PreviewDiagnosticDisplayTextResolver _previewDiagnosticDisplayTextResolver;
    private readonly SpineAssetGroupDiagnosticDisplayFormatter _diagnosticDisplayFormatter;
    private readonly SpineAssetGroupChangeRecordPresenter _changeRecordPresenter;
    private readonly SpineAssetGroupInspectorTextFormatter _textFormatter;
    private readonly Func<WorkspaceId?> _workspaceIdProvider;

    public SpineAssetGroupInspectorPresenter(
        IBundleExplorerBundlePaneHost bundlePane,
        ILocalizationService localizationService,
        WorkspaceOperationPendingTracker? pendingTracker = null,
        Func<WorkspaceId?>? workspaceIdProvider = null)
    {
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _assetPreviewDisplayFormatter = new AssetPreviewDisplayFormatter(_localizationService);
        _previewDiagnosticDisplayTextResolver = new PreviewDiagnosticDisplayTextResolver(_localizationService);
        _diagnosticDisplayFormatter = new SpineAssetGroupDiagnosticDisplayFormatter(_localizationService);
        _changeRecordPresenter = new SpineAssetGroupChangeRecordPresenter(
            _localizationService,
            pendingTracker);
        _textFormatter = new SpineAssetGroupInspectorTextFormatter(_localizationService);
        _workspaceIdProvider = workspaceIdProvider ?? (() => null);
    }

    public void PresentSpineGroupDocumentTab(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        var group = groupTab.Item;
        var details = groupTab.Details;
        var displayGroup = details?.Group ?? group;
        var parts = EnumerateParts(displayGroup).ToArray();
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
            extractCapabilityText: parts.Length > 0
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            fileReplacementCapabilityText: GetString(ResourceKeys.InspectorCapabilityUnavailable),
            workspaceReplacementCapabilityText: GetString(ResourceKeys.InspectorCapabilityUnavailable),
            replacementSourceText: GetString(ResourceKeys.InspectorReplacementSourceNone),
            replacementStatusText: GetString(ResourceKeys.InspectorReplacementStatusNone),
            changeSummaryText: _changeRecordPresenter.BuildSummaryText(parts),
            canExtract: parts.Length > 0,
            canSelectFileReplacementSource: false,
            canSelectReplacementSource: false,
            canApplyReplacement: false,
            canUndoReplacement: false,
            assetDependencies: parts.Select(CreatePartDependency),
            referencedByDependencies: Array.Empty<AssetDependencyViewModel>(),
            dependencyClosureItems: Array.Empty<AssetDependencyViewModel>(),
            bundleExternalDependencies: Array.Empty<BundleExternalDependencyViewModel>(),
            changeRecords: _changeRecordPresenter.CreateChangeRecords(
                parts,
                canManageRecords: true,
                _workspaceIdProvider()),
            canModify: parts.Length > 0,
            modifyButtonText: GetString(ResourceKeys.InspectorModifyButton),
            modifyButtonIconGlyph: EditGlyph);
        groupTab.Inspector.SpineAtlasPageSelector.Apply(
            groupTab.AtlasPageOptions,
            groupTab.SelectedAtlasPageOption);
        if (details?.AnimationPreview is { } animationPreview &&
            groupTab.AnimationTextures.Count > 0)
        {
            ApplyAnimationPreview(
                groupTab.Inspector,
                group,
                details,
                diagnostics,
                animationPreview,
                groupTab.AnimationTextures,
                groupTab.CapturePreviewState());
            return;
        }

        if (groupTab.TexturePreview is { } texturePreview)
        {
            ApplyTexturePreview(groupTab.Inspector, group, details, diagnostics, texturePreview);
            return;
        }

        groupTab.Inspector.ApplySummaryPreview(
            _textFormatter.BuildSummaryText(group, details, diagnostics),
            BuildGroupPreviewInfoRows(group, details, diagnostics));
    }

    public void PresentSpineGroupPreviewLoading(SpineAssetGroupDocumentTabViewModel groupTab)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        groupTab.Inspector.SetPreviewLoading(GetString(ResourceKeys.InspectorPreviewLoadingText));
    }

    public void PresentSpineGroupOperationProjection(
        SpineAssetGroupDocumentTabViewModel groupTab,
        bool isBusy)
    {
        ArgumentNullException.ThrowIfNull(groupTab);

        var parts = EnumerateParts(groupTab.Item).ToArray();
        groupTab.Inspector.ApplyOperationProjection(
            changeSummaryText: _changeRecordPresenter.BuildSummaryText(parts),
            canExtract: !isBusy && parts.Length > 0,
            canModify: !isBusy && parts.Length > 0,
            changeRecords: _changeRecordPresenter.CreateChangeRecords(
                parts,
                canManageRecords: true,
                _workspaceIdProvider()));
    }

    public void SyncInspectorActionStates(
        InspectorPanelViewModel inspector,
        SpineAssetGroupDocumentTabViewModel? groupTab,
        bool isBusy)
    {
        ArgumentNullException.ThrowIfNull(inspector);

        inspector.CanExtract = !isBusy &&
                               groupTab is not null &&
                               EnumerateParts(groupTab.Item).Any();
        inspector.CanSelectFileReplacementSource = false;
        inspector.CanSelectReplacementSource = false;
        inspector.CanApplyReplacement = false;
        inspector.CanUndoReplacement = false;
        inspector.CanRedoReplacement = false;
        inspector.CanModify = !isBusy &&
                              groupTab is not null &&
                              EnumerateParts(groupTab.Item).Any();
    }

    private AssetDependencyViewModel CreatePartDependency(SpineAssetGroupPartDto part)
    {
        var assetTypeText = _textFormatter.ResolveAssetTypeDisplayName(
            part.AssetType,
            part.AssetTypeDisplayName);
        return new AssetDependencyViewModel(
            Name: ResolvePartName(part),
            BundleName: ResolveBundleDisplayName(part),
            MetadataText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupPartMetadataFormat,
                _textFormatter.ResolvePartRoleText(part.Role),
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
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> animationTextures,
        WorkspaceSpinePreviewStateDto? previewState)
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
                _textFormatter.BuildRuntimeAdapterText(runtime)));
        }

        inspector.ApplySpineAnimationPreview(
            animationPreview,
            animationTextures,
            rows,
            previewState);
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
            rows.Add(new InspectorInfoRowViewModel(
                label,
                _textFormatter.BuildSkeletonStructureText(summaries[index])));

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
                _textFormatter.BuildSkeletonConstraintText(summaries[index])));
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
