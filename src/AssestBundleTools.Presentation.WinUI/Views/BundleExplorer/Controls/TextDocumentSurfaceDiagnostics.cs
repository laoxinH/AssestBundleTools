using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class TextDocumentSurfaceDiagnostics
{
    public static string BuildFallbackDiagnostic(
        string descriptionResourceKey,
        string description,
        string? detail,
        TextDocumentWebViewRuntimeProbe? runtimeProbe,
        ElementTheme actualTheme,
        string? documentLanguage,
        bool isReadOnly,
        int documentTextLength)
    {
        runtimeProbe ??= TextDocumentWebViewEnvironmentProvider.ProbeRuntime();
        var monacoRoot = TextDocumentWebViewEnvironmentProvider.ResolveMonacoAssetRoot();
        var userDataFolder = TextDocumentWebViewEnvironmentProvider.ResolveUserDataFolder();
        return new TextDocumentSurfaceDiagnosticBuilder()
            .Add("ReasonKey", descriptionResourceKey)
            .Add("Detail", detail)
            .AddRuntimeProbe(runtimeProbe)
            .Add("MonacoAssetRoot", monacoRoot)
            .Add("MonacoAssetsExist", Directory.Exists(monacoRoot))
            .Add("WebViewUserDataFolder", userDataFolder)
            .Add("WebViewUserDataFolderExists", Directory.Exists(userDataFolder))
            .Add("ActualTheme", actualTheme)
            .Add("DocumentLanguage", documentLanguage)
            .Add("IsReadOnly", isReadOnly)
            .Add("DocumentTextLength", documentTextLength)
            .Build(description, detail);
    }
}
