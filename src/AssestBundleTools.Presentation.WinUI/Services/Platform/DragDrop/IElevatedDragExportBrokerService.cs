namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IElevatedDragExportBrokerService
{
    Task<ElevatedDragExportBrokerResult> EnsureStartedAsync();

    Task<ElevatedDragExportBrokerResult> StartDragAsync(
        ElevatedDragExportDescriptor descriptor,
        Func<Task<IReadOnlyList<string>?>> preparePathsAsync);
}

internal readonly record struct ElevatedDragExportDescriptor(
    string DisplayName,
    int ItemCount);

internal readonly record struct ElevatedDragExportBrokerResult(
    bool Succeeded,
    string DiagnosticDetail)
{
    public static ElevatedDragExportBrokerResult Success(string diagnosticDetail) =>
        new(true, diagnosticDetail);

    public static ElevatedDragExportBrokerResult Failure(string diagnosticDetail) =>
        new(false, diagnosticDetail);
}
