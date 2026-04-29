using System.Diagnostics;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class BundleExplorerPagePaneLayoutCoordinator
    {
        private const double BundlePaneCollapsedWidth = 76d;
        private const double BundlePaneTransitionDurationMilliseconds = 240d;
        private const double BundleSearchHostExpandedHeight = 40d;
        private static readonly GridLength BalancedContentPaneColumnWidth = new(1d, GridUnitType.Star);
        private static readonly GridLength ExpandedAssetPaneColumnWidth = new(1.56, GridUnitType.Star);
        private static readonly GridLength ExpandedBundlePaneColumnWidth = new(1.08, GridUnitType.Star);
        private static readonly GridLength ExpandedInspectorPaneColumnWidth = new(1.36, GridUnitType.Star);
        private readonly Storyboard _bundlePaneCollapsedShowStoryboard = new();
        private readonly Storyboard _bundlePaneExpandedShowStoryboard = new();
        private readonly Storyboard _bundleSearchHideStoryboard = new();
        private readonly Storyboard _bundleSearchShowStoryboard = new();
        private readonly BundleExplorerPage _owner;
        private CancellationTokenSource? _bundlePaneTransitionCts;

        public BundleExplorerPagePaneLayoutCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void Initialize()
        {
            InitializeBundlePaneAnimations();
            InitializeBundleSearchAnimations();
        }

        public async Task ApplyInitialStateAsync()
        {
            await UpdateBundlePaneCollapseStateAsync(useTransitions: false);
            UpdateBundleSearchState(useTransitions: false);
        }

        public void FocusBundleSearchIfExpanded()
        {
            if (!_owner.ViewModel.BundlePane.IsBundleSearchExpanded)
            {
                return;
            }

            _owner.DispatcherQueue.TryEnqueue(() => _owner.BundleSearchTextBox.Focus(FocusState.Programmatic));
        }

        public void HandleBundlePanePageStateChanged(
            BundleExplorerPageStateChangedEventArgs<BundleExplorerBundlePanePageStateChange> e)
        {
            if (e.ChangeFlags.HasFlag(BundleExplorerBundlePanePageStateChange.SearchChrome))
            {
                Enqueue(UpdateBundleSearchState);
            }

            if (e.ChangeFlags.HasFlag(BundleExplorerBundlePanePageStateChange.PaneChrome))
            {
                Enqueue(() => _ = UpdateBundlePaneCollapseStateAsync());
            }
        }

        public void Stop()
        {
            _bundleSearchShowStoryboard.Stop();
            _bundleSearchHideStoryboard.Stop();
            _bundlePaneExpandedShowStoryboard.Stop();
            _bundlePaneCollapsedShowStoryboard.Stop();
            CancelBundlePaneTransition();
        }

        /// <summary>
        /// 初始化左侧资源包区域在“完整列表”和“摘要窄条”之间切换时的轻量动画。
        /// 当前将列宽连续过渡与卡片轻微淡入位移组合起来，
        /// 让布局切换更自然，同时保持 WinUI 布局稳定。
        /// </summary>
        private void InitializeBundlePaneAnimations()
        {
            var expandedOpacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(expandedOpacityAnimation, _owner.BundlePaneExpandedCard);
            Storyboard.SetTargetProperty(expandedOpacityAnimation, nameof(UIElement.Opacity));

            var expandedTranslateAnimation = new DoubleAnimation
            {
                From = 10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(expandedTranslateAnimation, _owner.BundlePaneExpandedTranslateTransform);
            Storyboard.SetTargetProperty(expandedTranslateAnimation, nameof(TranslateTransform.X));

            _bundlePaneExpandedShowStoryboard.Children.Add(expandedOpacityAnimation);
            _bundlePaneExpandedShowStoryboard.Children.Add(expandedTranslateAnimation);

            var collapsedOpacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(collapsedOpacityAnimation, _owner.BundlePaneCollapsedCard);
            Storyboard.SetTargetProperty(collapsedOpacityAnimation, nameof(UIElement.Opacity));

            var collapsedTranslateAnimation = new DoubleAnimation
            {
                From = -10,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(collapsedTranslateAnimation, _owner.BundlePaneCollapsedTranslateTransform);
            Storyboard.SetTargetProperty(collapsedTranslateAnimation, nameof(TranslateTransform.X));

            _bundlePaneCollapsedShowStoryboard.Children.Add(collapsedOpacityAnimation);
            _bundlePaneCollapsedShowStoryboard.Children.Add(collapsedTranslateAnimation);
        }

        private Task UpdateBundlePaneCollapseStateAsync()
        {
            return UpdateBundlePaneCollapseStateAsync(useTransitions: true);
        }

        /// <summary>
        /// 同步左侧资源包区域的布局状态。
        /// 当需要动画时，会先连续过渡三列宽度，再淡入目标卡片，
        /// 从而让“收起 / 展开”不再是突兀跳变。
        /// </summary>
        private async Task UpdateBundlePaneCollapseStateAsync(bool useTransitions)
        {
            CancelBundlePaneTransition();

            var transitionCts = new CancellationTokenSource();
            _bundlePaneTransitionCts = transitionCts;

            try
            {
                var isCollapsed = _owner.ViewModel.BundlePane.IsBundlePaneCollapsed;

                if (!useTransitions || !CanAnimateBundlePaneLayout())
                {
                    ApplyBundlePaneLayoutImmediately(isCollapsed);
                    return;
                }

                var startLeftWidth = _owner.BundlePaneColumnDefinition.ActualWidth;
                var startCenterWidth = _owner.AssetPaneColumnDefinition.ActualWidth;
                var startRightWidth = _owner.InspectorPaneColumnDefinition.ActualWidth;
                var totalWidth = startLeftWidth + startCenterWidth + startRightWidth;

                if (totalWidth <= 0d)
                {
                    ApplyBundlePaneLayoutImmediately(isCollapsed);
                    return;
                }

                var (targetLeftWidth, targetCenterWidth, targetRightWidth) = GetBundlePaneTargetWidths(totalWidth, isCollapsed);
                PrepareBundlePaneSourceCard(isCollapsed);

                await AnimateBundlePaneWidthsAsync(
                    startLeftWidth,
                    startCenterWidth,
                    startRightWidth,
                    targetLeftWidth,
                    targetCenterWidth,
                    targetRightWidth,
                    transitionCts.Token);

                transitionCts.Token.ThrowIfCancellationRequested();

                ApplyBundlePaneFinalColumnWidths(isCollapsed);
                ShowBundlePaneTargetCard(isCollapsed, useTransitions: true);
            }
            catch (OperationCanceledException)
            {
                // 连续点击时允许新的动画接管当前布局状态，取消旧动画属于预期行为。
            }
            finally
            {
                if (ReferenceEquals(_bundlePaneTransitionCts, transitionCts))
                {
                    _bundlePaneTransitionCts = null;
                }

                transitionCts.Dispose();
            }
        }

        private bool CanAnimateBundlePaneLayout() =>
            _owner.BundleWorkspaceGrid.ActualWidth > 0d &&
            _owner.BundlePaneColumnDefinition.ActualWidth > 0d &&
            _owner.AssetPaneColumnDefinition.ActualWidth > 0d &&
            _owner.InspectorPaneColumnDefinition.ActualWidth > 0d;

        private void CancelBundlePaneTransition()
        {
            if (_bundlePaneTransitionCts is null)
            {
                return;
            }

            _bundlePaneTransitionCts.Cancel();
            _bundlePaneTransitionCts.Dispose();
            _bundlePaneTransitionCts = null;
        }

        private void ApplyBundlePaneLayoutImmediately(bool isCollapsed)
        {
            _bundlePaneExpandedShowStoryboard.Stop();
            _bundlePaneCollapsedShowStoryboard.Stop();
            ApplyBundlePaneFinalColumnWidths(isCollapsed);
            ShowBundlePaneTargetCard(isCollapsed, useTransitions: false);
        }

        private void PrepareBundlePaneSourceCard(bool isCollapsed)
        {
            _bundlePaneExpandedShowStoryboard.Stop();
            _bundlePaneCollapsedShowStoryboard.Stop();

            if (isCollapsed)
            {
                _owner.BundlePaneExpandedCard.Visibility = Visibility.Visible;
                _owner.BundlePaneExpandedCard.Opacity = 1;
                _owner.BundlePaneExpandedTranslateTransform.X = 0;
                _owner.BundlePaneCollapsedCard.Visibility = Visibility.Collapsed;
                _owner.BundlePaneCollapsedCard.Opacity = 0;
                _owner.BundlePaneCollapsedTranslateTransform.X = 0;
                return;
            }

            _owner.BundlePaneCollapsedCard.Visibility = Visibility.Visible;
            _owner.BundlePaneCollapsedCard.Opacity = 1;
            _owner.BundlePaneCollapsedTranslateTransform.X = 0;
            _owner.BundlePaneExpandedCard.Visibility = Visibility.Collapsed;
            _owner.BundlePaneExpandedCard.Opacity = 0;
            _owner.BundlePaneExpandedTranslateTransform.X = 0;
        }

        private void ShowBundlePaneTargetCard(bool isCollapsed, bool useTransitions)
        {
            var targetCard = isCollapsed ? _owner.BundlePaneCollapsedCard : _owner.BundlePaneExpandedCard;
            var sourceCard = isCollapsed ? _owner.BundlePaneExpandedCard : _owner.BundlePaneCollapsedCard;
            var targetTransform = isCollapsed ? _owner.BundlePaneCollapsedTranslateTransform : _owner.BundlePaneExpandedTranslateTransform;

            sourceCard.Visibility = Visibility.Collapsed;
            sourceCard.Opacity = 0;

            targetCard.Visibility = Visibility.Visible;

            if (!useTransitions)
            {
                targetCard.Opacity = 1;
                targetTransform.X = 0;
                return;
            }

            targetCard.Opacity = 0;
            targetTransform.X = isCollapsed ? -10 : 10;

            if (isCollapsed)
            {
                _bundlePaneCollapsedShowStoryboard.Begin();
                return;
            }

            _bundlePaneExpandedShowStoryboard.Begin();
        }

        private void ApplyBundlePaneFinalColumnWidths(bool isCollapsed)
        {
            _owner.BundlePaneColumnDefinition.Width = isCollapsed
                ? new GridLength(BundlePaneCollapsedWidth)
                : ExpandedBundlePaneColumnWidth;
            _owner.AssetPaneColumnDefinition.Width = isCollapsed
                ? BalancedContentPaneColumnWidth
                : ExpandedAssetPaneColumnWidth;
            _owner.InspectorPaneColumnDefinition.Width = isCollapsed
                ? BalancedContentPaneColumnWidth
                : ExpandedInspectorPaneColumnWidth;
        }

        private (double Left, double Center, double Right) GetBundlePaneTargetWidths(double totalWidth, bool isCollapsed)
        {
            if (isCollapsed)
            {
                var collapsedLeftWidth = Math.Min(BundlePaneCollapsedWidth, totalWidth);
                var remainingWidth = Math.Max(0d, totalWidth - collapsedLeftWidth);
                var balancedWidth = remainingWidth / 2d;
                return (collapsedLeftWidth, balancedWidth, balancedWidth);
            }

            const double expandedRatioSum = 1.08d + 1.56d + 1.36d;
            return (
                totalWidth * 1.08d / expandedRatioSum,
                totalWidth * 1.56d / expandedRatioSum,
                totalWidth * 1.36d / expandedRatioSum);
        }

        private async Task AnimateBundlePaneWidthsAsync(
            double startLeftWidth,
            double startCenterWidth,
            double startRightWidth,
            double targetLeftWidth,
            double targetCenterWidth,
            double targetRightWidth,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawProgress = stopwatch.Elapsed.TotalMilliseconds / BundlePaneTransitionDurationMilliseconds;
                var progress = Math.Clamp(rawProgress, 0d, 1d);
                var easedProgress = EaseInOutCubic(progress);

                SetBundlePaneColumnWidths(
                    Lerp(startLeftWidth, targetLeftWidth, easedProgress),
                    Lerp(startCenterWidth, targetCenterWidth, easedProgress),
                    Lerp(startRightWidth, targetRightWidth, easedProgress));

                if (progress >= 1d)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken);
            }
        }

        private void SetBundlePaneColumnWidths(double leftWidth, double centerWidth, double rightWidth)
        {
            _owner.BundlePaneColumnDefinition.Width = new GridLength(Math.Max(0d, leftWidth), GridUnitType.Pixel);
            _owner.AssetPaneColumnDefinition.Width = new GridLength(Math.Max(0d, centerWidth), GridUnitType.Pixel);
            _owner.InspectorPaneColumnDefinition.Width = new GridLength(Math.Max(0d, rightWidth), GridUnitType.Pixel);
        }

        private static double Lerp(double start, double end, double progress) => start + ((end - start) * progress);

        private static double EaseInOutCubic(double progress)
        {
            if (progress < 0.5d)
            {
                return 4d * progress * progress * progress;
            }

            return 1d - (Math.Pow(-2d * progress + 2d, 3d) / 2d);
        }

        /// <summary>
        /// 初始化左侧资源包搜索框的展开与收起动画。
        /// 动画采用高度、透明度和轻微位移组合，
        /// 让搜索区域更接近 Win11 面板里常见的柔和展开体验。
        /// </summary>
        private void InitializeBundleSearchAnimations()
        {
            var showHeightAnimation = new DoubleAnimation
            {
                From = 0,
                To = BundleSearchHostExpandedHeight,
                Duration = TimeSpan.FromMilliseconds(220),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(showHeightAnimation, _owner.BundleSearchHost);
            Storyboard.SetTargetProperty(showHeightAnimation, nameof(FrameworkElement.Height));

            var showOpacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(showOpacityAnimation, _owner.BundleSearchHost);
            Storyboard.SetTargetProperty(showOpacityAnimation, nameof(UIElement.Opacity));

            var showTranslateAnimation = new DoubleAnimation
            {
                From = -6,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(showTranslateAnimation, _owner.BundleSearchTranslateTransform);
            Storyboard.SetTargetProperty(showTranslateAnimation, nameof(TranslateTransform.Y));

            _bundleSearchShowStoryboard.Children.Add(showHeightAnimation);
            _bundleSearchShowStoryboard.Children.Add(showOpacityAnimation);
            _bundleSearchShowStoryboard.Children.Add(showTranslateAnimation);

            var hideHeightAnimation = new DoubleAnimation
            {
                From = BundleSearchHostExpandedHeight,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(hideHeightAnimation, _owner.BundleSearchHost);
            Storyboard.SetTargetProperty(hideHeightAnimation, nameof(FrameworkElement.Height));

            var hideOpacityAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(hideOpacityAnimation, _owner.BundleSearchHost);
            Storyboard.SetTargetProperty(hideOpacityAnimation, nameof(UIElement.Opacity));

            var hideTranslateAnimation = new DoubleAnimation
            {
                From = 0,
                To = -6,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(hideTranslateAnimation, _owner.BundleSearchTranslateTransform);
            Storyboard.SetTargetProperty(hideTranslateAnimation, nameof(TranslateTransform.Y));

            _bundleSearchHideStoryboard.Children.Add(hideHeightAnimation);
            _bundleSearchHideStoryboard.Children.Add(hideOpacityAnimation);
            _bundleSearchHideStoryboard.Children.Add(hideTranslateAnimation);
            _bundleSearchHideStoryboard.Completed += OnBundleSearchHideCompleted;
        }

        private void UpdateBundleSearchState()
        {
            UpdateBundleSearchState(useTransitions: true);
        }

        private void UpdateBundleSearchState(bool useTransitions)
        {
            if (_owner.ViewModel.BundlePane.IsBundleSearchExpanded)
            {
                _bundleSearchHideStoryboard.Stop();
                _owner.BundleSearchHost.Visibility = Visibility.Visible;

                if (!useTransitions)
                {
                    _owner.BundleSearchHost.Height = BundleSearchHostExpandedHeight;
                    _owner.BundleSearchHost.Opacity = 1;
                    _owner.BundleSearchTranslateTransform.Y = 0;
                    return;
                }

                _owner.BundleSearchHost.Height = 0;
                _owner.BundleSearchHost.Opacity = 0;
                _owner.BundleSearchTranslateTransform.Y = -6;
                _bundleSearchShowStoryboard.Begin();
                return;
            }

            if (!useTransitions)
            {
                _owner.BundleSearchHost.Visibility = Visibility.Collapsed;
                _owner.BundleSearchHost.Height = 0;
                _owner.BundleSearchHost.Opacity = 0;
                _owner.BundleSearchTranslateTransform.Y = -6;
                return;
            }

            if (_owner.BundleSearchHost.Visibility == Visibility.Visible)
            {
                _bundleSearchShowStoryboard.Stop();
                _bundleSearchHideStoryboard.Begin();
            }
        }

        private void OnBundleSearchHideCompleted(object? sender, object e)
        {
            if (_owner.ViewModel.BundlePane.IsBundleSearchExpanded)
            {
                return;
            }

            _owner.BundleSearchHost.Visibility = Visibility.Collapsed;
            _owner.BundleSearchHost.Height = 0;
            _owner.BundleSearchHost.Opacity = 0;
            _owner.BundleSearchTranslateTransform.Y = -6;
        }

        private void Enqueue(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (_owner.DispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }

            _owner.DispatcherQueue.TryEnqueue(() => action());
        }
    }
}
