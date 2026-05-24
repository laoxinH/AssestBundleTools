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
        bool canManageRecords)
    {
        ArgumentNullException.ThrowIfNull(details);

        var sourceRecords = NormalizeChangeRecords(details);
        var records = new List<InspectorChangeRecordViewModel>(sourceRecords.Count);
        foreach (var record in sourceRecords)
        {
            var isReplacementRecord = IsReplacementRecord(record);
            var showUndo = isReplacementRecord && record.IsEffectiveReplacement;
            var showRedo = isReplacementRecord && !record.IsEffectiveReplacement;

            records.Add(CreateChangeRecord(
                record,
                showUndo,
                showRedo,
                canManageRecords && !string.IsNullOrWhiteSpace(record.OperationId),
                canManageRecords && (showUndo || !string.IsNullOrWhiteSpace(record.OperationId))));
        }

        return records;
    }

    private IReadOnlyList<AssetChangeRecordDto> NormalizeChangeRecords(AssetDetailsDto details)
    {
        var candidateRecords = (details.ChangeRecords ?? Array.Empty<AssetChangeRecordDto>())
            .Where(static record => record.Kind is
                AssetChangeRecordKind.Extraction or
                AssetChangeRecordKind.FileReplacement or
                AssetChangeRecordKind.AssociatedFileReplacement or
                AssetChangeRecordKind.WorkspaceReplacement or
                AssetChangeRecordKind.SpriteEdit)
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
                SourceFilePath: details.FileReplacementSourcePath,
                IsEffectiveReplacement: true));
        }

        if (!string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceAssetName) ||
            !string.IsNullOrWhiteSpace(details.WorkspaceReplacementSourceBundleDisplayName))
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.WorkspaceReplacement,
                CreatedAtUtc: DateTimeOffset.MinValue,
                SourceBundleDisplayName: details.WorkspaceReplacementSourceBundleDisplayName,
                SourceAssetName: details.WorkspaceReplacementSourceAssetName,
                IsEffectiveReplacement: true));
        }

        if (details.LastExtractedAtUtc is { } extractedAtUtc)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.Extraction,
                CreatedAtUtc: extractedAtUtc,
                AppliedAtUtc: extractedAtUtc));
        }

        if (details.SpriteEditGeometry is not null &&
            details.SpriteEditAppliedAtUtc is { } spriteEditAppliedAtUtc)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.SpriteEdit,
                CreatedAtUtc: spriteEditAppliedAtUtc,
                AppliedAtUtc: spriteEditAppliedAtUtc,
                IsEffectiveEdit: true));
        }

        return records;
    }

    private InspectorChangeRecordViewModel CreateChangeRecord(
        AssetChangeRecordDto record,
        bool showUndo,
        bool showRedo,
        bool showDelete,
        bool isActionEnabled)
    {
        return new InspectorChangeRecordViewModel(
            OperationId: record.OperationId ?? string.Empty,
            TitleText: BuildTitleText(record),
            BadgeText: BuildBadgeText(record),
            SourceText: BuildSourceText(record),
            TimeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorChangeRecordTimeFormat,
                FormatTimestamp(record.DisplayTimestampUtc)),
            SecondaryText: BuildSecondaryText(record),
            ShowsUndoReplacement: showUndo,
            ShowsRedoReplacement: showRedo,
            ShowsDeleteRecord: showDelete,
            IsActionEnabled: isActionEnabled,
            UndoButtonText: GetString(ResourceKeys.InspectorUndoReplacementButton),
            RedoButtonText: GetString(ResourceKeys.InspectorRedoReplacementButton),
            DeleteButtonText: GetString(ResourceKeys.InspectorDeleteChangeRecordButton),
            Glyph: BuildGlyph(record));
    }

    private string BuildTitleText(AssetChangeRecordDto record)
    {
        return record.Kind switch
        {
            AssetChangeRecordKind.FileReplacement => GetString(ResourceKeys.InspectorChangeRecordFileReplacementTitle),
            AssetChangeRecordKind.AssociatedFileReplacement => GetString(ResourceKeys.InspectorChangeRecordAssociatedFileReplacementTitle),
            AssetChangeRecordKind.WorkspaceReplacement => GetString(ResourceKeys.InspectorChangeRecordWorkspaceReplacementTitle),
            AssetChangeRecordKind.SpriteEdit => GetString(ResourceKeys.InspectorChangeRecordSpriteEditTitle),
            AssetChangeRecordKind.Extraction => GetString(ResourceKeys.InspectorChangeRecordExtractionTitle),
            _ => GetString(ResourceKeys.InspectorChangeSummaryNone)
        };
    }

    private string BuildBadgeText(AssetChangeRecordDto record)
    {
        if (IsReplacementRecord(record))
        {
            return record.IsEffectiveReplacement
                ? GetString(ResourceKeys.InspectorChangeRecordEffectiveBadge)
                : GetString(ResourceKeys.InspectorChangeRecordRedoBadge);
        }

        if (record.Kind == AssetChangeRecordKind.SpriteEdit)
        {
            return record.IsEffectiveEdit
                ? GetString(ResourceKeys.InspectorChangeRecordEffectiveBadge)
                : string.Empty;
        }

        return record.Kind == AssetChangeRecordKind.Extraction
            ? GetString(ResourceKeys.InspectorChangeRecordLatestExtractionBadge)
            : string.Empty;
    }

    private string BuildSourceText(AssetChangeRecordDto record)
    {
        var source = record.Kind switch
        {
            AssetChangeRecordKind.FileReplacement or AssetChangeRecordKind.AssociatedFileReplacement => ResolveDisplayValue(record.SourceFileName ?? record.SourceFilePath),
            AssetChangeRecordKind.WorkspaceReplacement => string.Concat(
                ResolveDisplayValue(record.SourceBundleDisplayName),
                " / ",
                ResolveDisplayValue(record.SourceAssetName)),
            AssetChangeRecordKind.SpriteEdit => GetString(ResourceKeys.InspectorChangeRecordSpriteEditSource),
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(source)
            ? string.Empty
            : _localizationService.GetFormattedString(ResourceKeys.InspectorChangeRecordSourceFormat, source);
    }

    private string BuildSecondaryText(AssetChangeRecordDto record)
    {
        if (record.Kind == AssetChangeRecordKind.FileReplacement)
        {
            return record.SourceFilePath?.Trim() ?? string.Empty;
        }

        if (record.Kind == AssetChangeRecordKind.AssociatedFileReplacement)
        {
            var target = ResolveDisplayValue(record.TargetAssetName ?? record.TargetAssetId);
            return _localizationService.GetFormattedString(ResourceKeys.InspectorChangeRecordTargetFormat, target);
        }

        return string.Empty;
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
        record.Kind is AssetChangeRecordKind.FileReplacement or
            AssetChangeRecordKind.WorkspaceReplacement or
            AssetChangeRecordKind.AssociatedFileReplacement;

    private static string BuildGlyph(AssetChangeRecordDto record) =>
        record.Kind switch
        {
            AssetChangeRecordKind.FileReplacement => "\uE8B7",
            AssetChangeRecordKind.AssociatedFileReplacement => "\uE91B",
            AssetChangeRecordKind.WorkspaceReplacement => "\uE8A5",
            AssetChangeRecordKind.SpriteEdit => "\uE70F",
            AssetChangeRecordKind.Extraction => "\uE896",
            _ => "\uE946"
        };

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
