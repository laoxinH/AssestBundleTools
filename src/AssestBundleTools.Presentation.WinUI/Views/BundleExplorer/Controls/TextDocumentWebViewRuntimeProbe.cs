namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed record TextDocumentWebViewRuntimeProbe(
    bool IsAvailable,
    string Version,
    string ErrorCode,
    int HResult,
    string Message)
{
    public static TextDocumentWebViewRuntimeProbe Available(string version)
    {
        return new TextDocumentWebViewRuntimeProbe(true, version, string.Empty, 0, string.Empty);
    }

    public static TextDocumentWebViewRuntimeProbe Unavailable(
        string errorCode,
        int hResult,
        string message)
    {
        return new TextDocumentWebViewRuntimeProbe(
            false,
            string.Empty,
            errorCode,
            hResult,
            message);
    }
}
