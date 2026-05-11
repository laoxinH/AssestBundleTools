using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class BundleExplorerPageChromeCoordinator
    {
        private readonly Storyboard _collapsedBundleImportDirectoryInteractionStoryboard = new();
        private readonly Storyboard _collapsedBundleImportFileInteractionStoryboard = new();
        private readonly BundleExplorerPage _owner;
        private bool _isWorkspaceImportDragActive;

        public BundleExplorerPageChromeCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void ApplyLocalizedText()
        {
            _owner.PageEyebrowText.Text = UiText.Get(ResourceKeys.BundleExplorerPageEyebrow);
            _owner.PageTitleText.Text = UiText.Get(ResourceKeys.BundleExplorerPageTitle);
            _owner.CreateWorkspaceChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageNewWorkspaceButton);
            _owner.OpenWorkspaceChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageOpenWorkspaceButton);
            ToolTipService.SetToolTip(_owner.ReturnToConsoleIconButton, UiText.Get(ResourceKeys.BundleExplorerPageReturnToConsoleButton));
            _owner.RefreshChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageRefreshButton);
            _owner.BatchExtractChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageBatchExtractButton);
            _owner.SaveAsBundleChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageSaveAsBundleButton);
            _owner.LeftPanelTitleText.Text = UiText.Get(ResourceKeys.BundleExplorerPageLeftPanelTitle);
            _owner.BundleSearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.BundleExplorerPageBundleSearchTextBoxPlaceholder);
            ToolTipService.SetToolTip(
                _owner.BundleImportFileButton,
                UiText.Get(ResourceKeys.BundleExplorerPageBundleImportFileButtonToolTip));
            ToolTipService.SetToolTip(
                _owner.BundleImportDirectoryButton,
                UiText.Get(ResourceKeys.BundleExplorerPageBundleImportDirectoryButtonToolTip));
            ToolTipService.SetToolTip(
                _owner.CollapsedBundleImportFileButton,
                UiText.Get(ResourceKeys.BundleExplorerPageBundleImportFileButtonToolTip));
            ToolTipService.SetToolTip(
                _owner.CollapsedBundleImportDirectoryButton,
                UiText.Get(ResourceKeys.BundleExplorerPageBundleImportDirectoryButtonToolTip));
            _owner.CollapsedBundleImportFileButtonText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneImportFileButtonText);
            _owner.CollapsedBundleImportDirectoryButtonText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneImportDirectoryButtonText);
            _owner.CollapsedBundlePaneBundleCountLabelText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneBundleCountLabelText);
            _owner.CollapsedBundlePaneGroupCountLabelText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneGroupCountLabelText);
            _owner.CollapsedBundlePaneSelectionLabelText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneSelectionLabelText);
            _owner.WorkspaceImportDropOverlayTitleText.Text = UiText.Get(ResourceKeys.WorkspaceImportDropOverlayTitle);
            _owner.WorkspaceImportDropOverlayHintText.Text = UiText.Get(ResourceKeys.WorkspaceImportDropOverlayHint);
            _owner.WorkspaceImportBusyOverlayTitleText.Text = UiText.Get(ResourceKeys.WorkspaceImportBusyOverlayTitle);
            _owner.WorkspaceImportBusyOverlayHintText.Text = UiText.Get(ResourceKeys.WorkspaceImportBusyOverlayHint);
            _owner.CenterPanelTitleText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCenterPanelTitle);
            _owner.RightPanelTitleText.Text = UiText.Get(ResourceKeys.BundleExplorerPageRightPanelTitle);
            ToolTipService.SetToolTip(
                _owner.LocateActiveDocumentButton,
                UiText.Get(ResourceKeys.AssetEditorWindowLocateActiveDocumentText));
            _owner.LocateActiveDocumentButton.SetValue(
                AutomationProperties.NameProperty,
                UiText.Get(ResourceKeys.AssetEditorWindowLocateActiveDocumentText));
            ToolTipService.SetToolTip(
                _owner.OpenAssetDocumentGroupInNewWindowButton,
                UiText.Get(ResourceKeys.AssetEditorWindowOpenGroupInNewWindowText));
            _owner.OpenAssetDocumentGroupInNewWindowButton.SetValue(
                AutomationProperties.NameProperty,
                UiText.Get(ResourceKeys.AssetEditorWindowOpenGroupInNewWindowText));
            ToolTipService.SetToolTip(
                _owner.AssetDocumentGroupDragHandle,
                UiText.Get(ResourceKeys.AssetEditorWindowDragGroupHandleToolTip));
        }

        public void SetWorkspaceImportDropOverlayVisible(bool isVisible)
        {
            if (_isWorkspaceImportDragActive == isVisible)
            {
                return;
            }

            _isWorkspaceImportDragActive = isVisible;
            _owner.WorkspaceImportDropOverlay.Visibility = isVisible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateCollapsedBundlePaneActionVisualState(object sender, bool isPointerOver, bool isPressed)
        {
            if (!TryGetCollapsedBundlePaneActionTargets(
                    sender,
                    out var actionTransform,
                    out var hoverOverlay,
                    out var pressedOverlay,
                    out var hoverStroke,
                    out var storyboard))
            {
                return;
            }

            var hoverOpacity = isPointerOver ? (isPressed ? 0.16d : 0.4d) : 0d;
            var pressedOpacity = isPressed ? 0.52d : 0d;
            var strokeOpacity = isPointerOver ? (isPressed ? 0.52d : 0.26d) : 0d;
            var translateY = isPressed ? 0.5d : isPointerOver ? -1d : 0d;
            var scale = isPressed ? 0.985d : isPointerOver ? 1.015d : 1d;
            var duration = TimeSpan.FromMilliseconds(isPressed ? 80d : isPointerOver ? 115d : 135d);

            storyboard.Stop();
            storyboard.Children.Clear();
            storyboard.Children.Add(CreateDoubleAnimation(hoverOverlay, nameof(UIElement.Opacity), hoverOpacity, duration));
            storyboard.Children.Add(CreateDoubleAnimation(pressedOverlay, nameof(UIElement.Opacity), pressedOpacity, duration));
            storyboard.Children.Add(CreateDoubleAnimation(hoverStroke, nameof(UIElement.Opacity), strokeOpacity, duration));
            storyboard.Children.Add(CreateDoubleAnimation(actionTransform, nameof(CompositeTransform.TranslateY), translateY, duration, enableDependentAnimation: true));
            storyboard.Children.Add(CreateDoubleAnimation(actionTransform, nameof(CompositeTransform.ScaleX), scale, duration, enableDependentAnimation: true));
            storyboard.Children.Add(CreateDoubleAnimation(actionTransform, nameof(CompositeTransform.ScaleY), scale, duration, enableDependentAnimation: true));
            storyboard.Begin();
        }

        public void Stop()
        {
            _collapsedBundleImportFileInteractionStoryboard.Stop();
            _collapsedBundleImportDirectoryInteractionStoryboard.Stop();
        }

        private bool TryGetCollapsedBundlePaneActionTargets(
            object sender,
            out CompositeTransform actionTransform,
            out Border hoverOverlay,
            out Border pressedOverlay,
            out Border hoverStroke,
            out Storyboard storyboard)
        {
            if (ReferenceEquals(sender, _owner.CollapsedBundleImportFileButton))
            {
                actionTransform = _owner.CollapsedBundleImportFileActionTransform;
                hoverOverlay = _owner.CollapsedBundleImportFileHoverOverlay;
                pressedOverlay = _owner.CollapsedBundleImportFilePressedOverlay;
                hoverStroke = _owner.CollapsedBundleImportFileHoverStroke;
                storyboard = _collapsedBundleImportFileInteractionStoryboard;
                return true;
            }

            if (ReferenceEquals(sender, _owner.CollapsedBundleImportDirectoryButton))
            {
                actionTransform = _owner.CollapsedBundleImportDirectoryActionTransform;
                hoverOverlay = _owner.CollapsedBundleImportDirectoryHoverOverlay;
                pressedOverlay = _owner.CollapsedBundleImportDirectoryPressedOverlay;
                hoverStroke = _owner.CollapsedBundleImportDirectoryHoverStroke;
                storyboard = _collapsedBundleImportDirectoryInteractionStoryboard;
                return true;
            }

            actionTransform = null!;
            hoverOverlay = null!;
            pressedOverlay = null!;
            hoverStroke = null!;
            storyboard = null!;
            return false;
        }

        private static DoubleAnimation CreateDoubleAnimation(
            DependencyObject target,
            string propertyPath,
            double to,
            TimeSpan duration,
            bool enableDependentAnimation = false)
        {
            var animation = new DoubleAnimation
            {
                To = to,
                Duration = duration,
                EnableDependentAnimation = enableDependentAnimation,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyPath);
            return animation;
        }
    }
}
