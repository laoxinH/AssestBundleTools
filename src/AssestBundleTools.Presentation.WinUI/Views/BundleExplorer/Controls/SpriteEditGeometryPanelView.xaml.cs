using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class SpriteEditGeometryPanelView : UserControl
{
    private InspectorPreviewImageViewModel? _observedViewModel;
    private bool _isBindingsUpdateQueued;
    private bool _isUpdatingSpriteEditGeometryFields;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewImageViewModel),
            typeof(SpriteEditGeometryPanelView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public SpriteEditGeometryPanelView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedText();
    }

    public InspectorPreviewImageViewModel? ViewModel
    {
        get => (InspectorPreviewImageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not SpriteEditGeometryPanelView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPreviewImageViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPreviewImageViewModel);
        view.UpdateBindingsNow();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        UpdateBindingsNow();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachObservedViewModel(_observedViewModel);
    }

    private void AttachObservedViewModel(InspectorPreviewImageViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPreviewImageViewModel? viewModel)
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
        if (e.PropertyName == nameof(InspectorPreviewImageViewModel.IsSpriteEditDragActive) &&
            _observedViewModel?.IsSpriteEditDragActive == true)
        {
            ReleaseSpriteEditGeometryInputFocus();
        }

        QueueBindingsUpdate();
    }

    private void OnSpriteEditGeometryNumberBoxValueChanged(
        NumberBox sender,
        NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingSpriteEditGeometryFields ||
            ViewModel?.IsSpriteEditModeActive != true ||
            ViewModel.IsSpriteEditDragActive)
        {
            return;
        }

        ViewModel.UpdateSpriteEditGeometryFromInput(
            SpriteEditCropXNumberBox.Value,
            SpriteEditCropYNumberBox.Value,
            SpriteEditCropWidthNumberBox.Value,
            SpriteEditCropHeightNumberBox.Value,
            SpriteEditPivotXNumberBox.Value,
            SpriteEditPivotYNumberBox.Value,
            ResolveSpriteEditGeometryInputKind(sender));
    }

    private void UpdateBindingsNow()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBindingsNow);
            return;
        }

        _isBindingsUpdateQueued = false;
        _isUpdatingSpriteEditGeometryFields = true;
        try
        {
            Bindings.Update();
        }
        finally
        {
            _isUpdatingSpriteEditGeometryFields = false;
        }
    }

    private void QueueBindingsUpdate()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueBindingsUpdate);
            return;
        }

        if (_isBindingsUpdateQueued)
        {
            return;
        }

        _isBindingsUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(UpdateBindingsNow))
        {
            _isBindingsUpdateQueued = false;
        }
    }

    private void ReleaseSpriteEditGeometryInputFocus()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(ReleaseSpriteEditGeometryInputFocus);
            return;
        }

        _ = SpriteEditFocusSinkButton.Focus(FocusState.Programmatic);
    }

    private static SpriteEditGeometryInputKind ResolveSpriteEditGeometryInputKind(NumberBox sender)
    {
        return sender.Name is nameof(SpriteEditPivotXNumberBox) or nameof(SpriteEditPivotYNumberBox)
            ? SpriteEditGeometryInputKind.Pivot
            : SpriteEditGeometryInputKind.Crop;
    }

    private void ApplyLocalizedText()
    {
        SpriteEditGeometryTitleText.Text = UiText.Get(ResourceKeys.InspectorSpriteEditGeometryTitle);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropXLabelText,
            SpriteEditCropXNumberBox,
            ResourceKeys.InspectorSpriteEditCropXLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropYLabelText,
            SpriteEditCropYNumberBox,
            ResourceKeys.InspectorSpriteEditCropYLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropWidthLabelText,
            SpriteEditCropWidthNumberBox,
            ResourceKeys.InspectorSpriteEditCropWidthLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditCropHeightLabelText,
            SpriteEditCropHeightNumberBox,
            ResourceKeys.InspectorSpriteEditCropHeightLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditPivotXLabelText,
            SpriteEditPivotXNumberBox,
            ResourceKeys.InspectorSpriteEditPivotXLabel);
        ApplySpriteEditNumberBoxText(
            SpriteEditPivotYLabelText,
            SpriteEditPivotYNumberBox,
            ResourceKeys.InspectorSpriteEditPivotYLabel);
    }

    private static void ApplySpriteEditNumberBoxText(
        TextBlock label,
        NumberBox numberBox,
        string resourceKey)
    {
        var text = UiText.Get(resourceKey);
        label.Text = text;
        AutomationProperties.SetName(numberBox, text);
    }
}
