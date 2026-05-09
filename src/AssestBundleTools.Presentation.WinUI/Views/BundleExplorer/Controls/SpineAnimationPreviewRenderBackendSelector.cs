namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationPreviewRenderBackendSelector
{
    public const string D3D11SmokeEnvironmentVariable = "ABT_SPINE_D3D11_SMOKE";
    public const string D3D11DiagnosticsEnvironmentVariable = "ABT_SPINE_D3D11_DIAGNOSTICS";

    public static SpineAnimationPreviewRenderBackendKind ResolvePreferredKind(
        Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        var value = readEnvironmentVariable(D3D11SmokeEnvironmentVariable);
        return IsEnabled(value)
            ? SpineAnimationPreviewRenderBackendKind.D3D11Smoke
            : SpineAnimationPreviewRenderBackendKind.Win2D;
    }

    public static bool ResolveD3D11DiagnosticsEnabled(
        Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        return IsEnabled(readEnvironmentVariable(D3D11DiagnosticsEnvironmentVariable));
    }

    private static bool IsEnabled(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "d3d11", StringComparison.OrdinalIgnoreCase);
}
