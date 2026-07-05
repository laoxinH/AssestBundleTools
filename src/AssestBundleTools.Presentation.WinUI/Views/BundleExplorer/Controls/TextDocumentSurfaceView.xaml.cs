using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class TextDocumentSurfaceView : UserControl
{
    public static readonly DependencyProperty LinesProperty =
        DependencyProperty.Register(
            nameof(Lines),
            typeof(IEnumerable),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SearchMatchesProperty =
        DependencyProperty.Register(
            nameof(SearchMatches),
            typeof(IEnumerable),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EditableTextProperty =
        DependencyProperty.Register(
            nameof(EditableText),
            typeof(string),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(string.Empty, OnEditableTextChanged));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(
            nameof(StatusText),
            typeof(string),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ReadOnlyViewVisibilityProperty =
        DependencyProperty.Register(
            nameof(ReadOnlyViewVisibility),
            typeof(Visibility),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty EditableViewVisibilityProperty =
        DependencyProperty.Register(
            nameof(EditableViewVisibility),
            typeof(Visibility),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty SearchResultsVisibilityProperty =
        DependencyProperty.Register(
            nameof(SearchResultsVisibility),
            typeof(Visibility),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty SearchResultsColumnWidthProperty =
        DependencyProperty.Register(
            nameof(SearchResultsColumnWidth),
            typeof(GridLength),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(new GridLength(0)));

    public static readonly DependencyProperty FooterVisibilityProperty =
        DependencyProperty.Register(
            nameof(FooterVisibility),
            typeof(Visibility),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty CanLoadMoreLinesProperty =
        DependencyProperty.Register(
            nameof(CanLoadMoreLines),
            typeof(bool),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty LoadMoreLinesButtonVisibilityProperty =
        DependencyProperty.Register(
            nameof(LoadMoreLinesButtonVisibility),
            typeof(Visibility),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(Visibility.Collapsed));

    public event EventHandler? LoadMoreLinesRequested;

    public event EventHandler? EditableTextChanged;

    public TextDocumentSurfaceView()
    {
        InitializeComponent();
        LoadMoreLinesButtonText.Text = UiText.Get(ResourceKeys.TextAssetEditorLoadMoreLinesButton);
    }

    public IEnumerable? Lines
    {
        get => (IEnumerable?)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public IEnumerable? SearchMatches
    {
        get => (IEnumerable?)GetValue(SearchMatchesProperty);
        set => SetValue(SearchMatchesProperty, value);
    }

    public string EditableText
    {
        get => (string)GetValue(EditableTextProperty);
        set => SetValue(EditableTextProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public Visibility ReadOnlyViewVisibility
    {
        get => (Visibility)GetValue(ReadOnlyViewVisibilityProperty);
        set => SetValue(ReadOnlyViewVisibilityProperty, value);
    }

    public Visibility EditableViewVisibility
    {
        get => (Visibility)GetValue(EditableViewVisibilityProperty);
        set => SetValue(EditableViewVisibilityProperty, value);
    }

    public Visibility SearchResultsVisibility
    {
        get => (Visibility)GetValue(SearchResultsVisibilityProperty);
        set => SetValue(SearchResultsVisibilityProperty, value);
    }

    public GridLength SearchResultsColumnWidth
    {
        get => (GridLength)GetValue(SearchResultsColumnWidthProperty);
        set => SetValue(SearchResultsColumnWidthProperty, value);
    }

    public Visibility FooterVisibility
    {
        get => (Visibility)GetValue(FooterVisibilityProperty);
        set => SetValue(FooterVisibilityProperty, value);
    }

    public bool CanLoadMoreLines
    {
        get => (bool)GetValue(CanLoadMoreLinesProperty);
        set => SetValue(CanLoadMoreLinesProperty, value);
    }

    public Visibility LoadMoreLinesButtonVisibility
    {
        get => (Visibility)GetValue(LoadMoreLinesButtonVisibilityProperty);
        set => SetValue(LoadMoreLinesButtonVisibilityProperty, value);
    }

    private void OnLoadMoreLinesButtonClick(object sender, RoutedEventArgs e)
    {
        LoadMoreLinesRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void OnEditableTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is TextDocumentSurfaceView view)
        {
            view.EditableTextChanged?.Invoke(view, EventArgs.Empty);
        }
    }
}
