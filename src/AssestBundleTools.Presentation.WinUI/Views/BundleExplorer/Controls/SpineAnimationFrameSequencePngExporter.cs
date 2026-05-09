using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationFrameSequencePngExporter
{
    private const float RenderTargetDpi = 96f;

    private readonly ICanvasResourceCreator _resourceCreator;

    public SpineAnimationFrameSequencePngExporter(ICanvasResourceCreator resourceCreator)
    {
        _resourceCreator = resourceCreator ?? throw new ArgumentNullException(nameof(resourceCreator));
    }

    public async Task<int> ExportAsync(
        SpineAnimationRuntimeFrameSequenceExportPlanResultDto plan,
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents,
        string outputDirectoryPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(texturePageContents);

        if (!plan.IsHandled ||
            plan.Layout is null ||
            plan.Frames.Count == 0 ||
            string.IsNullOrWhiteSpace(outputDirectoryPath))
        {
            return 0;
        }

        Directory.CreateDirectory(outputDirectoryPath);
        using var textureBitmaps = await LoadTextureBitmapsAsync(texturePageContents, cancellationToken);
        var exportedCount = 0;
        foreach (var frame in plan.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExportFrameAsync(plan.Layout, frame, textureBitmaps.Items, outputDirectoryPath);
            exportedCount++;
        }

        return exportedCount;
    }

    private async Task ExportFrameAsync(
        SpineAnimationRuntimeFrameSequenceExportLayoutDto layout,
        SpineAnimationRuntimeFrameSequenceExportFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        string outputDirectoryPath)
    {
        using var target = new CanvasRenderTarget(
            _resourceCreator,
            layout.CanvasWidth,
            layout.CanvasHeight,
            RenderTargetDpi);
        using (var drawingSession = target.CreateDrawingSession())
        {
            drawingSession.Clear(Color.FromArgb(0, 0, 0, 0));
            InspectorSpineAnimationPreviewRenderer.DrawExport(
                drawingSession,
                frame.Frame,
                textureBitmaps,
                layout);
        }

        await target.SaveAsync(
            Path.Combine(outputDirectoryPath, Path.GetFileName(frame.FileName)),
            CanvasBitmapFileFormat.Png);
    }

    private async Task<SpineExportTextureBitmapSet> LoadTextureBitmapsAsync(
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents,
        CancellationToken cancellationToken)
    {
        var textureBitmaps = new Dictionary<string, CanvasBitmap>(StringComparer.Ordinal);
        try
        {
            foreach (var pair in texturePageContents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    pair.Value.ColorContent.Length == 0)
                {
                    continue;
                }

                var decodedPages = await SpineAnimationTexturePageDecoder.DecodeAsync(
                    new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal)
                    {
                        [pair.Key] = pair.Value
                    });
                if (!decodedPages.TryGetValue(pair.Key, out var decodedPage))
                {
                    continue;
                }

                textureBitmaps[pair.Key] = CanvasBitmap.CreateFromBytes(
                    _resourceCreator,
                    decodedPage.BgraPixels,
                    decodedPage.Width,
                    decodedPage.Height,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96f,
                    CanvasAlphaMode.Premultiplied);
            }

            return new SpineExportTextureBitmapSet(textureBitmaps);
        }
        catch
        {
            foreach (var bitmap in textureBitmaps.Values)
            {
                bitmap.Dispose();
            }

            throw;
        }
    }

    private sealed class SpineExportTextureBitmapSet(
        IReadOnlyDictionary<string, CanvasBitmap> items) : IDisposable
    {
        public IReadOnlyDictionary<string, CanvasBitmap> Items { get; } = items;

        public void Dispose()
        {
            foreach (var bitmap in Items.Values)
            {
                bitmap.Dispose();
            }
        }
    }
}
