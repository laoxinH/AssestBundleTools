namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed class InspectorChangeRecordActionRequestedEventArgs : EventArgs
{
    public InspectorChangeRecordActionRequestedEventArgs(string? operationId)
    {
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
    }

    public string? OperationId { get; }
}
