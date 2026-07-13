namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IUnelevatedProcessRestartService
{
    UnelevatedProcessRestartResult TryRestart(IReadOnlyList<string> arguments);
}

internal readonly record struct UnelevatedProcessRestartResult(
    bool Succeeded,
    int? ProcessId,
    int? NativeErrorCode,
    string DiagnosticDetail)
{
    public static UnelevatedProcessRestartResult Success(int processId)
    {
        return new UnelevatedProcessRestartResult(true, processId, null, string.Empty);
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
