using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 负责资产窗格搜索框的展开/收起动画和搜索按钮提示文本。
/// </summary>
internal sealed class AssetPaneSearchViewCoordinator
{
    private const double SearchHostExpandedHeight = 40d;

    private readonly Border _searchHost;
    private readonly TranslateTransform _searchTranslateTransform;
    private readonly Button _searchToggleButton;
    private readonly Func<AssetPaneViewModel?> _getViewModel;
    private readonly Storyboard _searchShowStoryboard = new();
    private readonly Storyboard _searchHideStoryboard = new();

    public AssetPaneSearchViewCoordinator(
        Border searchHost,
        TranslateTransform searchTranslateTransform,
        Button searchToggleButton,
        Func<AssetPaneViewModel?> getViewModel)
    {
        _searchHost = searchHost ?? throw new ArgumentNullException(nameof(searchHost));
        _searchTranslateTransform = searchTranslateTransform ?? throw new ArgumentNullException(nameof(searchTranslateTransform));
        _searchToggleButton = searchToggleButton ?? throw new ArgumentNullException(nameof(searchToggleButton));
        _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
        InitializeAnimations();
    }

    public void Stop()
    {
        _searchShowStoryboard.Stop();
        _searchHideStoryboard.Stop();
    }

    public void UpdateSearchState(AssetPaneViewModel? viewModel, bool useTransitions = true)
    {
        var isSearchExpanded = viewModel?.IsSearchExpanded == true;

        if (isSearchExpanded)
        {
            _searchHideStoryboard.Stop();
            _searchHost.Visibility = Visibility.Visible;

            if (!useTransitions)
            {
                _searchHost.Height = SearchHostExpandedHeight;
                _searchHost.Opacity = 1;
                _searchTranslateTransform.Y = 0;
                return;
            }

            _searchHost.Height = 0;
            _searchHost.Opacity = 0;
            _searchTranslateTransform.Y = -6;
            _searchShowStoryboard.Begin();
            return;
        }

        if (!useTransitions)
        {
            _searchHost.Visibility = Visibility.Collapsed;
            _searchHost.Height = 0;
            _searchHost.Opacity = 0;
            _searchTranslateTransform.Y = -6;
            return;
        }

        if (_searchHost.Visibility == Visibility.Visible)
        {
            _searchShowStoryboard.Stop();
            _searchHideStoryboard.Begin();
        }
    }

    public void UpdateSearchToggleToolTip(AssetPaneViewModel? viewModel)
    {
        var resourceKey = viewModel?.IsSearchExpanded == true
            ? ResourceKeys.AssetPaneViewSearchToggleButtonCloseToolTip
            : ResourceKeys.AssetPaneViewSearchToggleButtonOpenToolTip;

        ToolTipService.SetToolTip(_searchToggleButton, UiText.Get(resourceKey));
    }

    private void InitializeAnimations()
    {
        var showHeightAnimation = new DoubleAnimation
        {
            From = 0,
            To = SearchHostExpandedHeight,
            Duration = TimeSpan.FromMilliseconds(220),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showHeightAnimation, _searchHost);
        Storyboard.SetTargetProperty(showHeightAnimation, nameof(FrameworkElement.Height));

        var showOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showOpacityAnimation, _searchHost);
        Storyboard.SetTargetProperty(showOpacityAnimation, nameof(UIElement.Opacity));

        var showTranslateAnimation = new DoubleAnimation
        {
            From = -6,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showTranslateAnimation, _searchTranslateTransform);
        Storyboard.SetTargetProperty(showTranslateAnimation, nameof(TranslateTransform.Y));

        _searchShowStoryboard.Children.Add(showHeightAnimation);
        _searchShowStoryboard.Children.Add(showOpacityAnimation);
        _searchShowStoryboard.Children.Add(showTranslateAnimation);

        var hideHeightAnimation = new DoubleAnimation
        {
            From = SearchHostExpandedHeight,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideHeightAnimation, _searchHost);
        Storyboard.SetTargetProperty(hideHeightAnimation, nameof(FrameworkElement.Height));

        var hideOpacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideOpacityAnimation, _searchHost);
        Storyboard.SetTargetProperty(hideOpacityAnimation, nameof(UIElement.Opacity));

        var hideTranslateAnimation = new DoubleAnimation
        {
            From = 0,
            To = -6,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideTranslateAnimation, _searchTranslateTransform);
        Storyboard.SetTargetProperty(hideTranslateAnimation, nameof(TranslateTransform.Y));

        _searchHideStoryboard.Children.Add(hideHeightAnimation);
        _searchHideStoryboard.Children.Add(hideOpacityAnimation);
        _searchHideStoryboard.Children.Add(hideTranslateAnimation);
        _searchHideStoryboard.Completed += OnSearchHideCompleted;
    }

    private void OnSearchHideCompleted(object? sender, object e)
    {
        if (_getViewModel()?.IsSearchExpanded == true)
        {
            return;
        }

        _searchHost.Visibility = Visibility.Collapsed;
        _searchHost.Height = 0;
        _searchHost.Opacity = 0;
        _searchTranslateTransform.Y = -6;
    }
}
