using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IElevatedDropCoordinateMapper
{
    bool TryMapToRoot(
        Window sourceWindow,
        ElevatedNativeDropPosition nativePosition,
        out ElevatedDropRootCoordinateMapping mapping);

    bool TryMapToElement(
        ElevatedDropRootCoordinateMapping rootMapping,
        FrameworkElement targetElement,
        out Point localPosition);
}

internal sealed class ElevatedDropCoordinateMapper : IElevatedDropCoordinateMapper
{
    public bool TryMapToRoot(
        Window sourceWindow,
        ElevatedNativeDropPosition nativePosition,
        out ElevatedDropRootCoordinateMapping mapping)
    {
        mapping = default;
        if (sourceWindow.Content is not FrameworkElement rootElement ||
            rootElement.XamlRoot is null)
        {
            return false;
        }

        if (!TryValidateClientPoint(nativePosition, out var validationMode))
        {
            return false;
        }

        var rootWidth = rootElement.ActualWidth > 0d
            ? rootElement.ActualWidth
            : rootElement.XamlRoot.Size.Width;
        var rootHeight = rootElement.ActualHeight > 0d
            ? rootElement.ActualHeight
            : rootElement.XamlRoot.Size.Height;
        if (!TryCalculateRootPosition(
                nativePosition,
                rootWidth,
                rootHeight,
                rootElement.XamlRoot.RasterizationScale,
                out var rootPosition,
                out var scaleX,
                out var scaleY))
        {
            return false;
        }

        mapping = new ElevatedDropRootCoordinateMapping(
            rootElement,
            rootPosition,
            scaleX,
            scaleY,
            validationMode);
        return true;
    }

    public bool TryMapToElement(
        ElevatedDropRootCoordinateMapping rootMapping,
        FrameworkElement targetElement,
        out Point localPosition)
    {
        localPosition = default;
        if (!targetElement.IsLoaded ||
            targetElement.Visibility != Visibility.Visible ||
            targetElement.XamlRoot is null ||
            !ReferenceEquals(targetElement.XamlRoot, rootMapping.RootElement.XamlRoot))
        {
            return false;
        }

        try
        {
            localPosition = rootMapping.RootElement
                .TransformToVisual(targetElement)
                .TransformPoint(rootMapping.RootPosition);
            return double.IsFinite(localPosition.X) && double.IsFinite(localPosition.Y);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool TryCalculateRootPosition(
        ElevatedNativeDropPosition nativePosition,
        double rootWidth,
        double rootHeight,
        double rasterizationScale,
        out Point rootPosition,
        out double scaleX,
        out double scaleY)
    {
        rootPosition = default;
        scaleX = 0d;
        scaleY = 0d;
        if (rootWidth <= 0d || rootHeight <= 0d)
        {
            return false;
        }

        var fallbackScale = double.IsFinite(rasterizationScale) && rasterizationScale > 0d
            ? rasterizationScale
            : 1d;
        scaleX = nativePosition.ClientWidth > 0
            ? nativePosition.ClientWidth / rootWidth
            : fallbackScale;
        scaleY = nativePosition.ClientHeight > 0
            ? nativePosition.ClientHeight / rootHeight
            : fallbackScale;
        if (!double.IsFinite(scaleX) || scaleX <= 0d ||
            !double.IsFinite(scaleY) || scaleY <= 0d)
        {
            return false;
        }

        rootPosition = new Point(
            nativePosition.ClientX / scaleX,
            nativePosition.ClientY / scaleY);
        return double.IsFinite(rootPosition.X) && double.IsFinite(rootPosition.Y);
    }

    internal static bool TryValidateClientPoint(
        ElevatedNativeDropPosition nativePosition,
        out ElevatedDropCoordinateValidationMode validationMode)
    {
        validationMode = default;
        var hasClientBounds = nativePosition.ClientWidth > 0 && nativePosition.ClientHeight > 0;
        if (hasClientBounds)
        {
            var isWithinClientBounds =
                nativePosition.ClientX >= 0 &&
                nativePosition.ClientX < nativePosition.ClientWidth &&
                nativePosition.ClientY >= 0 &&
                nativePosition.ClientY < nativePosition.ClientHeight;
            if (!isWithinClientBounds)
            {
                return false;
            }

            validationMode = nativePosition.IsClientArea
                ? ElevatedDropCoordinateValidationMode.ClientBounds
                : ElevatedDropCoordinateValidationMode.ClientBoundsFallback;
            return true;
        }

        if (!nativePosition.IsClientArea)
        {
            return false;
        }

        validationMode = ElevatedDropCoordinateValidationMode.NativeClientAreaFlag;
        return true;
    }
}

internal readonly record struct ElevatedDropRootCoordinateMapping(
    FrameworkElement RootElement,
    Point RootPosition,
    double ScaleX,
    double ScaleY,
    ElevatedDropCoordinateValidationMode ValidationMode);
