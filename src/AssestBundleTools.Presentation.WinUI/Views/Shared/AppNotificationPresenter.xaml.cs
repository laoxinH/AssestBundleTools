using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace AssestBundleTools.Presentation.WinUI.Views.Shared;

/// <summary>
/// 承载应用级轻量通知的通用展示控件。
/// </summary>
/// <remarks>
/// 通知状态由 <see cref="IAppNotificationService" /> 提供，
/// 控件本身只负责视觉样式、动画和关闭手势，
/// 这样 Shell 只需挂载一个宿主，页面和 ViewModel 则专注于发起通知。
/// </remarks>
public sealed partial class AppNotificationPresenter : UserControl
{
    private readonly Storyboard _showStoryboard = new();
    private readonly Storyboard _hideStoryboard = new();
    private IAppNotificationService? _subscribedNotificationService;

    public static readonly DependencyProperty NotificationServiceProperty =
        DependencyProperty.Register(
            nameof(NotificationService),
            typeof(IAppNotificationService),
            typeof(AppNotificationPresenter),
            new PropertyMetadata(null, OnNotificationServiceChanged));

    public AppNotificationPresenter()
    {
        InitializeComponent();
        ApplyLocalizedText();
        InitializeAnimations();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IAppNotificationService? NotificationService
    {
        get => (IAppNotificationService?)GetValue(NotificationServiceProperty);
        set => SetValue(NotificationServiceProperty, value);
    }

    private static void OnNotificationServiceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not AppNotificationPresenter presenter)
        {
            return;
        }

        presenter.DetachNotificationService(e.OldValue as IAppNotificationService);

        if (presenter.IsLoaded)
        {
            presenter.AttachNotificationService(e.NewValue as IAppNotificationService);
        }

        presenter.UpdateNotificationState(useTransitions: false);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachNotificationService(NotificationService);
        UpdateNotificationState(useTransitions: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachNotificationService(NotificationService);
        _showStoryboard.Stop();
        _hideStoryboard.Stop();
    }

    private void OnNotificationCloseButtonClick(object sender, RoutedEventArgs e)
    {
        NotificationService?.Dismiss();
    }

    private void OnNotificationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppNotificationService.IsOpen)
            or nameof(IAppNotificationService.Severity)
            or nameof(IAppNotificationService.Title)
            or nameof(IAppNotificationService.Message))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                UpdateNotificationState();
                return;
            }

            DispatcherQueue.TryEnqueue(UpdateNotificationState);
        }
    }

    private void AttachNotificationService(IAppNotificationService? notificationService)
    {
        if (notificationService is null || ReferenceEquals(_subscribedNotificationService, notificationService))
        {
            return;
        }

        DetachNotificationService(_subscribedNotificationService);
        notificationService.PropertyChanged += OnNotificationServicePropertyChanged;
        _subscribedNotificationService = notificationService;
    }

    private void DetachNotificationService(IAppNotificationService? notificationService)
    {
        if (notificationService is null || !ReferenceEquals(_subscribedNotificationService, notificationService))
        {
            return;
        }

        notificationService.PropertyChanged -= OnNotificationServicePropertyChanged;
        _subscribedNotificationService = null;
    }

    private void ApplyLocalizedText()
    {
        ToolTipService.SetToolTip(
            NotificationCloseButton,
            UiText.Get(ResourceKeys.AppNotificationCloseButtonToolTip));
    }

    private void InitializeAnimations()
    {
        var showOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showOpacityAnimation, NotificationHost);
        Storyboard.SetTargetProperty(showOpacityAnimation, nameof(Opacity));

        var showTranslateAnimation = new DoubleAnimation
        {
            From = -24,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(260),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showTranslateAnimation, NotificationTranslateTransform);
        Storyboard.SetTargetProperty(showTranslateAnimation, nameof(TranslateTransform.Y));

        _showStoryboard.Children.Add(showOpacityAnimation);
        _showStoryboard.Children.Add(showTranslateAnimation);

        var hideOpacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideOpacityAnimation, NotificationHost);
        Storyboard.SetTargetProperty(hideOpacityAnimation, nameof(Opacity));

        var hideTranslateAnimation = new DoubleAnimation
        {
            From = 0,
            To = -14,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideTranslateAnimation, NotificationTranslateTransform);
        Storyboard.SetTargetProperty(hideTranslateAnimation, nameof(TranslateTransform.Y));

        _hideStoryboard.Children.Add(hideOpacityAnimation);
        _hideStoryboard.Children.Add(hideTranslateAnimation);
        _hideStoryboard.Completed += OnHideStoryboardCompleted;
    }

    private void UpdateNotificationState()
    {
        UpdateNotificationState(useTransitions: true);
    }

    private void UpdateNotificationState(bool useTransitions)
    {
        NotificationTitleText.Text = NotificationService?.Title ?? string.Empty;
        NotificationMessageText.Text = NotificationService?.Message ?? string.Empty;
        ApplyNotificationVisuals();

        if (NotificationService?.IsOpen == true)
        {
            _hideStoryboard.Stop();
            NotificationHost.Visibility = Visibility.Visible;

            if (!useTransitions)
            {
                NotificationHost.Opacity = 1;
                NotificationTranslateTransform.Y = 0;
                return;
            }

            NotificationHost.Opacity = 0;
            NotificationTranslateTransform.Y = -24;
            _showStoryboard.Begin();
            return;
        }

        if (!useTransitions)
        {
            NotificationHost.Visibility = Visibility.Collapsed;
            NotificationHost.Opacity = 0;
            NotificationTranslateTransform.Y = -24;
            return;
        }

        if (NotificationHost.Visibility == Visibility.Visible)
        {
            _showStoryboard.Stop();
            _hideStoryboard.Begin();
        }
    }

    private void OnHideStoryboardCompleted(object? sender, object e)
    {
        if (NotificationService?.IsOpen == true)
        {
            return;
        }

        NotificationHost.Visibility = Visibility.Collapsed;
        NotificationHost.Opacity = 0;
        NotificationTranslateTransform.Y = -24;
    }

    private void ApplyNotificationVisuals()
    {
        var severity = NotificationService?.Severity ?? AppNotificationSeverity.Informational;

        var (glyph, accentColor, iconBackgroundColor, iconForegroundColor) = severity switch
        {
            AppNotificationSeverity.Success => ("\uE460", ColorHelper.FromArgb(255, 45, 125, 70), ColorHelper.FromArgb(255, 232, 245, 233), ColorHelper.FromArgb(255, 45, 125, 70)),
            AppNotificationSeverity.Warning => ("\uF561", ColorHelper.FromArgb(255, 198, 124, 18), ColorHelper.FromArgb(255, 255, 244, 220), ColorHelper.FromArgb(255, 140, 84, 0)),
            AppNotificationSeverity.Error => ("\uE877", ColorHelper.FromArgb(255, 196, 43, 28), ColorHelper.FromArgb(255, 253, 232, 232), ColorHelper.FromArgb(255, 164, 38, 44)),
            _ => ("\uEA88", ColorHelper.FromArgb(255, 79, 107, 237), ColorHelper.FromArgb(255, 236, 242, 255), ColorHelper.FromArgb(255, 58, 91, 201))
        };

        NotificationAccentBar.Background = new SolidColorBrush(accentColor);
        NotificationIconBackground.Background = new SolidColorBrush(iconBackgroundColor);
        NotificationIcon.Foreground = new SolidColorBrush(iconForegroundColor);
        NotificationIcon.Glyph = glyph;
    }
}
