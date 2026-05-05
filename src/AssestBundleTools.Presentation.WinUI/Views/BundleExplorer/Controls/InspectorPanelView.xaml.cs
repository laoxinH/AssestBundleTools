using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
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
    private InspectorPanelViewModel? _observedViewModel;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorPanelView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorPanelView()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public InspectorPanelViewModel? ViewModel
    {
        get => (InspectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler? ExtractRequested;

    public event EventHandler? SelectFileReplacementSourceRequested;

    public event EventHandler? SelectReplacementSourceRequested;

    public event EventHandler? ApplyReplacementRequested;

    public event EventHandler? UndoReplacementRequested;

    public event EventHandler? OpenPreviewLocationRequested;

    public event EventHandler<ResolvedAssetOpenRequestedEventArgs>? ResolvedAssetOpenRequested;

    private void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        ExtractRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectFileReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectFileReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplyReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        ApplyReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        UndoReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewSurfaceOpenPreviewLocationRequested(object? sender, EventArgs e)
    {
        OpenPreviewLocationRequested?.Invoke(this, EventArgs.Empty);
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

    private void OnResolvedAssetOpenButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ResolvedAssetNavigationTarget target })
        {
            ResolvedAssetOpenRequested?.Invoke(this, new ResolvedAssetOpenRequestedEventArgs(target));
        }
    }

    /// <summary>
    /// 内层详情切换保持单选语义，点击后统一回写到同一个 SectionIndex，
    /// 避免控件自己的开关行为把“当前选中页”状态打散。
    /// </summary>
    private void OnSectionRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not RadioButton { Tag: string sectionTag })
        {
            return;
        }

        if (int.TryParse(sectionTag, out var sectionIndex))
        {
            ViewModel.SelectedSectionIndex = sectionIndex;
        }
    }

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorPanelView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPanelViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPanelViewModel);
    }

    private void ApplyLocalizedText()
    {
        DetailsSectionButton.Content = UiText.Get(ResourceKeys.InspectorDetailsTabHeader);
        DependenciesSectionButton.Content = UiText.Get(ResourceKeys.InspectorDependenciesTabHeader);
        ReplacementSectionButton.Content = UiText.Get(ResourceKeys.InspectorReplacementTabHeader);
        ChangesSectionButton.Content = UiText.Get(ResourceKeys.InspectorChangesTabHeader);

        EmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorEmptyStateTitle);
        InformationSectionTitleText.Text = UiText.Get(ResourceKeys.InspectorInformationSectionTitle);
        AssetDependenciesTitleText.Text = UiText.Get(ResourceKeys.InspectorAssetDependenciesTitle);
        ReferencedByTitleText.Text = UiText.Get(ResourceKeys.InspectorReferencedByTitle);
        DependencyClosureTitleText.Text = UiText.Get(ResourceKeys.InspectorDependencyClosureTitle);
        BundleExternalDependenciesTitleText.Text = UiText.Get(ResourceKeys.InspectorBundleExternalDependenciesTitle);
        DependenciesEmptyStateText.Text = UiText.Get(ResourceKeys.InspectorDependenciesEmptyStateText);
        ReplacementTargetTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementTargetTitle);
        ReplacementSourceTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementSourceTitle);
        ReplacementCompatibilityTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementCompatibilityTitle);
        ExtractCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorExtractCapabilityLabel);
        FileReplacementCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorFileReplacementCapabilityLabel);
        WorkspaceReplacementCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorWorkspaceReplacementCapabilityLabel);
        ChangeSummaryTitleText.Text = UiText.Get(ResourceKeys.InspectorChangeSummaryTitle);

        ExtractButton.Content = UiText.Get(ResourceKeys.InspectorExtractButton);
        SelectFileReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectFileReplacementButton);
        SelectReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectReplacementButton);
        ApplyReplacementButton.Content = UiText.Get(ResourceKeys.InspectorApplyReplacementButton);
        UndoReplacementButton.Content = UiText.Get(ResourceKeys.InspectorUndoReplacementButton);
    }

    private void AttachObservedViewModel(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
    }

    private void DetachObservedViewModel(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = null;
    }
}
