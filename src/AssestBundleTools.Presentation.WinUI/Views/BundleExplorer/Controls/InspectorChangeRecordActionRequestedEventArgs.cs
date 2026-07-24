namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed class InspectorChangeRecordActionRequestedEventArgs : EventArgs
{
    public InspectorChangeRecordActionRequestedEventArgs(string? operationId)
        : this(string.IsNullOrWhiteSpace(operationId) ? [] : [operationId], false)
    {
    }

    public InspectorChangeRecordActionRequestedEventArgs(
        IEnumerable<string> operationIds,
        bool isRestoreOriginal = false)
    {
        ArgumentNullException.ThrowIfNull(operationIds);
        OperationIds = operationIds
            .Where(static operationId => !string.IsNullOrWhiteSpace(operationId))
            .Select(static operationId => operationId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        IsRestoreOriginal = isRestoreOriginal;
    }

    public IReadOnlyList<string> OperationIds { get; }

    public string? OperationId => OperationIds.FirstOrDefault();

    public bool IsRestoreOriginal { get; }
}
