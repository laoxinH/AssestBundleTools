namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IUnelevatedProcessRestartService
{
    Task<UnelevatedProcessRestartResult> TryRestartAsync(IReadOnlyList<string> arguments);
}

internal readonly record struct UnelevatedProcessRestartResult(
    bool Succeeded,
    int? ProcessId,
    int? NativeErrorCode,
    string DiagnosticDetail)
{
    public static UnelevatedProcessRestartResult Success(
        int processId,
        string diagnosticDetail = "")
    {
        return new UnelevatedProcessRestartResult(true, processId, null, diagnosticDetail);
    }

    public static UnelevatedProcessRestartResult Failure(
        string diagnosticDetail,
        int? nativeErrorCode = null)
    {
        return new UnelevatedProcessRestartResult(
            false,
            null,
            nativeErrorCode,
            diagnosticDetail);
    }
}
