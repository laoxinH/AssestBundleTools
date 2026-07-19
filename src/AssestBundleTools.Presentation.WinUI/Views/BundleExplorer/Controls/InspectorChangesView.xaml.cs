using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorChangesView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorChangesView),
            new PropertyMetadata(null));

    public InspectorChangesView()
    {
        InitializeComponent();
        ChangesEmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorChangesEmptyStateTitle);
    }

    public event EventHandler<InspectorChangeRecordActionRequestedEventArgs>? UndoReplacementRequested;

    public event EventHandler<InspectorChangeRecordActionRequestedEventArgs>? RedoReplacementRequested;

    public event EventHandler<InspectorChangeRecordActionRequestedEventArgs>? DeleteRecordRequested;

    public event EventHandler<ScrollViewerViewChangedEventArgs>? SectionViewChanged;

    public InspectorPanelViewModel? ViewModel
    {
        get => (InspectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ScrollViewer SectionScrollViewer => ChangesSectionScrollViewer;

    private void OnUndoReplacementButtonClick(object sender, RoutedEventArgs e) =>
        UndoReplacementRequested?.Invoke(this, CreateActionEventArgs(sender));

    private void OnRedoReplacementButtonClick(object sender, RoutedEventArgs e) =>
        RedoReplacementRequested?.Invoke(this, CreateActionEventArgs(sender));

    private void OnDeleteRecordButtonClick(object sender, RoutedEventArgs e) =>
        DeleteRecordRequested?.Invoke(this, CreateActionEventArgs(sender));

    private void OnChangesSectionScrollViewerViewChanged(
        object sender,
        ScrollViewerViewChangedEventArgs e) =>
        SectionViewChanged?.Invoke(sender, e);

    private static InspectorChangeRecordActionRequestedEventArgs CreateActionEventArgs(object sender) =>
        new(sender is FrameworkElement { Tag: string operationId }
            ? operationId
            : null);
}
