using System;
using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ServiceStudio.WebViewImplementation.Framework;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ServiceStudio.Presenter;
using ServiceStudio.View;
using Image = Avalonia.Controls.Image;

namespace ServiceStudio.WebViewImplementation
{
    /// <summary>
    /// Aggregator Window code section handling the drag drop of tab items
    /// </summary>
    partial class AggregatorWindow
    {
        private Object lockMove = new Object();
        private Object lockAdjust = new Object();

        private Window ghostWindow;
        private Control ghostTab;
        private Control ghostSSFacsimile;

        private DockPanel savedDockPanel;
        private TabItem tabItemSelectedForDragDrop;
        private TabDragInfo tabBeingMoved;

        private bool isFirstMove;

        private PixelPoint clickPoint;
        private PixelPoint clickOffset;

        private int tabOffset;

        private int tabStripLeft;
        private int tabStripRight;
        private int tabStripTop;
        private int tabStripBottom;
        private int tabStripHalfHeight;

        private bool hasRail;
        private bool isMoveInProgress;

        private IPointer currentPointer;

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var tabItemTest = sender as TabItem;
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                HandleTabRightMouseClick(sender);
                e.Handled = true;
                return;
            }
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                HandleTabMiddleMouseClick(sender);
                e.Handled = true;
                return;
            }

            // Check for module tab (defined by the fact it is the only tab type that allows the user to close)
            if ((TabHeaderInfo)tabItemTest?.Header is null ||
                !((TabHeaderInfo)tabItemTest.Header).AllowClose ||
                TabItems.Count(t => (TabHeaderInfo)(t.Header) is null || ((TabHeaderInfo)(t.Header)).AllowClose) == 0
               )
            {
                return;
            }

            // Check that the left button has been pressed
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }


            var caption = ((TabHeaderInfo)tabItemTest.Header).Caption;
            if (caption is null)
            {
                return;
            }

            tabItemSelectedForDragDrop = tabItemTest;

            var tempTabItems = this.tabs.Items.Cast<TabItem>().ToArray();
            var count = tempTabItems.Length;

            // Get first module tab item
            int tabIndex;
            TabItem tabItem = null;
            PixelPoint originPoint = default;
            for (tabIndex = 0; tabIndex < count; tabIndex++)
            {
                tabItem = tempTabItems[tabIndex];
                if ((TabHeaderInfo)tabItem.Header != null && ((TabHeaderInfo)tabItem.Header).AllowClose)
                {
                    originPoint = tabItem.PointToScreen(new Point(0, 0));
                    break;
                }
            }
            // The first module tab's origin point represents the top-left corner of the rail
            tabStripLeft = originPoint.X;
            tabStripTop = originPoint.Y;

            // Has a rail when there are one or more module tabs
            hasRail = (count - tabIndex) >= 1;

            // Build list of tab drag infos from module tab items (skipping last tab module)
            TabDragInfo tabInfoBefore = null;
            for (; tabIndex < count - 1; tabIndex++)
            {
                // The next tab item is needed to determine width of the current tab item
                var nextTabItem = tempTabItems[tabIndex + 1];
                var nextOriginPoint = nextTabItem.PointToScreen(new Point(0, 0));

                // Create tab drag info for tab at <tabIndex>
                var tabInfo = new TabDragInfo(tabItem, tabIndex, originPoint, (nextOriginPoint.X - originPoint.X), tabInfoBefore);
                if (tabItem == tabItemSelectedForDragDrop)
                {
                    tabBeingMoved = tabInfo;
                }

                // The next tab item becomes the <tabIndex> tab item
                tabInfoBefore = tabInfo;
                tabItem = nextTabItem;
                originPoint = nextOriginPoint;
            }

            if (tabItem is null)
            {
                // if we don't have a last tab, bail out
                return;
            }

            // Width of the last tab is determined by the last tab's bounds
            var lastEndPoint = tabItem.PointToScreen(new Point(tabItem.Bounds.Width, tabItem.Bounds.Height));

            // The last tab's end point represents the end of the rail
            tabStripRight = lastEndPoint.X;
            tabStripBottom = lastEndPoint.Y;
            // Vertical center of the tabstrip (rail)
            tabStripHalfHeight = (tabStripBottom - tabStripTop) / 2;

            // Add last tab module which width is calculated from its bounds
            var lastTabInfo = new TabDragInfo(tabItem, tabIndex, originPoint, (lastEndPoint.X - originPoint.X), tabInfoBefore);
            if (tabItem == tabItemSelectedForDragDrop)
            {
                tabBeingMoved = lastTabInfo;
            }

            // Determine initial pointer's relative position to the tab's origin
            var tabOriginPoint = tabBeingMoved.originPoint;
            clickPoint = tabBeingMoved.tabItem.PointToScreen(e.GetPosition(tabBeingMoved.tabItem));
            clickOffset = clickPoint - tabOriginPoint;

            tabOffset = (tabBeingMoved.GetMinWidth() / 2) - 1;

            DeleteGhostWindow();

            var ghostWidth = tabItemSelectedForDragDrop.Bounds.Width;
            var ghostHeight = tabItemSelectedForDragDrop.Bounds.Height;

            ghostTab = CreateGhostTab(caption, ghostWidth, ghostHeight, tabItemSelectedForDragDrop.Background, tabItemSelectedForDragDrop.Foreground);
            ghostSSFacsimile = CreateGhostSSFacsimile(caption, this.Width, this.Height, tabItemSelectedForDragDrop.Background, tabItemSelectedForDragDrop.Foreground);

            var ghostPosition = tabOriginPoint - new PixelPoint(10, 0);
            ghostWindow = CreateGhostWindow(caption, ghostWidth, ghostHeight, ghostPosition);
            ghostWindow.KeyDown += OnKeyDownWhileDragging;
            SetPointerCapture(e.Pointer);

            // Trick to make current tab look like it is moving by making the existing tab foreground invisible.
            if (hasRail)
            {
                // tabItemSelectedForDragDrop can be null at this phase because CreateGhostWindow allows the jobs that were in the queue to be run.
                // It's probably a bug on avalonia because it seems it is allowing to run operations that were pushed to be run async to run at the CreateWindow phase
                // That allows the RemoveTab that was pushed to run async to run at the CreateWindow phase and will set tabItemSelectedForDragDrop as null
                // Didn't fix changing the order because that could lead to breaking this in the future if we forgot the order matters and the savedDockPanel won't be needed as the tab was closed
                // To be followed by RDOIAT-602
                savedDockPanel = (DockPanel)((IStyledElement)tabItemSelectedForDragDrop)?.LogicalChildren.FirstOrDefault(c => c is DockPanel);
            }

            lock (lockMove)
            {
                isFirstMove = true;
                this.PointerLeave += OnPointerLeave;
                this.PointerCaptureLost += OnPointerCaptureLost;
                this.PointerMoved += OnPointerMove;
                this.PointerReleased += OnPointerReleased;
            }
        }

        private void HandleTabRightMouseClick(object sender)
        { }

        private void HandleTabMiddleMouseClick(object sender)
        { }

        private void OnKeyDownWhileDragging(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelTabMove(currentPointer);
            }
        }

        private void OnPointerLeave(object sender, PointerEventArgs e)
        {
            SetPointerCapture(e.Pointer);
        }

        private void OnPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            CancelTabMove(e.Pointer);
        }

        private void OnPointerMove(object sender, PointerEventArgs e)
        {
            // Cursor in screen coordinates
            var currentPoint = this.PointToScreen(e.GetPosition(this));
            if (isFirstMove)
            {
                // Move a bit before showing the ghost window
                var pointDiff = currentPoint - clickPoint;
                if (Math.Abs(pointDiff.X) <= 11 && Math.Abs(pointDiff.Y) <= 11)
                {
                    SetPointerCapture(e.Pointer);
                    return;
                }
                // Show ghost window
                lock (lockMove)
                {
                    if (isFirstMove)
                    {
                        isFirstMove = false;
                        isMoveInProgress = true;
                        ghostWindow.Show();
                        SetPointerCapture(e.Pointer);
                    }
                }
            }
            else
            {
                if (IsPointOnRail(currentPoint, out PixelPoint originPoint))
                {
                    // Move ghost window along rail
                    if (!Object.ReferenceEquals(ghostWindow.Content, ghostTab))
                    {
                        ghostWindow.SystemDecorations = SystemDecorations.None;
                        ghostWindow.Content = ghostTab;
                        ghostWindow.InvalidateMeasure();
                        HideTabItem();
                    }
                    ghostWindow.Position = new PixelPoint(originPoint.X - 11, tabStripTop);
                    AdjustTabPositions(originPoint.X);
                }
                else
                {
                    // Move ghost window off of rail
                    if (!Object.ReferenceEquals(ghostWindow.Content, ghostSSFacsimile))
                    {
                        ghostWindow.SystemDecorations = SystemDecorations.BorderOnly;
                        ghostWindow.Content = ghostSSFacsimile;
                        ghostWindow.InvalidateMeasure();
                        ShowTabItem();
                    }
                    ghostWindow.Position = originPoint;
                }
            }
        }

        private void OnPointerReleased(object sender, PointerEventArgs e)
        {
            EndTabMove(e.Pointer);

            var currentPoint = this.PointToScreen(e.GetPosition(this));
            if (IsPointOnRail(currentPoint, out PixelPoint originPoint))
            {
                AdjustTabPositions(originPoint.X);
            }
            else
            {
                SpawnTabToInstance(currentPoint);
            }

            tabBeingMoved = null;
        }

        private void EndTabMove(IPointer pointer)
        {
            // No longer subscribe to pointer events.
            this.PointerLeave -= OnPointerLeave;
            this.PointerCaptureLost -= OnPointerCaptureLost;
            this.PointerMoved -= OnPointerMove;
            this.PointerReleased -= OnPointerReleased;

            DeleteGhostWindow();
            pointer?.Capture(null);
            ShowTabItem();

            isMoveInProgress = false;
        }

        private void CancelTabMove(IPointer pointer)
        {
            EndTabMove(pointer);
            tabBeingMoved = null;
        }

        private void SetPointerCapture(IPointer pointer)
        {
            pointer.Capture(this);
            currentPointer = pointer;
        }

        private bool IsPointOnRail(PixelPoint currentPoint, out PixelPoint originPoint)
        {
            // Compute the adjusted origin and enter points
            originPoint = currentPoint - clickOffset;
            var centerPoint = originPoint + new PixelPoint(tabBeingMoved.halfWidth, tabStripHalfHeight);

            return (
                // more than one module tab
                hasRail
                // between top and bottom of strip - between top and bottom of rail
                && (tabStripTop - 11 < centerPoint.Y && centerPoint.Y < tabStripBottom + 11)
                // between left and right of modules part of tab strip - between left and right of rail
                && (tabStripLeft < centerPoint.X && centerPoint.X < tabStripRight)
            );
        }

        private void AdjustTabPositions(int xOriginAdjusted)
        {
            lock (lockAdjust)
            {
                // AdjustPosition returns true when the tab being moved switches position with the tab before or after
                if (tabBeingMoved.AdjustPosition(xOriginAdjusted, tabOffset, out int originalIndex, out int moveToIndex))
                {
                    // Swap the tab positions (do this on the UIThread at a priority before input)
                    Avalonia.Threading.Dispatcher.UIThread.AsyncExecuteInUIThread(
                        () =>
                        {
                            var list = (IList)tabs.Items;
                            var tab = list[moveToIndex];
                            list.RemoveAt(moveToIndex);
                            list.Insert(originalIndex, tab);
                            tabs.SelectedIndex = moveToIndex;
                        },
                        Avalonia.Threading.DispatcherPriority.Loaded
                    );
                }
            }
        }

        Rect rect;

        private void HideTabItem()
        {
            if (hasRail && isMoveInProgress && tabItemSelectedForDragDrop != null)
            {
                if (!(savedDockPanel is null))
                {
                    rect = tabItemSelectedForDragDrop.Bounds;
                    savedDockPanel.IsVisible = false;
                    tabItemSelectedForDragDrop.Width = rect.Width;
                }

                ((IPseudoClasses)(tabItemSelectedForDragDrop).Classes).Set(":hide", true);
            }
        }

        private void ShowTabItem()
        {
            if (hasRail && isMoveInProgress && tabItemSelectedForDragDrop != null)
            {
                if (!(savedDockPanel is null))
                {
                    tabItemSelectedForDragDrop.Width = double.NaN;
                    savedDockPanel.IsVisible = true;
                }

                ((IPseudoClasses)(tabItemSelectedForDragDrop).Classes).Set(":hide", false);
            }
        }

        private static Control CreateGhostTab(string caption, double width, double height, IBrush backgroundBrush, IBrush foregroundBrush)
        {
            var textBlock = new TextBlock()
            {
                Text = caption,
                Width = width - 50,
                FontSize = 12,
                Margin = new Thickness(16, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var canvas = new Canvas()
            {
                Width = 20,
                Height = 10,
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Children.Add(new Line() { StartPoint = new Point(0, 0), EndPoint = new Point(8, 8), Stroke = foregroundBrush, StrokeThickness = 1 });
            canvas.Children.Add(new Line() { StartPoint = new Point(8, 0), EndPoint = new Point(0, 8), Stroke = foregroundBrush, StrokeThickness = 1 });

            var dockPanel = new DockPanel() { Height = height, Background = Brush.Parse("#F7F8FA"), VerticalAlignment = VerticalAlignment.Top, };
            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(canvas);

            var borderLineBrushColor = "#ffe0e2e4";
            Border border = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Brush.Parse(borderLineBrushColor),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(1, 1, 1, 0),
                Child = dockPanel
            };

            var panel = new Panel() { Width = width + 2 + (2 * 11), Margin = new Thickness(11, 0, 11, 0), Background = Brushes.Transparent, };
            panel.Children.Add(border);

            return panel;
        }

        private static int GetSizeForFacsimile(double currentSize, double minSize, int facsimileShrinkageFactor) =>
            (double.IsNaN(currentSize) || currentSize < minSize) ? (int) (minSize/facsimileShrinkageFactor) : (int) (currentSize/facsimileShrinkageFactor);
        
        private const int FacsimileShrinkageFactor = 3;
        
        private static Control CreateGhostSSFacsimile(string caption, double currentSSWindowWidth, double currentSSWindowHeight, IBrush backgroundBrush, IBrush foregroundBrush)
        {
            var width = GetSizeForFacsimile(1792, 1080, FacsimileShrinkageFactor);
            // load bitmap image
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            var ghostWindowThemeUri = GhostSSFacsimileLightImageUri;

            var bitmapOrig = new Bitmap(assets.Open(new Uri(ghostWindowThemeUri)));
            var height = (int)(width * bitmapOrig.Size.Height / bitmapOrig.Size.Width);
            var scaledBitmap = bitmapOrig.CreateScaledBitmap(new PixelSize(width, height));
            var ghostTabImage = new Image {Source = scaledBitmap};

            var ghostContainer = new Canvas {
                Background = Brushes.Transparent,
                Width = width,
                Height = height
            };
            ghostContainer.Children.Add(ghostTabImage);
            ghostTabImage.SetValue(Canvas.TopProperty, 0);
            ghostTabImage.SetValue(Canvas.LeftProperty, 0);

            return ghostContainer;
        }

        private static Window CreateGhostWindow(string caption, double width, double height, PixelPoint windowPosition)
        {
            return new Window()
            {
                SystemDecorations = SystemDecorations.BorderOnly,
                CanResize = false,
                Focusable = false,
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                TransparencyBackgroundFallback = Brushes.Transparent,
                TransparencyLevelHint = WindowTransparencyLevel.Transparent,
                CornerRadius = new CornerRadius(0),
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SizeToContent = SizeToContent.WidthAndHeight,
            };
        }

        private void DeleteGhostWindow()
        {
            if (!(ghostWindow is null))
            {
                ghostWindow.KeyDown -= OnKeyDownWhileDragging;
                ghostWindow.Close();
                ghostWindow = null;
            }
        }

        private void SpawnTabToInstance(PixelPoint dropPoint)
        {
            var tabTopLevelView = tabBeingMoved.tabItem.Content as ITopLevelView;

            var aggregatorWindow = GetAggregatorWindow(tabTopLevelView);
            if (aggregatorWindow is null)
            {
                return;
            }

            var aggregatorPresenter = GetAggregatorPresenter(tabTopLevelView, aggregatorWindow);
            if (aggregatorPresenter is null)
            {
                return;
            }

            var newAggregatorWindow = RuntimeImplementation.Instance.CreateAggregatorWindow();


            RemoveTab(aggregatorPresenter.View);
            aggregatorWindow.RemoveAggregator(aggregatorPresenter, removeFromView: false);
            newAggregatorWindow.AttachAggregator(aggregatorPresenter);

            newAggregatorWindow.Show(); // show window before changing login to avoid dialogs from appearing before main window shown
            newAggregatorWindow.SelectedTopLevelPresenter = aggregatorPresenter; // select tab after has been created, otherwise won't be selected
        }

        private static bool IsLoading(TabItem tabItemTest)
        {
            var tabTopLevelView = tabItemTest.Content as IAggregatorView;
            if (tabTopLevelView is null)
            {
                return true;
            }

            var aggregatorWindow = GetAggregatorWindow(tabTopLevelView);
            if (aggregatorWindow is null)
            {
                return true;
            }

            var aggregatorPresenter = GetAggregatorPresenter(tabTopLevelView, aggregatorWindow);

            return true;
        }

        private static IAggregatorWindow GetAggregatorWindow(ITopLevelView tabTopLevelView)
        {
            return RuntimeImplementation.Instance.GetAllTopLevelPresenters()
                .Where(tlp => tlp.GetView() == tabTopLevelView)
                .Select(tlp => RuntimeImplementation.Instance.GetAggregatorWindow(tlp as IAggregatorPresenter))
                .FirstOrDefault();
        }

        private static IAggregatorPresenter GetAggregatorPresenter(ITopLevelView tabTopLevelView, IAggregatorWindow aggregatorWindow)
        {
            return aggregatorWindow.Aggregators.FirstOrDefault(p => p.View == tabTopLevelView);
        }
    }
}
