using Avalonia.Input;
using ServiceStudio.View;

namespace ServiceStudio.WebViewImplementation;

partial class AggregatorWindow {
    internal interface ITabDragDropStrategy {
        void OnPointerPressed(IAggregatorWindowView aggregatorWindow, object sender, PointerPressedEventArgs e);
        void OnClosed();
    }
}
