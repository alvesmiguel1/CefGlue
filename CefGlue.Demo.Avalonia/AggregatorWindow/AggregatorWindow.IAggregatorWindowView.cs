using System;
using Avalonia.Threading;
using ServiceStudio.View;
using ServiceStudio.WebViewImplementation.Framework;

namespace ServiceStudio.WebViewImplementation {
    partial class AggregatorWindow : IAggregatorWindowView {
        ITopLevelView IAggregatorWindowView.SelectedView {
            get => Dispatcher.UIThread.ExecuteInUIThread(() => (ITopLevelView)tabs.SelectedContent);
            set => Dispatcher.UIThread.AsyncExecuteInUIThread(() => tabs.SelectedIndex = GetTabIndex(value));
        }

        private string caption = string.Empty;

        string IView.Caption {
            get => caption;
            set {
                caption = value;
                Dispatcher.UIThread.AsyncExecuteInUIThread(() => Title = caption);
            }
        }

        event Action<ITopLevelView> IAggregatorWindowView.SelectedViewChanged {
            add { selectedAggregatorChanged += value; }
            remove { selectedAggregatorChanged -= value; }
        }

        void IView.Activate() => Dispatcher.UIThread.AsyncExecuteInUIThread(Activate);

        void IAggregatorWindowView.RemoveAggregator(IAggregatorView aggregatorView) {
            Dispatcher.UIThread.AsyncExecuteInUIThread(() => {
                RemoveTab(aggregatorView);
            });
        }

        IAggregatorView IAggregatorWindowView.CreateAggregatorView() {
            return Dispatcher.UIThread.ExecuteInUIThread(() => {
                var tabHeaderInfo = new TabHeaderInfo {
                    Caption = "Tab title",
                    AllowClose = true
                };
                var view = new AggregatorView(tabHeaderInfo);

                AddTab(tabHeaderInfo, view);
                return view;
            });
        }
        
        void IAggregatorWindowView.InsertAggregator(IAggregatorView aggregatorView, int? index) {
            Dispatcher.UIThread.ExecuteInUIThread(() => {
                var concreteAggregatorView = (AggregatorView)aggregatorView;
                AddTab(concreteAggregatorView.TabHeader, concreteAggregatorView);
            });
        }

        void IAggregatorWindowView.Show() {
            Dispatcher.UIThread.AsyncExecuteInUIThread(Show);
        }
    }
}
