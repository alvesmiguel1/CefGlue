using System;
using System.Collections.Generic;
using System.Linq;
using ServiceStudio.View;

namespace ServiceStudio.Presenter {
    internal partial class AggregatorWindowPresenter : IAggregatorWindow {

        private readonly List<IAggregatorPresenter> aggregators = new();

        private ITopLevelPresenter selectedTopLevelPresenter;
        private IServerPagePresenter serverPagePresenter;

        public AggregatorWindowPresenter(IAggregatorWindowView view) {
            View = view;

            RuntimeImplementation.Instance.AddAggregatorWindow(this);
            SelectedTopLevelPresenter = CreateNewAggregator();
            CreateNewAggregator();

            view.SelectedViewChanged += OnSelectedViewChanged;
        }

        public IEnumerable<ITopLevelPresenter> TopLevelPresenters => new List<ITopLevelPresenter> { ServerPagePresenter }.Concat(aggregators);

        public IEnumerable<IAggregatorPresenter> Aggregators => aggregators;

        public ITopLevelPresenter SelectedTopLevelPresenter {
            get => selectedTopLevelPresenter;
            set {
                if (value != selectedTopLevelPresenter) {
                    selectedTopLevelPresenter = value;
                    if (selectedTopLevelPresenter != null) {
                        // might be null on closing
                        View.SelectedView = selectedTopLevelPresenter.View;
                    }
                }
            }
        }

        public IServerPagePresenter ServerPagePresenter => serverPagePresenter;
        
        public int WindowTop { get; }
        public IAggregatorWindowView View { get; }

        public event Action SelectedTabChanged;

        public void Activate() => View.Activate();

        public void Attach(IAggregatorPresenter aggregator) {
            var sourceWindow = RuntimeImplementation.Instance.GetAggregatorWindow(aggregator);
            sourceWindow.RemoveAggregator(aggregator);
            AttachAggregator(aggregator);
        }

        public void AttachAggregator(IAggregatorPresenter aggregator) {
            InsertAggregator(aggregator, null);
        }

        private void InsertAggregator(IAggregatorPresenter aggregator, int? index) {
            aggregators.Add(aggregator);
            View.InsertAggregator(aggregator.View, index);
        }

        public IAggregatorPresenter CreateNewAggregator() {
            var aggregator = new AggregatorPresenter(this, View.CreateAggregatorView());

            aggregators.Add(aggregator);
            return aggregator;
        }

        public void RemoveAggregator(IAggregatorPresenter aggregator, bool removeFromView = true) {
            if (removeFromView) {
                View.RemoveAggregator(aggregator.View);
            }
            aggregators.Remove(aggregator);

            if (SelectedTopLevelPresenter == aggregator) {
                SelectedTopLevelPresenter = aggregators.LastOrDefault();
                if (SelectedTopLevelPresenter == null) {
                    SelectedTopLevelPresenter = ServerPagePresenter;
                }
            }
        }

        public void Show() {
            View.Show();
        }
        
        private void OnSelectedViewChanged(ITopLevelView topLevelView) {
            // We don't use the 'SelectedTopLevelPresenter' to avoid recursion problems (RMAC-5706)
            // This is caused because 'SelectedTopLevelPresenter' interacts with view by changing the TabControl's currentIndex, what would trigger again a 'SelectedViewChanged';
            selectedTopLevelPresenter = aggregators
                .Concat(new ITopLevelPresenter[] { serverPagePresenter })
                .FirstOrDefault(t => t.View == topLevelView);

            if (SelectedTabChanged != null) {
                SelectedTabChanged();
            }

            if (SelectedTopLevelPresenter != null && SelectedTopLevelPresenter.View != null) {
                if (SelectedTopLevelPresenter is IAggregatorPresenter aggregator) {
                    aggregator.RefreshTitleBarAndStatusBar();
                }
            }
        }
    }
}
