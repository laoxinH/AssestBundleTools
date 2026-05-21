using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 将资产操作记录投影为检查器“变更”页中的独立展示项。
/// </summary>
internal sealed class BundleExplorerAssetChangeRecordPresenter
{
    private readonly ILocalizationService _localizationService;

    public BundleExplorerAssetChangeRecordPresenter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string EmptyText => GetString(ResourceKeys.InspectorChangeSummaryNone);

    public IReadOnlyList<InspectorChangeRecordViewModel> CreateChangeRecords(
        AssetDetailsDto details,
        bool canUndoReplacement,
        bool canRedoReplacement)
    {
        ArgumentNullException.ThrowIfNull(details);

        var sourceRecords = NormalizeChangeRecords(details);
        var firstReplacementActionAssigned = false;
        var records = new List<InspectorChangeRecordViewModel>(sourceRecords.Count);
        foreach (var record in sourceRecords)
        {
            var isReplacementRecord = IsReplacementRecord(record);
            var showUndo = false;
            var showRedo = false;
            if (isReplacementRecord && !firstReplacementActionAssigned)
            {
                showUndo = canUndoReplacement;
                showRedo = !showUndo && canRedoReplacement;
                firstReplacementActionAssigned = showUndo || showRedo;
            }

            records.Add(CreateChangeRecord(record, showUndo, showRedo));
        }

        return records;
    }

    private IReadOnlyList<AssetChangeRecordDto> NormalizeChangeRecords(AssetDetailsDto details)
    {
        var candidateRecords = (details.ChangeRecords ?? Array.Empty<AssetChangeRecordDto>())
            .Where(static record => record.Kind is
                AssetChangeRecordKind.Extraction or
                AssetChangeRecordKind.FileReplacement or
                AssetChangeRecordKind.WorkspaceReplacement)
            .ToArray();
        var records = candidateRecords
            .Where(static record => record.Kind is not AssetChangeRecordKind.Extraction)
            .Concat(candidateRecords
                .Where(static record => record.Kind == AssetChangeRecordKind.Extraction)
                .OrderByDescending(static record => record.DisplayTimestampUtc)
                .Take(1))
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .ToArray();
        return records.Length > 0
            ? records
            : CreateEffectiveStateFallbackRecords(details);
    }

    private static IReadOnlyList<AssetChangeRecordDto> CreateEffectiveStateFallbackRecords(AssetDetailsDto details)
    {
        var records = new List<AssetChangeRecordDto>();
        if (!string.IsNullOrWhiteSpace(details.FileReplacementSourceFileName) ||
            !string.IsNullOrWhiteSpace(details.FileReplacementSourcePath))
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.FileReplacement,
                CreatedAtUtc: DateTimeOffset.MinValue,
                SourceFileName: details.FileReplacementSourceFileName,
                SourceFilePath: details.FileReplacementSourcePath));
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) ||
            !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceBundleDisplayName))
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.WorkspaceReplacement,
                CreatedAtUtc: DateTimeOffset.MinValue,
                SourceBundleDisplayName: details.WorkspaceReplacementSourceBundleDisplayName,
                SourceAssetName: details.WorkspaceReplacementSourceAssetName));
        }

        if (details.LastExtractedAtUtc is { } extractedAtUtc)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.Extraction,
                CreatedAtUtc: extractedAtUtc,
                AppliedAtUtc: extractedAtUtc));
        }

        return records;
    }

    private InspectorChangeRecordViewModel CreateChangeRecord(
        AssetChangeRecordDto record,
        bool showUndo,
        bool showRedo)
    {
        return new InspectorChangeRecordViewModel(
            PrimaryText: BuildPrimaryText(record),
            SecondaryText: BuildSecondaryText(record),
            ShowsUndoReplacement: showUndo,
            ShowsRedoReplacement: showRedo,
            UndoButtonText: GetString(ResourceKeys.InspectorUndoReplacementButton),
            RedoButtonText: GetString(ResourceKeys.InspectorRedoReplacementButton));
    }

    private string BuildPrimaryText(AssetChangeRecordDto record)
    {
        var timestamp = FormatTimestamp(record.DisplayTimestampUtc);
        return record.Kind switch
        {
            AssetChangeRecordKind.FileReplacement => _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeRecordFileReplacementFormat,
                ResolveDisplayValue(record.SourceFileName ?? record.SourceFilePath),
                timestamp),
            AssetChangeRecordKind.WorkspaceReplacement => _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeRecordWorkspaceReplacementFormat,
                ResolveDisplayValue(record.SourceBundleDisplayName),
                ResolveDisplayValue(record.SourceAssetName),
                timestamp),
            AssetChangeRecordKind.Extraction => _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeRecordExtractFormat,
                timestamp),
            _ => GetString(ResourceKeys.InspectorChangeSummaryNone)
        };
    }

    private string BuildSecondaryText(AssetChangeRecordDto record)
    {
        return record.Kind == AssetChangeRecordKind.FileReplacement
            ? record.SourceFilePath?.Trim() ?? string.Empty
            : string.Empty;
    }

    private string ResolveDisplayValue(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? GetString(ResourceKeys.BundleExplorerSelectionNone)
            : value.Trim();

    private string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp == DateTimeOffset.MinValue
            ? GetString(ResourceKeys.BundleExplorerSelectionNone)
            : timestamp.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);
    }

    private static bool IsReplacementRecord(AssetChangeRecordDto record) =>
        record.Kind is AssetChangeRecordKind.FileReplacement or AssetChangeRecordKind.WorkspaceReplacement;

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
