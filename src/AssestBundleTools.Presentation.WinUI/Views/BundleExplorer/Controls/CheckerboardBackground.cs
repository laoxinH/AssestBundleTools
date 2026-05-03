using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed class CheckerboardBackground : Grid
{
    public static readonly DependencyProperty CellSizeProperty =
        DependencyProperty.Register(
            nameof(CellSize),
            typeof(double),
            typeof(CheckerboardBackground),
            new PropertyMetadata(12d, OnAppearancePropertyChanged));

    public static readonly DependencyProperty LightSquareBrushProperty =
        DependencyProperty.Register(
            nameof(LightSquareBrush),
            typeof(Brush),
            typeof(CheckerboardBackground),
            new PropertyMetadata(null, OnAppearancePropertyChanged));

    public static readonly DependencyProperty DarkSquareBrushProperty =
        DependencyProperty.Register(
            nameof(DarkSquareBrush),
            typeof(Brush),
            typeof(CheckerboardBackground),
            new PropertyMetadata(null, OnAppearancePropertyChanged));

    private readonly Canvas _darkTileCanvas = new();
    private readonly Brush _defaultLightSquareBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 235, 238, 242));
    private readonly Brush _defaultDarkSquareBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 195, 201, 210));

    public CheckerboardBackground()
    {
        IsHitTestVisible = false;
        Children.Add(_darkTileCanvas);
        SizeChanged += OnSizeChanged;
        RebuildTiles();
    }

    public double CellSize
    {
        get => (double)GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public Brush? LightSquareBrush
    {
        get => (Brush?)GetValue(LightSquareBrushProperty);
        set => SetValue(LightSquareBrushProperty, value);
    }

    public Brush? DarkSquareBrush
    {
        get => (Brush?)GetValue(DarkSquareBrushProperty);
        set => SetValue(DarkSquareBrushProperty, value);
    }

    private Brush EffectiveLightSquareBrush => LightSquareBrush ?? _defaultLightSquareBrush;

    private Brush EffectiveDarkSquareBrush => DarkSquareBrush ?? _defaultDarkSquareBrush;

    private static void OnAppearancePropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is CheckerboardBackground background)
        {
            background.RebuildTiles();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        RebuildTiles();
    }

    private void RebuildTiles()
    {
        Background = EffectiveLightSquareBrush;
        _darkTileCanvas.Children.Clear();

        foreach (var tile in CheckerboardTileLayoutBuilder.Build(ActualWidth, ActualHeight, CellSize))
        {
            var rectangle = new Rectangle
            {
                Width = tile.Width,
                Height = tile.Height,
                Fill = EffectiveDarkSquareBrush
            };

            Canvas.SetLeft(rectangle, tile.X);
            Canvas.SetTop(rectangle, tile.Y);
            _darkTileCanvas.Children.Add(rectangle);
        }
    }
}
