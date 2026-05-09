using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Vortice.DXGI;
using WinRT;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class WinUI3SwapChainPanelNative
{
    internal static readonly Guid Iid =
        new("63aad0b8-7c24-40ff-85a8-640d944cc325");

    internal const int SetSwapChainVtableIndex = 3;

    private const string NativeObjectStrategy = "winrt-native-object-query-interface";
    private const string MarshalInterfaceStrategy = "marshal-interface-query-interface";

    public static void SetSwapChain(
        SwapChainPanel panel,
        IDXGISwapChain swapChain,
        D3D11SpineRenderDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(swapChain);

        diagnostics?.NativePanelBindingStarted(
            panel.GetType().FullName ?? panel.GetType().Name,
            panel.IsLoaded,
            panel.XamlRoot is not null,
            panel.ActualWidth,
            panel.ActualHeight);

        Exception? lastException;
        if (TrySetSwapChain(
            NativeObjectStrategy,
            () => SetSwapChainFromNativeObject(panel, swapChain),
            diagnostics,
            out lastException))
        {
            return;
        }

        if (TrySetSwapChain(
            MarshalInterfaceStrategy,
            () => SetSwapChainFromMarshalInterface(panel, swapChain),
            diagnostics,
            out lastException))
        {
            return;
        }

        ExceptionDispatchInfo.Capture(lastException).Throw();
    }

    private static bool TrySetSwapChain(
        string strategy,
        Action setSwapChain,
        D3D11SpineRenderDiagnostics? diagnostics,
        out Exception exception)
    {
        try
        {
            setSwapChain();
            diagnostics?.NativePanelBound(strategy);
            exception = null!;
            return true;
        }
        catch (Exception caught)
        {
            diagnostics?.NativePanelBindingFailed(strategy, caught);
            exception = caught;
            return false;
        }
    }

    private static void SetSwapChainFromNativeObject(SwapChainPanel panel, IDXGISwapChain swapChain)
    {
        if (panel is not IWinRTObject winrtObject)
        {
            throw new InvalidCastException("SwapChainPanel does not expose a WinRT native object.");
        }

        var nativeObject = winrtObject.NativeObject;
        if (nativeObject.ThisPtr == IntPtr.Zero)
        {
            throw new InvalidCastException("SwapChainPanel exposes an empty WinRT native object.");
        }

        SetSwapChainFromQueryInterface(nativeObject.ThisPtr, swapChain);
    }

    private static void SetSwapChainFromMarshalInterface(SwapChainPanel panel, IDXGISwapChain swapChain)
    {
        var panelAbi = MarshalInterface<SwapChainPanel>.FromManaged(panel);
        try
        {
            SetSwapChainFromQueryInterface(panelAbi, swapChain);
        }
        finally
        {
            MarshalInterface<SwapChainPanel>.DisposeAbi(panelAbi);
        }
    }

    private static void SetSwapChainFromQueryInterface(IntPtr sourceObject, IDXGISwapChain swapChain)
    {
        if (sourceObject == IntPtr.Zero)
        {
            throw new InvalidCastException("SwapChainPanel native source pointer is empty.");
        }

        var iid = Iid;
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(sourceObject, ref iid, out var nativePanel));
        try
        {
            SetSwapChain(nativePanel, swapChain.NativePointer);
        }
        finally
        {
            Marshal.Release(nativePanel);
        }
    }

    private static void SetSwapChain(IntPtr nativePanel, IntPtr swapChain)
    {
        var vtable = Marshal.ReadIntPtr(nativePanel);
        var methodPointer = Marshal.ReadIntPtr(vtable, SetSwapChainVtableIndex * IntPtr.Size);
        var setSwapChain = Marshal.GetDelegateForFunctionPointer<SetSwapChainDelegate>(methodPointer);
        Marshal.ThrowExceptionForHR(setSwapChain(nativePanel, swapChain));
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetSwapChainDelegate(IntPtr nativePanel, IntPtr swapChain);
}
