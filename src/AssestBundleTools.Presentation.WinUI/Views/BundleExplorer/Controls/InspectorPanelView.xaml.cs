using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 右侧详情检查器的可复用视图。
/// </summary>
public sealed partial class InspectorPanelView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorPanelView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public static readonly DependencyProperty ShowDetailsPreviewProperty =
        DependencyProperty.Register(
            nameof(ShowDetailsPreview),
            typeof(bool),
            typeof(InspectorPanelView),
            new PropertyMetadata(true, OnLayoutOptionPropertyChanged));

    public static readonly DependencyProperty ShowActionBarProperty =
        DependencyProperty.Register(
            nameof(ShowActionBar),
            typeof(bool),
            typeof(InspectorPanelView),
            new PropertyMetadata(true, OnLayoutOptionPropertyChanged));

    public static readonly DependencyProperty SpineEditDraftProperty =
        DependencyProperty.Register(
            nameof(SpineEditDraft),
            typeof(SpineAssetGroupEditDraftViewModel),
            typeof(InspectorPanelView),
            new PropertyMetadata(null, OnLayoutOptionPropertyChanged));

    public static readonly DependencyProperty SpineEditPanelVisibilityProperty =
        DependencyProperty.Register(
            nameof(SpineEditPanelVisibility),
            typeof(Visibility),
            typeof(InspectorPanelView),
            new PropertyMetadata(Visibility.Collapsed, OnLayoutOptionPropertyChanged));

    public InspectorPanelView()
    {
        InitializeComponent();
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DetailsSectionScrollViewer.LayoutUpdated += OnSectionScrollViewerLayoutUpdated;
        DependenciesView.SectionScrollViewer.LayoutUpdated += OnSectionScrollViewerLayoutUpdated;
        ReplacementSectionScrollViewer.LayoutUpdated += OnSectionScrollViewerLayoutUpdated;
        ChangesSectionScrollViewer.LayoutUpdated += OnSectionScrollViewerLayoutUpdated;
    }

    public InspectorPanelViewModel? ViewModel
    {
        get => (InspectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool ShowDetailsPreview
    {
        get => (bool)GetValue(ShowDetailsPreviewProperty);
        set => SetValue(ShowDetailsPreviewProperty, value);
    }

    public bool ShowActionBar
    {
        get => (bool)GetValue(ShowActionBarProperty);
        set => SetValue(ShowActionBarProperty, value);
    }

    public SpineAssetGroupEditDraftViewModel? SpineEditDraft
    {
        get => (SpineAssetGroupEditDraftViewModel?)GetValue(SpineEditDraftProperty);
        set => SetValue(SpineEditDraftProperty, value);
    }

    public Visibility SpineEditPanelVisibility
    {
        get => (Visibility)GetValue(SpineEditPanelVisibilityProperty);
        set => SetValue(SpineEditPanelVisibilityProperty, value);
    }

    public Visibility DetailsPreviewVisibility => ShowDetailsPreview ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ActionBarVisibility => ShowActionBar ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StandardReplacementContentVisibility => Visibility.Collapsed;

    public event EventHandler? ExtractRequested;

    public event EventHandler? SelectFileReplacementSourceRequested;

    public event EventHandler? ModifyFromFileReplacementSourceRequested;

    public event EventHandler? EditRequested;

    public event EventHandler? ApplyReplacementRequested;

    public event EventHandler<InspectorChangeRecordActionRequestedEventArgs>? UndoReplacementRequested;

    public event EventHandler<InspectorChangeRecordActionRequestedEventArgs>? RedoReplacementRequested;

    public event EventHandler<InspectorChangeRecordActionRequestedEventArgs>? DeleteChangeRecordRequested;

    public event EventHandler? OpenPreviewLocationRequested;

    public event EventHandler? SpriteTextureReplacementRequested;

    public event EventHandler? LoadMoreTextRequested;

    public event EventHandler<ResolvedAssetOpenRequestedEventArgs>? ResolvedAssetOpenRequested;

    public event EventHandler<SpineAtlasPageSelectionRequestedEventArgs>? SpineAtlasPageSelectionRequested;

    public event EventHandler<SpineEditPartFileSelectionRequestedEventArgs>? SpineEditPartFileSelectionRequested;

    public event EventHandler? SpineEditBatchMatchRequested;

    private void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        ExtractRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectFileReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectFileReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplySelectedReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        ApplyReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnModifyButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CanModify == true)
        {
            ModifyFromFileReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnEditButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CanEditText == true)
        {
            EditRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnChangeRecordUndoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        UndoReplacementRequested?.Invoke(this, new InspectorChangeRecordActionRequestedEventArgs(GetOperationId(sender)));
    }

    private void OnChangeRecordRedoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        RedoReplacementRequested?.Invoke(this, new InspectorChangeRecordActionRequestedEventArgs(GetOperationId(sender)));
    }

    private void OnChangeRecordDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        DeleteChangeRecordRequested?.Invoke(this, new InspectorChangeRecordActionRequestedEventArgs(GetOperationId(sender)));
    }

    private void OnPreviewSurfaceOpenPreviewLocationRequested(object? sender, EventArgs e)
    {
        OpenPreviewLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewSurfaceSpriteTextureReplacementRequested(object? sender, EventArgs e)
    {
        SpriteTextureReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewSurfaceLoadMoreTextRequested(object? sender, EventArgs e)
    {
        LoadMoreTextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewSurfaceResolvedAssetOpenRequested(
        object? sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        ResolvedAssetOpenRequested?.Invoke(this, e);
    }

    private void OnInfoCardResolvedAssetOpenRequested(
        object? sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        ResolvedAssetOpenRequested?.Invoke(this, e);
    }

    private void OnSpineAtlasPageSelectorSelectionRequested(
        object? sender,
        SpineAtlasPageSelectionRequestedEventArgs e)
    {
        SpineAtlasPageSelectionRequested?.Invoke(this, e);
    }

    private void OnSpineEditPartFileSelectionRequested(
        object? sender,
        SpineEditPartFileSelectionRequestedEventArgs e)
    {
        SpineEditPartFileSelectionRequested?.Invoke(this, e);
    }

    private void OnSpineEditBatchMatchRequested(object? sender, EventArgs e)
    {
        SpineEditBatchMatchRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void OnLayoutOptionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorPanelView view)
        {
            view.Bindings.Update();
        }
    }

    private void ApplyLocalizedText()
    {
        DetailsSectionButton.Content = UiText.Get(ResourceKeys.InspectorDetailsTabHeader);
        DependenciesSectionButton.Content = UiText.Get(ResourceKeys.InspectorDependenciesTabHeader);
        ReplacementSectionButton.Content = UiText.Get(ResourceKeys.InspectorReplacementTabHeader);
        ChangesSectionButton.Content = UiText.Get(ResourceKeys.InspectorChangesTabHeader);

        EmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorEmptyStateTitle);
        InformationSectionTitleText.Text = UiText.Get(ResourceKeys.InspectorInformationSectionTitle);
        ReplacementTargetTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementTargetTitle);
        ReplacementSourceTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementSourceTitle);
        ReplacementCompatibilityTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementCompatibilityTitle);
        ExtractCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorExtractCapabilityLabel);
        FileReplacementCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorFileReplacementCapabilityLabel);
        WorkspaceReplacementCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorWorkspaceReplacementCapabilityLabel);
        ChangeSummaryTitleText.Text = UiText.Get(ResourceKeys.InspectorChangeSummaryTitle);
        SelectFileReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectFileReplacementButton);
        ApplySelectedReplacementButton.Content = UiText.Get(ResourceKeys.InspectorApplyReplacementButton);

        ApplyActionButtonText(ExtractButton, ExtractButtonText, ResourceKeys.InspectorExtractButton);
    }

    private static void ApplyActionButtonText(Button button, TextBlock textBlock, string resourceKey)
    {
        var text = UiText.Get(resourceKey);
        textBlock.Text = text;
        AutomationProperties.SetName(button, text);
        ToolTipService.SetToolTip(button, text);
    }

    private static string? GetOperationId(object sender) =>
        sender is FrameworkElement { Tag: string operationId } &&
        !string.IsNullOrWhiteSpace(operationId)
            ? operationId.Trim()
            : null;

}

public sealed class InspectorChangeRecordActionRequestedEventArgs : EventArgs
{
    public InspectorChangeRecordActionRequestedEventArgs(string? operationId)
    {
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
    }

    public string? OperationId { get; }
}
