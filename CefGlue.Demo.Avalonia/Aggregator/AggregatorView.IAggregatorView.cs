using Avalonia.Controls;
using Avalonia.Threading;
using ServiceStudio.View;
using ServiceStudio.WebViewImplementation.Framework;

namespace ServiceStudio.WebViewImplementation {
    partial class AggregatorView : IAggregatorView {
        string IView.Caption {
            get => TabHeader.Caption;
            set => Dispatcher.UIThread.AsyncExecuteInUIThread(() => TabHeader.Caption = value);
        }

        void IView.Activate() { }
        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}
