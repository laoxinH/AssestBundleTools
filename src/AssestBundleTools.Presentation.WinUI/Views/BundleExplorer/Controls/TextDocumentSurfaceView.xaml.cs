using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.Web.WebView2.Core;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class TextDocumentSurfaceView : UserControl
{
    private const string WebViewVirtualHostName = "abt-text-editor.local";
    private const int WebViewBridgeReadyTimeoutSeconds = 8;

    public static readonly DependencyProperty LinesProperty =
        DependencyProperty.Register(
            nameof(Lines),
            typeof(IEnumerable),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(null, OnDocumentPropertyChanged));

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
            new PropertyMetadata(Visibility.Visible, OnDocumentPropertyChanged));

    public static readonly DependencyProperty EditableViewVisibilityProperty =
        DependencyProperty.Register(
            nameof(EditableViewVisibility),
            typeof(Visibility),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(Visibility.Collapsed, OnDocumentPropertyChanged));

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

    public static readonly DependencyProperty DocumentTextProperty =
        DependencyProperty.Register(
            nameof(DocumentText),
            typeof(string),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(string.Empty, OnDocumentPropertyChanged));

    public static readonly DependencyProperty DocumentLanguageProperty =
        DependencyProperty.Register(
            nameof(DocumentLanguage),
            typeof(string),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata("plaintext", OnDocumentPropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(TextDocumentSurfaceView),
            new PropertyMetadata(true, OnDocumentPropertyChanged));

    private bool _isWebViewReady;
    private bool _isSendingDocument;
    private bool _isApplyingWebViewText;
    private bool _webMessageHandlerAttached;
    private bool _webNavigationHandlerAttached;
    private bool _webProcessFailedHandlerAttached;
    private bool _hasUnsynchronizedWebViewChanges;
    private int _webViewNavigationVersion;
    private readonly Dictionary<string, TaskCompletionSource<string>> _documentTextRequests = new();
    private string _fallbackDiagnosticText = string.Empty;
    private string _lastSentTheme = string.Empty;
    private string _lastSentText = string.Empty;
    private string _lastSentLanguage = string.Empty;
    private bool _lastSentReadOnly = true;
    private bool _isSentDocumentCacheValid;

    public event EventHandler? LoadMoreLinesRequested;
    public event EventHandler? EditableTextChanged;
    public event EventHandler? DocumentChanged;
    public event EventHandler? SaveRequested;

    public TextDocumentSurfaceView()
    {
        InitializeComponent();
        LoadMoreLinesButtonText.Text = UiText.Get(ResourceKeys.TextAssetEditorLoadMoreLinesButton);
        CopyFallbackDiagnosticButtonText.Text = UiText.Get(ResourceKeys.TextDocumentSurfaceFallbackCopyDiagnosticButton);
        FallbackDiagnosticTitleText.Text = UiText.Get(ResourceKeys.TextDocumentSurfaceFallbackTitle);
        FallbackDiagnosticDescriptionText.Text = UiText.Get(ResourceKeys.TextDocumentSurfaceFallbackInitializing);
        ActualThemeChanged += OnActualThemeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IEnumerable? Lines { get => (IEnumerable?)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    public IEnumerable? SearchMatches { get => (IEnumerable?)GetValue(SearchMatchesProperty); set => SetValue(SearchMatchesProperty, value); }

    public string EditableText { get => (string)GetValue(EditableTextProperty); set => SetValue(EditableTextProperty, value); }

    public string StatusText { get => (string)GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    public Visibility ReadOnlyViewVisibility { get => (Visibility)GetValue(ReadOnlyViewVisibilityProperty); set => SetValue(ReadOnlyViewVisibilityProperty, value); }

    public Visibility EditableViewVisibility { get => (Visibility)GetValue(EditableViewVisibilityProperty); set => SetValue(EditableViewVisibilityProperty, value); }

    public Visibility SearchResultsVisibility { get => (Visibility)GetValue(SearchResultsVisibilityProperty); set => SetValue(SearchResultsVisibilityProperty, value); }

    public GridLength SearchResultsColumnWidth { get => (GridLength)GetValue(SearchResultsColumnWidthProperty); set => SetValue(SearchResultsColumnWidthProperty, value); }

    public Visibility FooterVisibility { get => (Visibility)GetValue(FooterVisibilityProperty); set => SetValue(FooterVisibilityProperty, value); }

    public bool CanLoadMoreLines { get => (bool)GetValue(CanLoadMoreLinesProperty); set => SetValue(CanLoadMoreLinesProperty, value); }

    public Visibility LoadMoreLinesButtonVisibility { get => (Visibility)GetValue(LoadMoreLinesButtonVisibilityProperty); set => SetValue(LoadMoreLinesButtonVisibilityProperty, value); }

    public string DocumentText { get => (string)GetValue(DocumentTextProperty); set => SetValue(DocumentTextProperty, value); }

    public string DocumentLanguage { get => (string)GetValue(DocumentLanguageProperty); set => SetValue(DocumentLanguageProperty, value); }

    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await TryInitializeWebViewAsync();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachWebViewHandlers();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        SendThemeToWebView(force: true);
    }

    private async Task TryInitializeWebViewAsync()
    {
        if (EditorWebView.CoreWebView2 is not null)
        {
            AttachWebViewHandlers(EditorWebView.CoreWebView2);
            if (_isWebViewReady)
            {
                SendDocumentToWebView(force: true);
                return;
            }
        }

        if (_isWebViewReady)
        {
            return;
        }

        try
        {
            var monacoRoot = TextDocumentWebViewEnvironmentProvider.ResolveMonacoAssetRoot();
            if (!Directory.Exists(monacoRoot))
            {
                ShowFallback(ResourceKeys.TextDocumentSurfaceFallbackAssetsMissing);
                return;
            }

            var runtimeProbe = TextDocumentWebViewEnvironmentProvider.ProbeRuntime();
            if (!runtimeProbe.IsAvailable)
            {
                ShowFallback(
                    ResourceKeys.TextDocumentSurfaceFallbackRuntimeUnavailable,
                    detail: runtimeProbe.ErrorCode,
                    runtimeProbe: runtimeProbe);
                return;
            }

            TextDocumentWebViewEnvironmentProvider.EnsureUserDataFolderConfigured();
            await EditorWebView.EnsureCoreWebView2Async();
            var coreWebView = EditorWebView.CoreWebView2;
            if (coreWebView is null)
            {
                ShowFallback(ResourceKeys.TextDocumentSurfaceFallbackInitializationFailed);
                return;
            }

            coreWebView.Settings.AreDefaultContextMenusEnabled = true;
            coreWebView.Settings.AreDevToolsEnabled = false;
            coreWebView.SetVirtualHostNameToFolderMapping(
                WebViewVirtualHostName,
                monacoRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            AttachWebViewHandlers(coreWebView);
            _isWebViewReady = false;
            var navigationVersion = ++_webViewNavigationVersion;
            EditorWebView.Source = new Uri($"https://{WebViewVirtualHostName}/abt-editor-host.html");
            _ = CheckBridgeReadyTimeoutAsync(navigationVersion);
        }
        catch
        {
            ShowFallback(ResourceKeys.TextDocumentSurfaceFallbackInitializationFailed);
        }
    }

    private void AttachWebViewHandlers(CoreWebView2 coreWebView)
    {
        if (!_webMessageHandlerAttached)
        {
            coreWebView.WebMessageReceived += OnWebMessageReceived;
            _webMessageHandlerAttached = true;
        }

        if (!_webNavigationHandlerAttached)
        {
            coreWebView.NavigationCompleted += OnNavigationCompleted;
            _webNavigationHandlerAttached = true;
        }

        if (!_webProcessFailedHandlerAttached)
        {
            coreWebView.ProcessFailed += OnCoreWebViewProcessFailed;
            _webProcessFailedHandlerAttached = true;
        }
    }

    private void DetachWebViewHandlers()
    {
        if (EditorWebView.CoreWebView2 is null)
        {
            return;
        }

        if (_webMessageHandlerAttached)
        {
            EditorWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _webMessageHandlerAttached = false;
        }

        if (_webNavigationHandlerAttached)
        {
            EditorWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            _webNavigationHandlerAttached = false;
        }

        if (_webProcessFailedHandlerAttached)
        {
            EditorWebView.CoreWebView2.ProcessFailed -= OnCoreWebViewProcessFailed;
            _webProcessFailedHandlerAttached = false;
        }
    }

    private void OnNavigationCompleted(
        CoreWebView2 sender,
        CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess && !_isWebViewReady)
        {
            ShowFallback(ResourceKeys.TextDocumentSurfaceFallbackHostLoadFailed);
        }
    }

    private void OnCoreWebViewProcessFailed(
        CoreWebView2 sender,
        CoreWebView2ProcessFailedEventArgs args)
    {
        ShowFallback(
            ResourceKeys.TextDocumentSurfaceFallbackProcessFailed,
            detail: args.ProcessFailedKind.ToString());
    }

    private async Task CheckBridgeReadyTimeoutAsync(int navigationVersion)
    {
        await Task.Delay(TimeSpan.FromSeconds(WebViewBridgeReadyTimeoutSeconds));
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (navigationVersion == _webViewNavigationVersion && !_isWebViewReady)
            {
                ShowFallback(ResourceKeys.TextDocumentSurfaceFallbackBridgeTimeout);
            }
        });
    }

    private void OnLoadMoreLinesButtonClick(object sender, RoutedEventArgs e)
    {
        LoadMoreLinesRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCopyFallbackDiagnosticButtonClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_fallbackDiagnosticText))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(_fallbackDiagnosticText);
        Clipboard.SetContent(package);
    }

    private void ShowFallback(
        string descriptionResourceKey,
        string? detail = null,
        TextDocumentWebViewRuntimeProbe? runtimeProbe = null)
    {
        _isWebViewReady = false;
        ++_webViewNavigationVersion;
        var description = UiText.Get(descriptionResourceKey);
        FallbackDiagnosticDescriptionText.Text = description;
        _fallbackDiagnosticText = TextDocumentSurfaceDiagnostics.BuildFallbackDiagnostic(
            descriptionResourceKey,
            description,
            detail,
            runtimeProbe,
            ActualTheme,
            DocumentLanguage,
            IsReadOnly,
            DocumentText?.Length ?? 0);
        EditorWebView.Visibility = Visibility.Collapsed;
        FallbackSurface.Visibility = Visibility.Visible;
    }
}
