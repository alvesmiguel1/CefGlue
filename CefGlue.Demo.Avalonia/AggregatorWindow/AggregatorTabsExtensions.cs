using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ServiceStudio.Presenter;
using ServiceStudio.View;

namespace ServiceStudio.WebViewImplementation {
    public static class AggregatorTabsExtensions {
        public static bool IsLoading(this IContentControl tab) {
            if (tab.Content is not IAggregatorView) {
                return true;
            }

            if (tab.GetAggregatorWindowPresenter() is null) {
                return true;
            }

            return false;
        }

        public static IAggregatorWindow FindAggregatorWindowPresenterWithPointOnTabs(this IEnumerable<IAggregatorWindow> aggregatorWindows, PixelPoint currentPoint, int epsilon) {
            var aggregator = FindVisibleAggregatorPresenterWithPointOnWindow(aggregatorWindows, currentPoint);
            return aggregator?.IsPointOnTabs(currentPoint, epsilon) == true ? aggregator : null;
        }

        public static IAggregatorWindow FindAggregatorWindowPresenterWithPointOnTopBar(this IEnumerable<IAggregatorWindow> aggregatorWindows, PixelPoint currentPoint, int epsilon) {
            var aggregator = FindVisibleAggregatorPresenterWithPointOnWindow(aggregatorWindows, currentPoint);
            return aggregator?.IsPointOnTopBar(currentPoint, epsilon) == true ? aggregator : null;
        }

        public static IAggregatorWindow FindVisibleAggregatorPresenterWithPointOnWindow(this IEnumerable<IAggregatorWindow> aggregatorWindows, PixelPoint point) {
            var candidates = new List<IAggregatorWindow>();

            foreach (var agg in aggregatorWindows) {
                var aggWindow = GetAggregatorView(agg);
                var (x, y) = aggWindow.PointToClient(point);
                if (aggWindow.Bounds.Contains(new Point(x, y))) {
                    candidates.Add(agg);
                }
            }
            
            return candidates.FirstOrDefault();
        }

        public static bool IsPointOnTopBar(this IAggregatorWindow aggregatorWindowPresenter, PixelPoint adjustedOriginPoint, int epsilon) {
            var tabsControl = aggregatorWindowPresenter.GetAggregatorView().FindDescendantOfType<TabItemsControl>();
            var clientPoint = tabsControl.PointToClient(adjustedOriginPoint);
            return tabsControl.Bounds.Inflate(epsilon).Contains(clientPoint);
        }

        public static bool IsPointOnTabs(this IAggregatorWindow aggregatorWindowPresenter, PixelPoint adjustedOriginPoint, int epsilon) {
            var tabsControl = aggregatorWindowPresenter.GetAggregatorView().FindDescendantOfType<TabItemsControl>();
            var image = tabsControl.FindDescendantOfType<Image>();
            var tabs = tabsControl.FindDescendantOfType<TabItemsControl.InnerTabItemsControl>();
            var fixedTabs = tabs.Children.Take(GetFirstDraggableTabIndex(aggregatorWindowPresenter));

            var clientPoint = tabsControl.PointToClient(adjustedOriginPoint);

            var isPointOnImage = image.Bounds.Inflate(epsilon).Contains(clientPoint);
            var isPointOnFixedTabs = fixedTabs.Any(t => t.Bounds.Inflate(epsilon).Contains(clientPoint));

            return !isPointOnImage && !isPointOnFixedTabs && IsPointOnTopBar(aggregatorWindowPresenter, adjustedOriginPoint, epsilon);
        }

        public static int GetTabIndex(this IAggregatorWindow aggregatorWindowPresenter, IContentControl tab) =>
            1;

        public static int CalculateTabIndexForPosition(this IAggregatorWindow aggregatorWindowPresenter, IContentControl tab, int xPosition) {
            var tabItems = aggregatorWindowPresenter.GetAggregatorView().TabItems.ToArray();
            var originalIndex = 1;

            // At least one more draggable tab is needed for tab swapping
            var firstDraggableTabIndex = aggregatorWindowPresenter.GetFirstDraggableTabIndex();
            if (tabItems.Length <= firstDraggableTabIndex + 1) {
                return originalIndex;
            }

            var previousTab = originalIndex > firstDraggableTabIndex ? tabItems[originalIndex - 1] : null;
            var nextTab = originalIndex < tabItems.Length - 1 ? tabItems[originalIndex + 1] : null;

            if (previousTab is not null) {
                var previousTabOrigin = GetTopLeftPoint(previousTab);
                var previousTabWidth = GetWidth(previousTab);

                if (xPosition < previousTabOrigin.X + previousTabWidth/2) {
                    return originalIndex - 1;
                }
            }

            if (nextTab is not null) {
                var tabWidth = GetWidth(tab);
                var nextTabOrigin = GetTopLeftPoint(nextTab);
                var nextTabWidth = GetWidth(nextTab);

                if (xPosition + tabWidth > nextTabOrigin.X + nextTabWidth/2) {
                    return originalIndex + 1;
                }
            }

            return originalIndex;
        }

        public static ITopLevelView GetTopLevelView(this IContentControl tab) => tab.Content as ITopLevelView;

        public static IAggregatorWindow GetAggregatorWindowPresenter(this IContentControl tab)
        {
            var top = RuntimeImplementation.Instance.GetAllTopLevelPresenters()
                .First(tlp => tlp.GetView() == tab.GetTopLevelView());
            return RuntimeImplementation.Instance.GetAggregatorWindow((IAggregatorPresenter)top);
        }

        public static IAggregatorPresenter GetAggregatorPresenter(this IContentControl tab) =>
            (IAggregatorPresenter)RuntimeImplementation.Instance.GetAllTopLevelPresenters()
                .First(tlp => tlp.GetView() == tab.GetTopLevelView());

        internal static AggregatorWindow GetAggregatorView(this IAggregatorWindow aggregatorWindow) => (AggregatorWindow)aggregatorWindow?.View;

        internal static AggregatorWindow GetAggregatorView(this IContentControl tab) => tab.GetAggregatorWindowPresenter().GetAggregatorView();

        public static int GetWidth(this IContentControl tab) => tab.Bounds.Width > 0 ? (int) tab.Bounds.Width : (int) tab.MinWidth;

        public static int GetHeight(this IContentControl tab) => tab.Bounds.Height > 0 ? (int) tab.Bounds.Height : (int) tab.MinHeight;

        public static PixelPoint GetTopLeftPoint(this IContentControl tab) => tab.PointToScreen(new Point(0, 0));

        public static int GetFirstDraggableTabIndex(this IAggregatorWindow aggregatorWindowPresenter) {
            var tabs = GetAggregatorView(aggregatorWindowPresenter).TabItems.ToArray();
            for (int i = 0; i < tabs.Length; i++)
            {
                if (((TabHeaderInfo)(tabs[i].Header))?.AllowClose == true)
                {
                    return i;
                }
            }

            return 0;
        }
            

        public static TabItem GetFirstDraggableTab(this IAggregatorWindow aggregatorWindowPresenter) =>
            GetAggregatorView(aggregatorWindowPresenter).TabItems.ElementAtOrDefault(GetFirstDraggableTabIndex(aggregatorWindowPresenter));

        public static bool HasDraggableTabs(this IAggregatorWindow aggregatorWindowPresenter) => GetFirstDraggableTab(aggregatorWindowPresenter) != null;

    }
}
