using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class SpriteEditGeometryPanelView : UserControl
{
    private const double SpriteEditGeometryValueTolerance = 0.0001d;

    private InspectorPreviewImageViewModel? _observedViewModel;
    private bool _isBindingsUpdateQueued;
    private bool _isUpdatingSpriteEditGeometryFields;
    private int _spriteEditGeometryBindingUpdateVersion;

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
            _isBindingsUpdateQueued ||
            ViewModel?.IsSpriteEditModeActive != true ||
            ViewModel.IsSpriteEditDragActive ||
            IsSpriteEditGeometryValueSynchronizedWithViewModel(sender, ViewModel))
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
        var updateVersion = ++_spriteEditGeometryBindingUpdateVersion;
        _isUpdatingSpriteEditGeometryFields = true;
        try
        {
            Bindings.Update();
        }
        finally
        {
            if (!DispatcherQueue.TryEnqueue(() => CompleteSpriteEditGeometryBindingUpdate(updateVersion)))
            {
                CompleteSpriteEditGeometryBindingUpdate(updateVersion);
            }
        }
    }

    private void CompleteSpriteEditGeometryBindingUpdate(int updateVersion)
    {
        if (updateVersion == _spriteEditGeometryBindingUpdateVersion)
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

        var focusedNumberBox = ResolveFocusedSpriteEditGeometryNumberBox();
        if (focusedNumberBox is not null)
        {
            ResetSpriteEditGeometryNumberBox(focusedNumberBox);
        }

        _ = TryFocusSpriteEditFocusSink();
    }

    private bool TryFocusSpriteEditFocusSink()
    {
        var wasTabStop = SpriteEditFocusSinkButton.IsTabStop;
        SpriteEditFocusSinkButton.IsTabStop = true;
        try
        {
            return SpriteEditFocusSinkButton.Focus(FocusState.Programmatic);
        }
        finally
        {
            SpriteEditFocusSinkButton.IsTabStop = wasTabStop;
        }
    }

    private NumberBox? ResolveFocusedSpriteEditGeometryNumberBox()
    {
        var focusedElement = XamlRoot is null
            ? FocusManager.GetFocusedElement()
            : FocusManager.GetFocusedElement(XamlRoot);

        if (focusedElement is not DependencyObject focusedDependencyObject)
        {
            return null;
        }

        if (focusedDependencyObject is TextBox textBox)
        {
            textBox.Select(textBox.Text.Length, 0);
        }

        return ResolveSpriteEditGeometryNumberBoxes()
            .FirstOrDefault(numberBox => IsElementOrVisualDescendantOf(focusedDependencyObject, numberBox));
    }

    private static void ResetSpriteEditGeometryNumberBox(NumberBox numberBox)
    {
        numberBox.IsEnabled = false;
        numberBox.IsEnabled = true;
    }

    private IEnumerable<NumberBox> ResolveSpriteEditGeometryNumberBoxes()
    {
        yield return SpriteEditCropXNumberBox;
        yield return SpriteEditCropYNumberBox;
        yield return SpriteEditCropWidthNumberBox;
        yield return SpriteEditCropHeightNumberBox;
        yield return SpriteEditPivotXNumberBox;
        yield return SpriteEditPivotYNumberBox;
    }

    private static bool IsElementOrVisualDescendantOf(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static SpriteEditGeometryInputKind ResolveSpriteEditGeometryInputKind(NumberBox sender)
    {
        return sender.Name is nameof(SpriteEditPivotXNumberBox) or nameof(SpriteEditPivotYNumberBox)
            ? SpriteEditGeometryInputKind.Pivot
            : SpriteEditGeometryInputKind.Crop;
    }

    private static bool IsSpriteEditGeometryValueSynchronizedWithViewModel(
        NumberBox sender,
        InspectorPreviewImageViewModel viewModel)
    {
        var currentValue = sender.Name switch
        {
            nameof(SpriteEditCropXNumberBox) => viewModel.SpriteEditCropX,
            nameof(SpriteEditCropYNumberBox) => viewModel.SpriteEditCropY,
            nameof(SpriteEditCropWidthNumberBox) => viewModel.SpriteEditCropWidth,
            nameof(SpriteEditCropHeightNumberBox) => viewModel.SpriteEditCropHeight,
            nameof(SpriteEditPivotXNumberBox) => viewModel.SpriteEditPivotX,
            nameof(SpriteEditPivotYNumberBox) => viewModel.SpriteEditPivotY,
            _ => double.NaN
        };

        return AreSpriteEditGeometryValuesClose(sender.Value, currentValue);
    }

    private static bool AreSpriteEditGeometryValuesClose(double left, double right)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right))
        {
            return false;
        }

        return Math.Abs(left - right) <= SpriteEditGeometryValueTolerance;
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
