using System.Collections.ObjectModel;
using System.Linq;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 把检查器基础信息与预览补充状态投影为信息卡集合。
/// </summary>
internal sealed class InspectorInformationCardPresenter
{
    private const string BasicInfoCardKey = "basic";
    private const string PreviewInfoCardKey = "preview-info";
    private const string PreviewDiagnosticCardKey = "preview-diagnostic";

    private readonly InspectorInformationTextSet _informationTextSet;

    public InspectorInformationCardPresenter(InspectorInformationTextSet informationTextSet)
    {
        _informationTextSet = informationTextSet ?? throw new ArgumentNullException(nameof(informationTextSet));
    }

    public void RebuildInformationCards(
        ObservableCollection<InspectorInfoCardViewModel> informationCards,
        bool hasSelection,
        string assetName,
        string assetTypeText,
        string bundleDisplayName,
        string sizeText,
        string containerPath,
        string serializedFileName,
        string pathIdText,
        InspectorPreviewViewModel preview)
    {
        ArgumentNullException.ThrowIfNull(informationCards);
        ArgumentNullException.ThrowIfNull(preview);

        var expandedStates = CaptureCardExpandedStates(informationCards);
        informationCards.Clear();

        if (!hasSelection)
        {
            return;
        }

        informationCards.Add(CreateBasicInfoCard(
            assetName,
            assetTypeText,
            bundleDisplayName,
            sizeText,
            containerPath,
            serializedFileName,
            pathIdText,
            expandedStates));

        var previewInfoRows = preview.PreviewInfoRows;
        if (previewInfoRows.Count > 0)
        {
            informationCards.Add(CreateCard(
                key: PreviewInfoCardKey,
                title: _informationTextSet.PreviewInfoTitle,
                rows: previewInfoRows,
                expandedStates: expandedStates,
                defaultExpanded: true));
        }

        foreach (var previewCard in preview.AdditionalInformationCards)
        {
            ApplyExpandedState(previewCard, expandedStates);
            informationCards.Add(previewCard);
        }

        var diagnosticRows = CreatePreviewDiagnosticRows(
            preview.PreviewCacheStatusText,
            preview.PreviewFallbackReasonText,
            preview.PreviewDiagnosticDetailText);
        if (diagnosticRows.Count > 0)
        {
            informationCards.Add(CreateCard(
                key: PreviewDiagnosticCardKey,
                title: _informationTextSet.PreviewDiagnosticTitle,
                rows: diagnosticRows,
                expandedStates: expandedStates,
                defaultExpanded: true));
        }
    }

    private static Dictionary<string, bool> CaptureCardExpandedStates(
        IEnumerable<InspectorInfoCardViewModel> informationCards)
    {
        return informationCards.ToDictionary(
            static card => card.Key,
            static card => card.IsExpanded,
            StringComparer.OrdinalIgnoreCase);
    }

    private InspectorInfoCardViewModel CreateBasicInfoCard(
        string assetName,
        string assetTypeText,
        string bundleDisplayName,
        string sizeText,
        string containerPath,
        string serializedFileName,
        string pathIdText,
        IReadOnlyDictionary<string, bool> expandedStates)
    {
        var rows = new List<InspectorInfoRowViewModel>
        {
            new(_informationTextSet.AssetNameLabel, assetName),
            new(_informationTextSet.AssetTypeLabel, assetTypeText),
            new(_informationTextSet.BundleLabel, bundleDisplayName),
            new(_informationTextSet.AssetSizeLabel, sizeText)
        };

        if (!string.IsNullOrWhiteSpace(containerPath))
        {
            rows.Add(new InspectorInfoRowViewModel(_informationTextSet.ContainerPathLabel, containerPath));
        }

        if (!string.IsNullOrWhiteSpace(serializedFileName))
        {
            rows.Add(new InspectorInfoRowViewModel(_informationTextSet.SerializedFileLabel, serializedFileName));
        }

        if (!string.IsNullOrWhiteSpace(pathIdText))
        {
            rows.Add(new InspectorInfoRowViewModel(_informationTextSet.PathIdLabel, pathIdText));
        }

        return CreateCard(
            key: BasicInfoCardKey,
            title: _informationTextSet.BasicInfoTitle,
            rows: rows,
            expandedStates: expandedStates,
            defaultExpanded: true);
    }

    private InspectorInfoCardViewModel CreateCard(
        string key,
        string title,
        IReadOnlyList<InspectorInfoRowViewModel> rows,
        IReadOnlyDictionary<string, bool> expandedStates,
        bool defaultExpanded)
    {
        var isExpanded = expandedStates.TryGetValue(key, out var existingExpanded)
            ? existingExpanded
            : defaultExpanded;

        return new InspectorInfoCardViewModel(
            key: key,
            title: title,
            copyToolTipText: _informationTextSet.CopyToolTipText,
            summaryText: _informationTextSet.FormatItemCount(rows.Count),
            isExpanded: isExpanded,
            rows: rows);
    }

    private static void ApplyExpandedState(
        InspectorInfoCardViewModel card,
        IReadOnlyDictionary<string, bool> expandedStates)
    {
        if (expandedStates.TryGetValue(card.Key, out var existingExpanded))
        {
            card.IsExpanded = existingExpanded;
        }
    }

    private List<InspectorInfoRowViewModel> CreatePreviewDiagnosticRows(
        string previewCacheStatusText,
        string previewFallbackReasonText,
        string previewDiagnosticDetailText)
    {
        var rows = new List<InspectorInfoRowViewModel>();

        AddRowIfHasValue(rows, _informationTextSet.PreviewCacheStatusLabel, previewCacheStatusText);
        AddRowIfHasValue(rows, _informationTextSet.PreviewFallbackReasonLabel, previewFallbackReasonText);
        AddRowIfHasValue(rows, _informationTextSet.PreviewDiagnosticDetailLabel, previewDiagnosticDetailText);

        return rows;
    }

    private static void AddRowIfHasValue(ICollection<InspectorInfoRowViewModel> rows, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        rows.Add(new InspectorInfoRowViewModel(label, value));
    }
}
