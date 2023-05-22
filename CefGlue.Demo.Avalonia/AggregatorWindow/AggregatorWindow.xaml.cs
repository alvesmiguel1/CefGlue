using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ServiceStudio.View;
using ServiceStudio.WebViewImplementation.Framework.Tooltip;

namespace ServiceStudio.WebViewImplementation {
    internal partial class AggregatorWindow : Window {
        private readonly TabControl tabs;
        private Action<ITopLevelView> selectedAggregatorChanged;

        private readonly ITabDragDropStrategy tabDragDropStrategy;
        public AggregatorWindow() {
            AvaloniaXamlLoader.Load(this);
            tabs = this.FindControl<TabControl>("tabs");
            tabDragDropStrategy = ReAttachableTabDragDropStrategy.GetInstance();
        }

        public static readonly StyledProperty<Thickness> TitleBarMarginProperty =
            AvaloniaProperty.Register<AggregatorWindow, Thickness>(nameof(TitleBarMargin), defaultValue: new Thickness(0), inherits: true);

        public Thickness TitleBarMargin {
            get => GetValue(TitleBarMarginProperty);
            private set => SetValue(TitleBarMarginProperty, value);
        }

        internal IEnumerable<TabItem> TabItems => tabs.Items.Cast<TabItem>();

        private void OnSelectedTabChanged(object sender, SelectionChangedEventArgs e) {
            var tabItem = e.AddedItems.OfType<TabItem>().FirstOrDefault()?.Content as ITopLevelView;

            selectedAggregatorChanged?.Invoke(tabItem);
        }

        private void SelectTab(TabItem tabItem) {
            if (tabItem.Content != null) {
                tabs.SelectedIndex = GetTabIndex(tabItem.Content as ITopLevelView);
            }
        }

        private void ShowTooltipFor(TabItem tabItem, TabHeaderInfo tabHeaderInfo, PointerEventArgs e) {
            var position = e.GetPosition(this);
            TooltipServiceProvider.ShowTooltip(this, "Amazing tooltip", position.X, position.Y, showDelayed: true);
        }

        private void OnTabCloseButtonClick(object sender, RoutedEventArgs e) {
            var button = (Button)sender;
            button.IsEnabled = false;

            var tabHeaderInfo = (TabHeaderInfo)button.DataContext;
            tabHeaderInfo?.TriggerClose().ContinueWith(t => button.IsEnabled = true, TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        protected override void OnClosed(EventArgs e) {
            tabDragDropStrategy.OnClosed();
            base.OnClosed(e);
        }

        private int GetTabIndex(ITopLevelView topLevelView) => Array.IndexOf(TabItems.Select(t => t.Content).ToArray(), topLevelView);

        private void AddTab(TabHeaderInfo header, Control view, bool isVisible = true, bool isEnabled = true) {
            var tab = new TabItem() {
                Content = view,
                DataContext = header,
                Header = header,
                IsVisible = isVisible,
                IsEnabled = isEnabled
            };

            var isMouseInside = false;

            void OnMouseLeft() {
                isMouseInside = false;
                TooltipServiceProvider.HideTooltip();
            }

            tab.PointerMoved += (sender, e) => {
                // in order to properly get the position we need to listen to pointer moved
                if (isMouseInside) {
                    return;
                }
                isMouseInside = true;
                ShowTooltipFor(tab, header, e);
            };
            tab.PointerLeave += delegate { OnMouseLeft(); };
            tab.Tapped += delegate { OnMouseLeft(); };
            tab.LostFocus += delegate { OnMouseLeft(); };
            tab.PointerPressed += (sender, e) => tabDragDropStrategy.OnPointerPressed(this, sender, e);;
            AddDragDropHandlers(tab);

            var pos = ((IList)tabs.Items).Add(tab);
        }

        private void RemoveTab(IAggregatorView aggregatorView) {
            var index = GetTabIndex(aggregatorView);

            if (tabs.SelectedIndex == index) {
                tabs.SelectedIndex = index - 1;
            }

            var itemsList = (IList<object>)tabs.Items;
            var currentTab = (TabItem)itemsList.ElementAt(index);
            currentTab.Content = null;
            currentTab.DataContext = null;
            currentTab.Header = null;
            itemsList.RemoveAt(index);

            TooltipServiceProvider.HideTooltip();
        }

        private void AddDragDropHandlers(TabItem tab) {
            Avalonia.Input.DragDrop.SetAllowDrop(tab, true);
            tab.AddHandler(Avalonia.Input.DragDrop.DragEnterEvent, OnDragEnter);
        }

        private void OnDragEnter(object sender, Avalonia.Input.DragEventArgs e) {
            tabs.SelectedIndex = GetTabIndex(((TabItem)sender).Content as ITopLevelView);
        }
    }
}
