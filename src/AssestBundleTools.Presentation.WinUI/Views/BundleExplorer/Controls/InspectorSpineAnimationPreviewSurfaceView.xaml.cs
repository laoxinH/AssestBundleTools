using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView : UserControl
{
    private readonly DispatcherTimer _playbackTimer;
    private readonly Dictionary<string, CanvasBitmap> _textureBitmaps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _textureBitmapSources = new(StringComparer.Ordinal);
    private InspectorPreviewSpineAnimationViewModel? _observedViewModel;
    private DateTimeOffset _lastTickUtc;
    private bool _isLoadingTextures;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewSpineAnimationViewModel),
            typeof(InspectorSpineAnimationPreviewSurfaceView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorSpineAnimationPreviewSurfaceView()
    {
        InitializeComponent();
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _playbackTimer.Tick += OnPlaybackTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedText();
    }

    public InspectorPreviewSpineAnimationViewModel? ViewModel
    {
        get => (InspectorPreviewSpineAnimationViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorSpineAnimationPreviewSurfaceView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPreviewSpineAnimationViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPreviewSpineAnimationViewModel);
        view.ClearTextureBitmaps();
        view.QueueTextureLoad();
        view.UpdateBindingsNow();
        view.InvalidateCanvas();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        QueueTextureLoad();
        _lastTickUtc = DateTimeOffset.UtcNow;
        _playbackTimer.Start();
        UpdateBindingsNow();
        InvalidateCanvas();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _playbackTimer.Stop();
        DetachObservedViewModel(_observedViewModel);
        ClearTextureBitmaps();
    }

    private void ApplyLocalizedText()
    {
        ToolTipService.SetToolTip(
            PlayPauseButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewPlayPauseToolTip));
        ToolTipService.SetToolTip(
            RestartButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewRestartToolTip));
    }

    private void AttachObservedViewModel(InspectorPreviewSpineAnimationViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPreviewSpineAnimationViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InspectorPreviewSpineAnimationViewModel.TexturePageContents))
        {
            ClearTextureBitmaps();
            QueueTextureLoad();
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            UpdateBindingsNow();
            InvalidateCanvas();
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateBindingsNow();
            InvalidateCanvas();
        });
    }

    private void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.TogglePlayback();
        UpdateBindingsNow();
    }

    private void OnRestartButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Restart();
        InvalidateCanvas();
    }

    private void OnPlaybackTimerTick(object? sender, object e)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _lastTickUtc;
        _lastTickUtc = now;
        ViewModel?.Advance(elapsed);
        InvalidateCanvas();
    }

    private void OnSpineCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateCanvas();
    }

    private void OnSpineCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var viewModel = ViewModel;
        if (viewModel?.Preview is null ||
            viewModel.SelectedAnimation is null ||
            _textureBitmaps.Count == 0)
        {
            return;
        }

        var width = (float)Math.Max(0d, sender.ActualWidth);
        var height = (float)Math.Max(0d, sender.ActualHeight);
        if (width <= 1f || height <= 1f)
        {
            return;
        }

        InspectorSpineAnimationPreviewRenderer.Draw(
            args.DrawingSession,
            viewModel.SelectedAnimation,
            _textureBitmaps,
            width,
            height,
            viewModel.PlaybackTime);
    }

    private void QueueTextureLoad()
    {
        if (_isLoadingTextures || ViewModel?.TexturePageContents is not { Count: > 0 })
        {
            return;
        }

        _ = LoadTextureBitmapsAsync(ViewModel.TexturePageContents);
    }

    private async Task LoadTextureBitmapsAsync(IReadOnlyDictionary<string, byte[]> texturePageContents)
    {
        _isLoadingTextures = true;
        try
        {
            foreach (var pair in texturePageContents)
            {
                if (_textureBitmaps.ContainsKey(pair.Key) ||
                    pair.Value.Length == 0)
                {
                    continue;
                }

                using var stream = await CreateTextureStreamAsync(pair.Value);
                var bitmap = await CanvasBitmap.LoadAsync(SpineCanvas, stream);
                if (ViewModel?.TexturePageContents.TryGetValue(pair.Key, out var currentContent) != true ||
                    !ReferenceEquals(currentContent, pair.Value))
                {
                    bitmap.Dispose();
                    continue;
                }

                _textureBitmaps[pair.Key] = bitmap;
                _textureBitmapSources[pair.Key] = pair.Value;
            }

            InvalidateCanvas();
        }
        catch
        {
            ClearTextureBitmaps();
        }
        finally
        {
            _isLoadingTextures = false;
        }
    }

    private static async Task<IRandomAccessStream> CreateTextureStreamAsync(byte[] textureContent)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(textureContent);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return stream;
    }

    private void ClearTextureBitmaps()
    {
        foreach (var bitmap in _textureBitmaps.Values)
        {
            bitmap.Dispose();
        }

        _textureBitmaps.Clear();
        _textureBitmapSources.Clear();
    }

    private void UpdateBindingsNow()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            Bindings.Update();
            return;
        }

        DispatcherQueue.TryEnqueue(UpdateBindingsNow);
    }

    private void InvalidateCanvas()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            SpineCanvas.Invalidate();
            return;
        }

        DispatcherQueue.TryEnqueue(InvalidateCanvas);
    }
}
