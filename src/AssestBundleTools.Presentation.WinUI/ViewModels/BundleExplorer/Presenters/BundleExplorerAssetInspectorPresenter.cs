using System.Globalization;
using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 负责把资产详情 DTO 投影为列表项、检查器和替换状态等可绑定 UI 状态。
/// </summary>
internal sealed class BundleExplorerAssetInspectorPresenter
{
    private const string EditGlyph = "\uE70F";
    private const string ReplaceFileGlyph = "\uE8AC";

    private static readonly AssetType[] PreferredAssetTypeOrder =
    [
        AssetType.Texture2D,
        AssetType.Sprite,
        AssetType.TextAsset,
        AssetType.AudioClip,
        AssetType.Mesh,
        AssetType.Material
    ];

    private readonly IBundleExplorerBundlePaneHost _bundlePane;
    private readonly ILocalizationService _localizationService;
    private readonly LocalizedValueFormatter _localizedValueFormatter;
    private readonly BundleExplorerAssetChangeRecordPresenter _changeRecordPresenter;

    public BundleExplorerAssetInspectorPresenter(
        IBundleExplorerBundlePaneHost bundlePane,
        ILocalizationService localizationService,
        WorkspaceOperationPendingTracker? pendingTracker = null)
    {
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
        _changeRecordPresenter = new BundleExplorerAssetChangeRecordPresenter(
            _localizationService,
            pendingTracker);
    }

    public IReadOnlyList<AssetTypeOptionViewModel> CreateAssetTypeOptions(
        IReadOnlyList<AssetTypeFacetDto>? typeFacets = null)
    {
        var normalizedFacets = (typeFacets ?? Array.Empty<AssetTypeFacetDto>())
            .Where(static facet => facet.Count > 0)
            .ToArray();
        var shouldUseFacets = typeFacets is not null;
        var options = new List<AssetTypeOptionViewModel>
        {
            new(GetString(ResourceKeys.AssetTypeFilterAll), null)
        };

        foreach (var assetType in PreferredAssetTypeOrder)
        {
            var count = normalizedFacets
                .Where(facet => facet.AssetType == assetType)
                .Sum(static facet => facet.Count);
            if (!shouldUseFacets || count > 0)
            {
                options.Add(new AssetTypeOptionViewModel(
                    GetAssetTypeDisplayName(assetType),
                    assetType,
                    Count: shouldUseFacets ? count : null));
            }
        }

        if (!shouldUseFacets)
        {
            return options;
        }

        var preferredAssetTypes = PreferredAssetTypeOrder.ToHashSet();
        var dynamicOptions = normalizedFacets
            .Where(facet => facet.AssetType == AssetType.Unknown ||
                            !preferredAssetTypes.Contains(facet.AssetType))
            .OrderBy(static facet => facet.AssetTypeDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(facet => new AssetTypeOptionViewModel(
                ResolveDynamicAssetTypeDisplayName(facet),
                facet.AssetType,
                facet.AssetType == AssetType.Unknown ? facet.AssetTypeDisplayName : null,
                facet.Count));
        options.AddRange(dynamicOptions);
        return options;
    }

    public AssetListEntryViewModel CreateAssetEntry(AssetListItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new AssetListEntryViewModel(
            item: item,
            assetTypeText: ResolveAssetTypeDisplayName(item.AssetType, item.AssetTypeDisplayName),
            sizeText: _localizedValueFormatter.FormatCompactByteSize(
                ResolveDisplaySizeInBytes(item.SizeInBytes, item.PreferredDisplaySizeInBytes)),
            stateText: CreateAssetStateText(item.IsModified));
    }

    public string CreateAssetStateText(bool isModified) =>
        isModified
            ? GetString(ResourceKeys.AssetStateModified)
            : GetString(ResourceKeys.AssetStateOriginal);

    public SpineAssetGroupEntryViewModel CreateSpineAssetGroupEntry(SpineAssetGroupDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new SpineAssetGroupEntryViewModel(
            item: item,
            partsText: _localizationService.GetFormattedString(
                ResourceKeys.AssetPaneViewSpineGroupPartsFormat,
                item.SkeletonAssets.Count.ToString(CultureInfo.CurrentCulture),
                item.AtlasAssets.Count.ToString(CultureInfo.CurrentCulture)),
            texturePagesText: _localizationService.GetFormattedString(
                ResourceKeys.AssetPaneViewSpineGroupTexturePagesFormat,
                item.TexturePageAssets.Count.ToString(CultureInfo.CurrentCulture)),
            stateText: item.Diagnostics.Count == 0
                ? GetString(ResourceKeys.AssetPaneViewSpineGroupStateReady)
                : _localizationService.GetFormattedString(
                    ResourceKeys.AssetPaneViewSpineGroupStateDiagnosticsFormat,
                    item.Diagnostics.Count.ToString(CultureInfo.CurrentCulture)));
    }

    /// <summary>
    /// Inspector 的文案、依赖卡片和替换摘要都由展示层统一投影。
    /// 避免把本地化格式化散落回主工作流 ViewModel。
    /// </summary>
    public void PresentAssetDocumentTab(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        bool canEdit,
        bool isBusy)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);

        var canUndoReplacement = canEdit && !isBusy && HasAppliedReplacement(details);
        var canRedoReplacement = canEdit && !isBusy && details.CanRedoReplacement;
        var canManageChangeRecords = canEdit;

        assetTab.ApplyDetails(details);
        assetTab.CanEdit = canEdit;

        assetTab.Inspector.Apply(
            assetName: details.Name,
            bundleDisplayName: _bundlePane.ResolveBundleDisplayName(details.BundleId),
            assetTypeText: ResolveAssetTypeDisplayName(details.AssetType, details.AssetTypeDisplayName),
            containerPath: details.ContainerPath,
            serializedFileName: details.SerializedFileName ?? string.Empty,
            sizeText: _localizedValueFormatter.FormatCompactByteSize(
                ResolveDisplaySizeInBytes(details.SizeInBytes, details.PreferredDisplaySizeInBytes)),
            pathIdText: details.PathId.ToString(CultureInfo.InvariantCulture),
            extractCapabilityText: details.CanExtract
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            fileReplacementCapabilityText: details.CanReplaceFromFile
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            workspaceReplacementCapabilityText: details.CanReplaceFromWorkspace
                ? GetString(ResourceKeys.InspectorCapabilityAvailable)
                : GetString(ResourceKeys.InspectorCapabilityUnavailable),
            replacementSourceText: BuildReplacementSourceText(
                details,
                assetTab.PendingWorkspaceReplacementCandidate,
                assetTab.PendingFileReplacementCandidate),
            replacementStatusText: BuildReplacementStatusText(
                details,
                assetTab.PendingWorkspaceReplacementCandidate,
                assetTab.PendingFileReplacementCandidate),
            changeSummaryText: _changeRecordPresenter.EmptyText,
            canExtract: canEdit && !isBusy && details.CanExtract,
            canSelectFileReplacementSource: canEdit && !isBusy && details.CanReplaceFromFile,
            canSelectReplacementSource: canEdit && !isBusy && details.CanReplaceFromWorkspace,
            canApplyReplacement: canEdit && !isBusy &&
                                 (assetTab.PendingWorkspaceReplacementCandidate is not null ||
                                  assetTab.PendingFileReplacementCandidate is not null),
            canUndoReplacement: canUndoReplacement,
            assetDependencies: details.Dependencies.Select(MapDependency),
            referencedByDependencies: (details.ReferencedBy ?? Array.Empty<AssetDependencyDto>())
                .Select(MapDependency),
            dependencyClosureItems: (details.DependencyClosure ?? Array.Empty<AssetDependencyDto>())
                .Select(MapDependency),
            bundleExternalDependencies: (details.BundleExternalDependencies ?? Array.Empty<BundleExternalDependencyDto>())
                .Select(MapBundleExternalDependency),
            canRedoReplacement: canRedoReplacement,
            changeRecords: _changeRecordPresenter.CreateChangeRecords(
                details,
                canManageChangeRecords),
            canModify: canEdit && !isBusy && details.CanReplaceFromFile,
            modifyButtonText: ResolveModifyButtonText(details),
            modifyButtonIconGlyph: ResolveModifyButtonIconGlyph(details));
    }

    public void SyncInspectorActionStates(
        InspectorPanelViewModel inspector,
        AssetDocumentTabViewModel? assetTab,
        AssetDetailsDto? details,
        bool isBusy)
    {
        ArgumentNullException.ThrowIfNull(inspector);

        if (assetTab is null || details is null)
        {
            inspector.CanExtract = false;
            inspector.CanSelectFileReplacementSource = false;
            inspector.CanSelectReplacementSource = false;
            inspector.CanApplyReplacement = false;
            inspector.CanUndoReplacement = false;
            inspector.CanRedoReplacement = false;
            inspector.CanModify = false;
            return;
        }

        inspector.CanExtract = !isBusy && assetTab.CanEdit && details.CanExtract;
        inspector.CanSelectFileReplacementSource = !isBusy && assetTab.CanEdit && details.CanReplaceFromFile;
        inspector.CanSelectReplacementSource = !isBusy && assetTab.CanEdit && details.CanReplaceFromWorkspace;
        inspector.CanApplyReplacement = !isBusy &&
                                       assetTab.CanEdit &&
                                        (assetTab.PendingWorkspaceReplacementCandidate is not null ||
                                         assetTab.PendingFileReplacementCandidate is not null);
        inspector.CanUndoReplacement = !isBusy && assetTab.CanEdit && HasAppliedReplacement(details);
        inspector.CanRedoReplacement = !isBusy && assetTab.CanEdit && details.CanRedoReplacement;
        inspector.CanModify = !isBusy && assetTab.CanEdit && details.CanReplaceFromFile;
    }

    private AssetDependencyViewModel MapDependency(AssetDependencyDto dependency)
    {
        var assetTypeText = ResolveAssetTypeDisplayName(dependency.AssetType, dependency.AssetTypeDisplayName);
        return new AssetDependencyViewModel(
            Name: dependency.Name,
            BundleName: _bundlePane.ResolveBundleDisplayName(dependency.BundleId),
            MetadataText: BuildDependencyMetadataText(dependency, assetTypeText),
            SupplementaryText: BuildDependencySupplementaryText(dependency),
            Target: new ResolvedAssetNavigationTarget(
                dependency.AssetId,
                dependency.BundleId,
                dependency.PathId,
                dependency.Name,
                assetTypeText),
            OpenButtonText: GetString(ResourceKeys.InspectorResolvedAssetOpenButton),
            OpenButtonToolTipText: GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip));
    }

    private string ResolveModifyButtonText(AssetDetailsDto details) =>
        details.AssetType == AssetType.Sprite
            ? GetString(ResourceKeys.InspectorModifyButton)
            : GetString(ResourceKeys.InspectorReplaceFileButton);

    private static string ResolveModifyButtonIconGlyph(AssetDetailsDto details) =>
        details.AssetType == AssetType.Sprite
            ? EditGlyph
            : ReplaceFileGlyph;

    /// <summary>
    /// Bundle 外部依赖的友好名称已经在基础设施层做过一轮解析，
    /// Presentation 这里只负责补本地化类型说明和最终显示兜底。
    /// </summary>
    private BundleExternalDependencyViewModel MapBundleExternalDependency(BundleExternalDependencyDto dependency)
    {
        var referencePath = string.IsNullOrWhiteSpace(dependency.ReferencePath)
            ? string.Empty
            : dependency.ReferencePath.Trim().Replace('\\', '/');
        var referenceDisplayName = string.IsNullOrWhiteSpace(dependency.ReferenceDisplayName)
            ? Path.GetFileName(referencePath)
            : dependency.ReferenceDisplayName.Trim();
        var displayName = string.IsNullOrWhiteSpace(dependency.ResolvedBundleDisplayName)
            ? dependency.DisplayName
            : dependency.ResolvedBundleDisplayName;
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(referencePath)
            : displayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.IsNullOrWhiteSpace(referencePath)
                ? GetString(ResourceKeys.BundleExplorerSelectionNone)
                : referencePath;
        }

        var kindText = GetBundleExternalDependencyKindText(dependency.Kind);
        // 当当前工作区已经能把 CAB 标识反查到具体资源包时，
        // 副标题继续保留 CAB 摘要，避免丢失更底层的 serialized file 定位信息。
        if (!string.IsNullOrWhiteSpace(dependency.ResolvedBundleDisplayName) &&
            !string.IsNullOrWhiteSpace(referenceDisplayName) &&
            !string.Equals(displayName, referenceDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            kindText = $"{kindText} · {referenceDisplayName}";
        }

        return new BundleExternalDependencyViewModel(
            DisplayName: displayName,
            KindText: kindText,
            ReferencePath: referencePath);
    }

    private string BuildDependencyMetadataText(
        AssetDependencyDto dependency,
        string assetTypeText)
    {
        // Unity 的 PathId 是有符号 long，负数同样是合法对象标识。
        // 依赖卡片只应在 PathId 为 0（未解析 / 无效）时隐藏该字段。
        if (dependency.PathId != 0)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorDependencyMetadataWithPathIdFormat,
                assetTypeText,
                dependency.PathId.ToString(CultureInfo.InvariantCulture));
        }

        return assetTypeText;
    }

    private string BuildDependencySupplementaryText(AssetDependencyDto dependency)
    {
        if (dependency.DependencyDepth is not { } depth || depth <= 0)
        {
            return string.Empty;
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorDependencyDepthFormat,
            depth.ToString(CultureInfo.CurrentCulture));
    }

    private string GetBundleExternalDependencyKindText(BundleExternalDependencyKind kind)
    {
        return kind switch
        {
            BundleExternalDependencyKind.ArchiveSerializedFile => GetString(ResourceKeys.InspectorBundleExternalDependencyKindArchiveSerializedFile),
            BundleExternalDependencyKind.FilePath => GetString(ResourceKeys.InspectorBundleExternalDependencyKindFilePath),
            _ => GetString(ResourceKeys.InspectorBundleExternalDependencyKindUnknown)
        };
    }

    private string GetAssetTypeDisplayName(AssetType assetType)
    {
        return assetType switch
        {
            AssetType.Texture2D => GetString(ResourceKeys.AssetTypeDisplayTexture2D),
            AssetType.Sprite => GetString(ResourceKeys.AssetTypeDisplaySprite),
            AssetType.TextAsset => GetString(ResourceKeys.AssetTypeDisplayTextAsset),
            AssetType.AudioClip => GetString(ResourceKeys.AssetTypeDisplayAudioClip),
            AssetType.Mesh => GetString(ResourceKeys.AssetTypeDisplayMesh),
            AssetType.Material => GetString(ResourceKeys.AssetTypeDisplayMaterial),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };
    }

    private static string ResolveDynamicAssetTypeDisplayName(AssetTypeFacetDto facet)
    {
        return string.IsNullOrWhiteSpace(facet.AssetTypeDisplayName)
            ? facet.AssetType.ToString()
            : facet.AssetTypeDisplayName.Trim();
    }

    private string ResolveAssetTypeDisplayName(AssetType assetType, string? assetTypeDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(assetTypeDisplayName))
        {
            return assetTypeDisplayName.Trim();
        }

        return GetAssetTypeDisplayName(assetType);
    }

    private string BuildReplacementSourceText(
        AssetDetailsDto details,
        WorkspaceReplacementCandidateDto? pendingWorkspaceCandidate,
        FileReplacementCandidateDto? pendingFileCandidate)
    {
        if (pendingFileCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceFileCandidateFormat,
                pendingFileCandidate.SourceFileName,
                pendingFileCandidate.SourceFilePath,
                GetAssetTypeDisplayName(pendingFileCandidate.SourceAssetType));
        }

        if (pendingWorkspaceCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceCandidateFormat,
                pendingWorkspaceCandidate.SourceBundleDisplayName,
                pendingWorkspaceCandidate.SourceAssetName,
                GetAssetTypeDisplayName(pendingWorkspaceCandidate.SourceAssetType));
        }

        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName) &&
            !string.IsNullOrWhiteSpace(details.FileReplacementSourcePath) &&
            details.FileReplacementSourceAssetType is { } fileAssetType)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceFileAppliedFormat,
                details.FileReplacementSourceFileName,
                details.FileReplacementSourcePath,
                GetAssetTypeDisplayName(fileAssetType));
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) &&
            !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceBundleDisplayName) &&
            details.WorkspaceReplacementSourceAssetType is { } sourceAssetType)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementSourceAppliedFormat,
                details.WorkspaceReplacementSourceBundleDisplayName,
                details.WorkspaceReplacementSourceAssetName,
                GetAssetTypeDisplayName(sourceAssetType));
        }

        return GetString(ResourceKeys.InspectorReplacementSourceNone);
    }

    private string BuildReplacementStatusText(
        AssetDetailsDto details,
        WorkspaceReplacementCandidateDto? pendingWorkspaceCandidate,
        FileReplacementCandidateDto? pendingFileCandidate)
    {
        if (pendingFileCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusFilePendingFormat,
                pendingFileCandidate.SourceFileName);
        }

        if (pendingWorkspaceCandidate is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusPendingFormat,
                pendingWorkspaceCandidate.SourceAssetName);
        }

        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName))
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusFileAppliedFormat,
                details.FileReplacementSourceFileName);
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName))
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorReplacementStatusAppliedFormat,
                details.WorkspaceReplacementSourceAssetName);
        }

        return GetString(ResourceKeys.InspectorReplacementStatusNone);
    }

    private static bool HasAppliedReplacement(AssetDetailsDto details)
    {
        return !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) ||
               !string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName);
    }

    private static long ResolveDisplaySizeInBytes(long sizeInBytes, long? preferredDisplaySizeInBytes)
    {
        return preferredDisplaySizeInBytes is { } preferredSize && preferredSize > 0
            ? preferredSize
            : sizeInBytes;
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
