using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class BundleExplorerPageTabCoordinator
    {
        private readonly BundleExplorerPage _owner;
        private bool _isSynchronizingDocumentTabSelection;

        public BundleExplorerPageTabCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public AssetDocumentTabItemViewModel? GetActivatableDocumentTab()
        {
            if (_isSynchronizingDocumentTabSelection ||
                _owner.ViewModel.DocumentWorkspace.IsSynchronizingAssetDocumentHostSelection)
            {
                return null;
            }

            return _owner.AssetDocumentTabView.SelectedItem as AssetDocumentTabItemViewModel;
        }

        public void HandleDocumentWorkspacePageStateChanged(
            BundleExplorerPageStateChangedEventArgs<BundleExplorerDocumentWorkspacePageStateChange> e)
        {
            if (!e.ChangeFlags.HasFlag(BundleExplorerDocumentWorkspacePageStateChange.AssetDocumentHost))
            {
                return;
            }

            Enqueue(SynchronizeDocumentTabSelection);
        }

        public void ConfigureTabContextFlyout(
            MenuFlyout? flyout,
            string? openInNewWindowMenuItemName,
            string closeOtherMenuItemName,
            string closeAllMenuItemName,
            int tabCount,
            bool canOpenInNewWindow)
        {
            if (flyout is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(openInNewWindowMenuItemName))
            {
                var openInNewWindowMenuItem = FindMenuFlyoutItem(flyout, openInNewWindowMenuItemName);
                if (openInNewWindowMenuItem is not null)
                {
                    openInNewWindowMenuItem.Text = UiText.Get(ResourceKeys.AssetEditorWindowOpenInNewWindowText);
                    openInNewWindowMenuItem.IsEnabled = tabCount > 0 && canOpenInNewWindow;
                }
            }

            var closeOtherMenuItem = FindMenuFlyoutItem(flyout, closeOtherMenuItemName);
            if (closeOtherMenuItem is not null)
            {
                closeOtherMenuItem.Text = UiText.Get(ResourceKeys.BundleExplorerPageTabContextCloseOtherText);
                closeOtherMenuItem.IsEnabled = tabCount > 1;
            }

            var closeAllMenuItem = FindMenuFlyoutItem(flyout, closeAllMenuItemName);
            if (closeAllMenuItem is not null)
            {
                closeAllMenuItem.Text = UiText.Get(ResourceKeys.BundleExplorerPageTabContextCloseAllText);
                closeAllMenuItem.IsEnabled = tabCount > 0;
            }
        }

        public bool TryBuildTabOrderSnapshot<TTabViewModel>(
            TabView tabView,
            out IReadOnlyList<TTabViewModel> orderedTabs)
            where TTabViewModel : class
        {
            ArgumentNullException.ThrowIfNull(tabView);

            var resolvedItems = new List<TTabViewModel>(tabView.TabItems.Count);
            foreach (var item in tabView.TabItems)
            {
                if (item is TTabViewModel typedItem)
                {
                    resolvedItems.Add(typedItem);
                    continue;
                }

                if (item is FrameworkElement { DataContext: TTabViewModel dataContextItem })
                {
                    resolvedItems.Add(dataContextItem);
                    continue;
                }

                orderedTabs = Array.Empty<TTabViewModel>();
                return false;
            }

            orderedTabs = resolvedItems;
            return true;
        }

        public bool TryGetTaggedValue<TValue>(
            object sender,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TValue? value)
            where TValue : class
        {
            value = (sender as FrameworkElement)?.Tag as TValue;
            return value is not null;
        }

        public void ScrollActiveDocumentTabIntoView()
        {
            Enqueue(ScrollActiveDocumentTabIntoViewCore);
        }

        /// <summary>
        /// WinUI 的 TabView 在切换 ItemsSource 时可能会先回落到第一项，
        /// 这里显式把控件选中态对齐回 ViewModel 中已记录的活动资产标签，
        /// 避免资源包切换时把“当前详情标签”误写成第一项。
        /// </summary>
        private void SynchronizeDocumentTabSelection()
        {
            if (_isSynchronizingDocumentTabSelection)
            {
                return;
            }

            _isSynchronizingDocumentTabSelection = true;
            try
            {
                if (!ReferenceEquals(_owner.AssetDocumentTabView.SelectedItem, _owner.ViewModel.DocumentWorkspace.ActiveDocumentTab))
                {
                    _owner.AssetDocumentTabView.SelectedItem = _owner.ViewModel.DocumentWorkspace.ActiveDocumentTab;
                }
            }
            finally
            {
                _isSynchronizingDocumentTabSelection = false;
            }
        }

        private void ScrollActiveDocumentTabIntoViewCore()
        {
            SynchronizeDocumentTabSelection();

            var activeDocumentTab = _owner.ViewModel.DocumentWorkspace.ActiveDocumentTab;
            if (activeDocumentTab is null)
            {
                return;
            }

            _owner.AssetDocumentTabView.UpdateLayout();
            if (TryResolveTabElement(activeDocumentTab, out var tabElement))
            {
                tabElement.StartBringIntoView();
            }
        }

        private bool TryResolveTabElement(
            AssetDocumentTabItemViewModel activeDocumentTab,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FrameworkElement? tabElement)
        {
            foreach (var item in _owner.AssetDocumentTabView.TabItems)
            {
                if (item is FrameworkElement { DataContext: var dataContext } itemElement &&
                    ReferenceEquals(dataContext, activeDocumentTab))
                {
                    tabElement = itemElement;
                    return true;
                }
            }

            return TryFindTabElement(_owner.AssetDocumentTabView, activeDocumentTab, out tabElement);
        }

        private static bool TryFindTabElement(
            DependencyObject root,
            AssetDocumentTabItemViewModel activeDocumentTab,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FrameworkElement? tabElement)
        {
            if (root is TabViewItem { DataContext: var dataContext } tabViewItem &&
                ReferenceEquals(dataContext, activeDocumentTab))
            {
                tabElement = tabViewItem;
                return true;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                if (TryFindTabElement(
                        VisualTreeHelper.GetChild(root, index),
                        activeDocumentTab,
                        out tabElement))
                {
                    return true;
                }
            }

            tabElement = null;
            return false;
        }

        private static MenuFlyoutItem? FindMenuFlyoutItem(MenuFlyout flyout, string itemName)
        {
            ArgumentNullException.ThrowIfNull(flyout);
            ArgumentException.ThrowIfNullOrWhiteSpace(itemName);

            return flyout.Items
                .OfType<MenuFlyoutItem>()
                .FirstOrDefault(item => string.Equals(item.Name, itemName, StringComparison.Ordinal));
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
