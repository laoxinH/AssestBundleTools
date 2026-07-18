using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal static partial class NativeOleDragBitmapRenderer
{
    private const string FontFaceName = "Segoe UI Variable Text";
    private const int TransparentBackgroundMode = 1;
    private const int PenStyleSolid = 0;
    private const int FontWeightNormal = 400;
    private const int FontWeightSemiBold = 600;
    private const uint ClearTypeQuality = 5;
    private const uint DrawTextCenter = 0x00000001;
    private const uint DrawTextVerticalCenter = 0x00000004;
    private const uint DrawTextSingleLine = 0x00000020;
    private const uint DrawTextNoPrefix = 0x00000800;
    private const uint DrawTextEndEllipsis = 0x00008000;
    private const double CardLeft = 5d;
    private const double CardTop = 4d;
    private const double CardRight = NativeOleDragImage.ImageWidth - 5d;
    private const double CardBottom = NativeOleDragImage.ImageHeight - 8d;
    private const double CardRadius = 10d;
    private const double ShadowOffsetY = 2d;
    private const double ShadowBlurRadius = 4d;
    private const double ShadowOpacity = 0.14d;

    public static IntPtr Create(
        string displayName,
        int itemCount,
        NativeOleDragVisualTheme theme,
        bool premultiplyAlpha)
    {
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Acquiring the screen device context failed.");
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previousBitmap = IntPtr.Zero;
        var succeeded = false;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            bitmap = CreateDragDib(screenDc, out var pixels);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero || pixels == IntPtr.Zero)
            {
                throw new InvalidOperationException("Creating the shell drag bitmap failed.");
            }

            var alphaMask = RenderCardSurface(pixels, theme, itemCount);
            previousBitmap = SelectObject(memoryDc, bitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
            {
                throw new InvalidOperationException("Selecting the shell drag bitmap failed.");
            }

            DrawContent(memoryDc, displayName, itemCount, theme);
            ApplyAlpha(pixels, alphaMask, premultiplyAlpha);
            succeeded = true;
            return bitmap;
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                _ = SelectObject(memoryDc, previousBitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                _ = DeleteDC(memoryDc);
            }

            _ = ReleaseDC(IntPtr.Zero, screenDc);
            if (!succeeded && bitmap != IntPtr.Zero)
            {
                _ = DeleteObject(bitmap);
            }
        }
    }

    private static byte[] RenderCardSurface(
        IntPtr pixels,
        NativeOleDragVisualTheme theme,
        int itemCount)
    {
        var alphaMask = new byte[NativeOleDragImage.ImageWidth * NativeOleDragImage.ImageHeight];
        for (var y = 0; y < NativeOleDragImage.ImageHeight; y++)
        {
            for (var x = 0; x < NativeOleDragImage.ImageWidth; x++)
            {
                var pixelIndex = (y * NativeOleDragImage.ImageWidth) + x;
                var pixel = ComposeSurfacePixel(x + 0.5d, y + 0.5d, theme);
                alphaMask[pixelIndex] = pixel.Alpha;
                Marshal.WriteInt32(pixels, pixelIndex * sizeof(int), unchecked((int)pixel.Color));
            }
        }

        DrawRoundedFill(
            pixels,
            alphaMask,
            left: 14,
            top: 16,
            right: 52,
            bottom: 54,
            radius: 8,
            theme.IconSurface);
        if (itemCount > 1)
        {
            DrawRoundedFill(
                pixels,
                alphaMask,
                left: NativeOleDragImage.ImageWidth - 55,
                top: 21,
                right: NativeOleDragImage.ImageWidth - 15,
                bottom: 49,
                radius: 14,
                theme.BadgeSurface);
        }

        return alphaMask;
    }

    private static PixelValue ComposeSurfacePixel(
        double x,
        double y,
        NativeOleDragVisualTheme theme)
    {
        var shadowDistance = SignedDistanceToRoundedRectangle(
            x,
            y,
            CardLeft,
            CardTop + ShadowOffsetY,
            CardRight,
            CardBottom + ShadowOffsetY,
            CardRadius);
        var shadowAlpha = shadowDistance > ShadowBlurRadius
            ? 0d
            : Math.Exp(-Math.Pow(Math.Max(shadowDistance, 0d), 2d) / 5d) * ShadowOpacity;
        var outerCoverage = Coverage(SignedDistanceToRoundedRectangle(
            x,
            y,
            CardLeft,
            CardTop,
            CardRight,
            CardBottom,
            CardRadius));
        var innerCoverage = Coverage(SignedDistanceToRoundedRectangle(
            x,
            y,
            CardLeft + 1d,
            CardTop + 1d,
            CardRight - 1d,
            CardBottom - 1d,
            CardRadius - 1d));

        var composite = new PremultipliedPixel();
        composite.Composite(theme.Shadow, shadowAlpha);
        composite.Composite(theme.CardBorder, outerCoverage);
        composite.Composite(theme.CardSurface, innerCoverage);
        return composite.ToStraightPixel();
    }

    private static void DrawRoundedFill(
        IntPtr pixels,
        byte[] alphaMask,
        int left,
        int top,
        int right,
        int bottom,
        int radius,
        uint color)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var coverage = Coverage(SignedDistanceToRoundedRectangle(
                    x + 0.5d,
                    y + 0.5d,
                    left,
                    top,
                    right,
                    bottom,
                    radius));
                if (coverage <= 0d)
                {
                    continue;
                }

                var pixelIndex = (y * NativeOleDragImage.ImageWidth) + x;
                var existing = unchecked((uint)Marshal.ReadInt32(pixels, pixelIndex * sizeof(int)));
                var blended = BlendRgb(existing & 0x00FFFFFF, color, coverage);
                Marshal.WriteInt32(
                    pixels,
                    pixelIndex * sizeof(int),
                    unchecked((int)(blended | ((uint)alphaMask[pixelIndex] << 24))));
            }
        }
    }

    private static void DrawContent(
        IntPtr dc,
        string displayName,
        int itemCount,
        NativeOleDragVisualTheme theme)
    {
        _ = SetBkMode(dc, TransparentBackgroundMode);
        DrawFileGlyph(dc, theme.IconGlyph);

        var nameFont = CreateFontW(
            -16,
            0,
            0,
            0,
            FontWeightNormal,
            0,
            0,
            0,
            1,
            0,
            0,
            ClearTypeQuality,
            0,
            FontFaceName);
        var previousFont = SelectObject(dc, nameFont);
        _ = SetTextColor(dc, ToColorRef(theme.PrimaryText));
        var nameRight = itemCount > 1
            ? NativeOleDragImage.ImageWidth - 66
            : NativeOleDragImage.ImageWidth - 18;
        var nameRect = new NativeRect(64, 11, nameRight, 59);
        _ = DrawTextW(
            dc,
            displayName,
            displayName.Length,
            ref nameRect,
            DrawTextSingleLine | DrawTextVerticalCenter | DrawTextEndEllipsis | DrawTextNoPrefix);
        _ = SelectObject(dc, previousFont);
        _ = DeleteObject(nameFont);

        if (itemCount <= 1)
        {
            return;
        }

        var countText = itemCount > 999
            ? "999+"
            : itemCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var badgeFont = CreateFontW(
            -13,
            0,
            0,
            0,
            FontWeightSemiBold,
            0,
            0,
            0,
            1,
            0,
            0,
            ClearTypeQuality,
            0,
            FontFaceName);
        previousFont = SelectObject(dc, badgeFont);
        _ = SetTextColor(dc, ToColorRef(theme.BadgeText));
        var countRect = new NativeRect(
            NativeOleDragImage.ImageWidth - 55,
            21,
            NativeOleDragImage.ImageWidth - 15,
            49);
        _ = DrawTextW(
            dc,
            countText,
            countText.Length,
            ref countRect,
            DrawTextSingleLine | DrawTextVerticalCenter | DrawTextNoPrefix | DrawTextCenter);
        _ = SelectObject(dc, previousFont);
        _ = DeleteObject(badgeFont);
    }

    private static void DrawFileGlyph(IntPtr dc, uint color)
    {
        var pen = CreatePen(PenStyleSolid, 2, ToColorRef(color));
        var previousPen = SelectObject(dc, pen);
        _ = MoveToEx(dc, 27, 24, IntPtr.Zero);
        _ = LineTo(dc, 36, 24);
        _ = LineTo(dc, 43, 31);
        _ = LineTo(dc, 43, 46);
        _ = LineTo(dc, 27, 46);
        _ = LineTo(dc, 27, 24);
        _ = MoveToEx(dc, 36, 24, IntPtr.Zero);
        _ = LineTo(dc, 36, 31);
        _ = LineTo(dc, 43, 31);
        _ = SelectObject(dc, previousPen);
        _ = DeleteObject(pen);
    }

    private static void ApplyAlpha(
        IntPtr pixels,
        byte[] alphaMask,
        bool premultiplyAlpha)
    {
        for (var index = 0; index < alphaMask.Length; index++)
        {
            var offset = index * sizeof(int);
            var color = unchecked((uint)Marshal.ReadInt32(pixels, offset));
            var alpha = alphaMask[index];
            var red = (color >> 16) & 0xFF;
            var green = (color >> 8) & 0xFF;
            var blue = color & 0xFF;
            if (premultiplyAlpha && alpha < byte.MaxValue)
            {
                red = (red * alpha + 127) / 255;
                green = (green * alpha + 127) / 255;
                blue = (blue * alpha + 127) / 255;
            }

            Marshal.WriteInt32(
                pixels,
                offset,
                unchecked((int)(((uint)alpha << 24) | (red << 16) | (green << 8) | blue)));
        }
    }

    private static IntPtr CreateDragDib(IntPtr screenDc, out IntPtr pixels)
    {
        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = checked((uint)Marshal.SizeOf<BitmapInfoHeader>()),
                Width = NativeOleDragImage.ImageWidth,
                Height = -NativeOleDragImage.ImageHeight,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
                SizeImage = checked((uint)(
                    NativeOleDragImage.ImageWidth *
                    NativeOleDragImage.ImageHeight *
                    sizeof(int)))
            }
        };
        return CreateDIBSection(
            screenDc,
            ref bitmapInfo,
            usage: 0,
            out pixels,
            IntPtr.Zero,
            offset: 0);
    }

    private static double SignedDistanceToRoundedRectangle(
        double x,
        double y,
        double left,
        double top,
        double right,
        double bottom,
        double radius)
    {
        var centerX = (left + right) / 2d;
        var centerY = (top + bottom) / 2d;
        var halfWidth = (right - left) / 2d;
        var halfHeight = (bottom - top) / 2d;
        var horizontal = Math.Abs(x - centerX) - (halfWidth - radius);
        var vertical = Math.Abs(y - centerY) - (halfHeight - radius);
        var outside = Math.Sqrt(
            Math.Pow(Math.Max(horizontal, 0d), 2d) +
            Math.Pow(Math.Max(vertical, 0d), 2d));
        return outside + Math.Min(Math.Max(horizontal, vertical), 0d) - radius;
    }

    private static double Coverage(double signedDistance) =>
        Math.Clamp(0.5d - signedDistance, 0d, 1d);

    private static uint BlendRgb(uint background, uint foreground, double amount)
    {
        var inverse = 1d - amount;
        var red = (uint)Math.Round((((background >> 16) & 0xFF) * inverse) + (((foreground >> 16) & 0xFF) * amount));
        var green = (uint)Math.Round((((background >> 8) & 0xFF) * inverse) + (((foreground >> 8) & 0xFF) * amount));
        var blue = (uint)Math.Round(((background & 0xFF) * inverse) + ((foreground & 0xFF) * amount));
        return (red << 16) | (green << 8) | blue;
    }

    private static uint ToColorRef(uint rgb) =>
        ((rgb & 0x0000FF) << 16) | (rgb & 0x00FF00) | ((rgb & 0xFF0000) >> 16);

    private readonly record struct PixelValue(uint Color, byte Alpha);

    private struct PremultipliedPixel
    {
        private double _red;
        private double _green;
        private double _blue;
        private double _alpha;

        public void Composite(uint color, double alpha)
        {
            alpha = Math.Clamp(alpha, 0d, 1d);
            var inverse = 1d - alpha;
            _red = (((color >> 16) & 0xFF) / 255d * alpha) + (_red * inverse);
            _green = (((color >> 8) & 0xFF) / 255d * alpha) + (_green * inverse);
            _blue = ((color & 0xFF) / 255d * alpha) + (_blue * inverse);
            _alpha = alpha + (_alpha * inverse);
        }

        public readonly PixelValue ToStraightPixel()
        {
            if (_alpha <= 0d)
            {
                return new PixelValue(NativeOleDragImage.TransparentColorKey, 0);
            }

            var red = (uint)Math.Clamp(Math.Round(_red / _alpha * 255d), 0d, 255d);
            var green = (uint)Math.Clamp(Math.Round(_green / _alpha * 255d), 0d, 255d);
            var blue = (uint)Math.Clamp(Math.Round(_blue / _alpha * 255d), 0d, 255d);
            var alpha = (byte)Math.Clamp(Math.Round(_alpha * 255d), 0d, 255d);
            return new PixelValue((red << 16) | (green << 8) | blue, alpha);
        }
    }

}
