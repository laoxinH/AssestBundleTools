using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class InspectorChangeRecordSelectionResolver
{
    public static IReadOnlyList<string> ResolveDeletableOperationIds(
        InspectorPanelViewModel inspector,
        IReadOnlyList<string>? operationIds)
    {
        ArgumentNullException.ThrowIfNull(inspector);

        var deletableOperationIds = inspector.ChangeRecords
            .Where(static record => record.ShowsDeleteRecord && record.HasOperationId)
            .Select(static record => record.OperationId)
            .ToHashSet(StringComparer.Ordinal);
        var requestedOperationIds = (operationIds ?? [])
            .Where(static operationId => !string.IsNullOrWhiteSpace(operationId))
            .Select(static operationId => operationId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return requestedOperationIds.All(deletableOperationIds.Contains)
            ? requestedOperationIds
            : [];
    }
}
