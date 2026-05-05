using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 让预览摘要卡片按内容区域宽度自适应两列排布。
/// </summary>
public sealed class TwoColumnSummaryCardPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty =
        DependencyProperty.Register(
            nameof(MinItemWidth),
            typeof(double),
            typeof(TwoColumnSummaryCardPanel),
            new PropertyMetadata(132d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(
            nameof(ColumnSpacing),
            typeof(double),
            typeof(TwoColumnSummaryCardPanel),
            new PropertyMetadata(6d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(
            nameof(RowSpacing),
            typeof(double),
            typeof(TwoColumnSummaryCardPanel),
            new PropertyMetadata(6d, OnLayoutPropertyChanged));

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return new Size(0, 0);
        }

        var metrics = CreateMetrics(availableSize.Width);
        var rowHeights = new double[GetRowCount(metrics.Columns)];

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            child.Measure(new Size(metrics.ItemWidth, double.PositiveInfinity));
            var rowIndex = index / metrics.Columns;
            rowHeights[rowIndex] = Math.Max(rowHeights[rowIndex], child.DesiredSize.Height);
        }

        var desiredHeight = rowHeights.Sum() + Math.Max(0, rowHeights.Length - 1) * metrics.RowSpacing;
        return new Size(metrics.DesiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        var metrics = CreateMetrics(finalSize.Width);
        var rowHeights = new double[GetRowCount(metrics.Columns)];

        for (var index = 0; index < Children.Count; index++)
        {
            var rowIndex = index / metrics.Columns;
            rowHeights[rowIndex] = Math.Max(rowHeights[rowIndex], Children[index].DesiredSize.Height);
        }

        var y = 0d;
        for (var rowIndex = 0; rowIndex < rowHeights.Length; rowIndex++)
        {
            var rowHeight = rowHeights[rowIndex];
            for (var columnIndex = 0; columnIndex < metrics.Columns; columnIndex++)
            {
                var childIndex = rowIndex * metrics.Columns + columnIndex;
                if (childIndex >= Children.Count)
                {
                    break;
                }

                var x = columnIndex * (metrics.ItemWidth + metrics.ColumnSpacing);
                Children[childIndex].Arrange(new Rect(x, y, metrics.ItemWidth, rowHeight));
            }

            y += rowHeight + metrics.RowSpacing;
        }

        return finalSize;
    }

    private static void OnLayoutPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TwoColumnSummaryCardPanel panel)
        {
            panel.InvalidateMeasure();
        }
    }

    private int GetRowCount(int columns) => (int)Math.Ceiling(Children.Count / (double)columns);

    private LayoutMetrics CreateMetrics(double availableWidth)
    {
        var minItemWidth = Math.Max(1d, MinItemWidth);
        var columnSpacing = Math.Max(0d, ColumnSpacing);
        var rowSpacing = Math.Max(0d, RowSpacing);
        var hasFiniteWidth = !double.IsInfinity(availableWidth) && !double.IsNaN(availableWidth);
        var canFitTwoColumns = hasFiniteWidth && availableWidth >= (minItemWidth * 2d) + columnSpacing;
        var columns = canFitTwoColumns ? 2 : 1;
        var itemWidth = hasFiniteWidth
            ? Math.Max(1d, (availableWidth - ((columns - 1) * columnSpacing)) / columns)
            : minItemWidth;
        var desiredWidth = hasFiniteWidth
            ? availableWidth
            : (itemWidth * columns) + ((columns - 1) * columnSpacing);

        return new LayoutMetrics(columns, itemWidth, columnSpacing, rowSpacing, desiredWidth);
    }

    private readonly record struct LayoutMetrics(
        int Columns,
        double ItemWidth,
        double ColumnSpacing,
        double RowSpacing,
        double DesiredWidth);
}
