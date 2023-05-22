using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace ServiceStudio.WebViewImplementation {

    internal class TabItemsControl : Grid, IPanel {

        internal class InnerTabItemsControl : StackPanel {

            private const int TabItemMaxWidth = 232;
            private const int TabItemMinWidth = 80;

            public InnerTabItemsControl() {
                Orientation = Orientation.Horizontal;
            }

            protected override void ChildrenChanged(object sender, NotifyCollectionChangedEventArgs e) {
                base.ChildrenChanged(sender, e);
                if (e.NewItems != null) {
                    foreach (var newItem in e.NewItems.OfType<Layoutable>()) {
                        var tabHeaderInfo = (TabHeaderInfo)newItem.DataContext;
                        if (!tabHeaderInfo.IsFixedSize) {
                            newItem.MinWidth = TabItemMinWidth;
                            newItem.MaxWidth = TabItemMaxWidth;
                        }
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize) {
                var childrenWithFixedSizeWidth = 0.0;
                var height = 0.0;
                var childrenWithVariableSize = new List<IControl>();

                foreach (var child in Children) {
                    var tabHeaderInfo = (TabHeaderInfo)child.DataContext;
                    if (tabHeaderInfo.IsFixedSize) {
                        child.Measure(availableSize);
                        childrenWithFixedSizeWidth += child.DesiredSize.Width;
                        height = Math.Max(child.DesiredSize.Height, height);
                    } else {
                        childrenWithVariableSize.Add(child);
                    }
                }

                var visualParent = this.GetVisualParent<IControl>();
                // Fetch ToggleButton witdh and/if others controls after this
                var siblingsAfter = visualParent.VisualChildren.OfType<Layoutable>().SkipWhile(child => child != this).Skip(1).ToArray();
                var siblingsTotalWidth = 0.0;
                foreach (var sibling in siblingsAfter) {
                    if(!sibling.IsMeasureValid) {
                        sibling.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    }
                    siblingsTotalWidth += sibling.DesiredSize.Width;
                }

                var availableWidth = availableSize.Width - childrenWithFixedSizeWidth - siblingsTotalWidth;
                var width = childrenWithFixedSizeWidth;

                if (availableWidth > 0 && childrenWithVariableSize.Count > 0) {
                    var tabItemAvailableMaxWidth = availableWidth / childrenWithVariableSize.Count;

                    foreach (var child in childrenWithVariableSize) {
                        child.Measure(new Size (tabItemAvailableMaxWidth, double.PositiveInfinity));
                        width += child.DesiredSize.Width;
                        height = Math.Max(child.DesiredSize.Height, height);
                    }
                }

                return new Size(width, height);
            }
        }

        private readonly IPanel tabItemsPanel = new InnerTabItemsControl();

        Controls IPanel.Children => tabItemsPanel.Children;

        public static readonly StyledProperty<int> TabItemsIndexProperty =
           AvaloniaProperty.Register<TabItemsControl, int>(nameof(TabItemsIndex));

        public int TabItemsIndex {
            get => GetValue(TabItemsIndexProperty);
            set => SetValue(TabItemsIndexProperty, value);
        }

        protected override void OnInitialized() {
            this.GetVisualDescendants().OfType<DockPanel>().First().Children.Insert(TabItemsIndex, tabItemsPanel);
        }
    }
}