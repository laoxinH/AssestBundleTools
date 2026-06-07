using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 将 Spine 组成资产的真实变更记录投影到组文档变更页。
/// </summary>
internal sealed class SpineAssetGroupChangeRecordPresenter
{
    private readonly ILocalizationService _localizationService;
    private readonly BundleExplorerAssetChangeRecordPresenter _assetChangeRecordPresenter;

    public SpineAssetGroupChangeRecordPresenter(
        ILocalizationService localizationService,
        WorkspaceOperationPendingTracker? pendingTracker = null)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _assetChangeRecordPresenter = new BundleExplorerAssetChangeRecordPresenter(
            _localizationService,
            pendingTracker);
    }

    public string BuildSummaryText(IReadOnlyList<SpineAssetGroupPartDto> parts)
    {
        var lastExtractedAtUtc = parts
            .Select(static part => part.LastExtractedAtUtc)
            .OfType<DateTimeOffset>()
            .DefaultIfEmpty()
            .Max();

        return lastExtractedAtUtc == default
            ? GetString(ResourceKeys.InspectorChangeSummaryNone)
            : _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeSummaryExtractOnlyFormat,
                lastExtractedAtUtc.ToLocalTime().ToString("G", CultureInfo.CurrentCulture));
    }

    public IReadOnlyList<InspectorChangeRecordViewModel> CreateChangeRecords(
        IReadOnlyList<SpineAssetGroupPartDto> parts,
        bool canManageRecords,
        WorkspaceId? workspaceId = null)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return _assetChangeRecordPresenter.CreateChangeRecords(
            CreateGroupChangeRecords(parts),
            canManageRecords,
            workspaceId);
    }

    private static IReadOnlyList<AssetChangeRecordDto> CreateGroupChangeRecords(
        IReadOnlyList<SpineAssetGroupPartDto> parts)
    {
        var records = new List<AssetChangeRecordDto>();
        foreach (var part in parts)
        {
            foreach (var record in part.ChangeRecords ?? Array.Empty<AssetChangeRecordDto>())
            {
                records.Add(ProjectPartChangeRecord(record, part));
            }
        }

        return records;
    }

    private static AssetChangeRecordDto ProjectPartChangeRecord(
        AssetChangeRecordDto record,
        SpineAssetGroupPartDto part)
    {
        var targetBundleId = string.IsNullOrWhiteSpace(record.TargetBundleId)
            ? part.BundleId.Value
            : record.TargetBundleId;
        var targetAssetId = string.IsNullOrWhiteSpace(record.TargetAssetId)
            ? part.AssetId.Value
            : record.TargetAssetId;
        var targetAssetName = string.IsNullOrWhiteSpace(record.TargetAssetName)
            ? ResolvePartName(part)
            : record.TargetAssetName;
        var targetAssetType = string.IsNullOrWhiteSpace(record.TargetAssetType)
            ? part.AssetType.ToString()
            : record.TargetAssetType;

        return record with
        {
            Kind = record.Kind == AssetChangeRecordKind.FileReplacement
                ? AssetChangeRecordKind.AssociatedFileReplacement
                : record.Kind,
            TargetBundleId = targetBundleId,
            TargetAssetId = targetAssetId,
            TargetAssetName = targetAssetName,
            TargetAssetType = targetAssetType,
            IsRelatedOperation = true
        };
    }

    private static string ResolvePartName(SpineAssetGroupPartDto part) =>
        string.IsNullOrWhiteSpace(part.Name)
            ? part.AssetId.Value
            : part.Name.Trim();

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
