using System.Diagnostics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationPreviewRenderBackendFactory
{
    public static ISpineAnimationPreviewRenderBackend Create(
        CanvasControl win2DCanvas,
        SwapChainPanel d3DPanel)
    {
        ArgumentNullException.ThrowIfNull(win2DCanvas);
        ArgumentNullException.ThrowIfNull(d3DPanel);

        if (SpineAnimationPreviewRenderBackendSelector.ResolvePreferredKind() ==
            SpineAnimationPreviewRenderBackendKind.D3D11)
        {
            var d3DBackend = TryCreateD3D11Backend(win2DCanvas, d3DPanel);
            if (d3DBackend is not null)
            {
                return d3DBackend;
            }
        }

        d3DPanel.Visibility = Visibility.Collapsed;
        win2DCanvas.Visibility = Visibility.Visible;
        return new Win2DSpineAnimationPreviewRenderBackend(win2DCanvas);
    }

    private static ISpineAnimationPreviewRenderBackend? TryCreateD3D11Backend(
        CanvasControl win2DCanvas,
        SwapChainPanel d3DPanel)
    {
        try
        {
            var diagnostics = CreateD3D11Diagnostics();
            var d3DBackend = new D3D11SpineAnimationPreviewRenderBackend(d3DPanel, diagnostics);
            var win2DBackend = new Win2DSpineAnimationPreviewRenderBackend(win2DCanvas);
            var backend = new SpineAnimationFallbackRenderBackend(
                d3DBackend,
                win2DBackend,
                usePrimary =>
                {
                    win2DCanvas.Visibility = usePrimary ? Visibility.Collapsed : Visibility.Visible;
                    d3DPanel.Visibility = usePrimary ? Visibility.Visible : Visibility.Collapsed;
                });
            d3DBackend.RenderingFailed += (_, _) => backend.SwitchToFallback();
            return backend;
        }
        catch
        {
            d3DPanel.Visibility = Visibility.Collapsed;
            win2DCanvas.Visibility = Visibility.Visible;
            return null;
        }
    }

    private static D3D11SpineRenderDiagnostics? CreateD3D11Diagnostics() =>
        SpineAnimationPreviewRenderBackendSelector.ResolveD3D11DiagnosticsEnabled()
            ? new D3D11SpineRenderDiagnostics(static message => Debug.WriteLine(message))
            : null;
}
