using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ServiceStudio.WebViewImplementation;

partial class AggregatorWindow : ITabClickHandler {
    void ITabClickHandler.HandleTabRightMouseClick(object sender) {
        var tabItem = sender as TabItem ?? (sender as TextBlock)?.FindAncestorOfType<TabItem>();
        var textBlock = sender as TextBlock ?? (sender as TabItem)?.FindDescendantOfType<TextBlock>();

        SelectTab(tabItem);
    }

    void ITabClickHandler.HandleTabMiddleMouseClick(object sender) {
        var tabItem = sender as TabItem ?? (sender as IVisual)?.FindAncestorOfType<TabItem>();
        var tabHeaderInfo = tabItem?.DataContext as TabHeaderInfo;
        tabHeaderInfo?.TriggerClose();
    }
}
