using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServiceStudio.Presenter;
using ServiceStudio.View;
using ServiceStudio.WebViewImplementation.Framework;

namespace ServiceStudio.WebViewImplementation;

/// <summary>
/// Aggregator Window code section handling the drag drop of tab items
/// </summary>
partial class AggregatorWindow {

    private const int Epsilon = 11;
    private const int AdjustForCurvedCorners = 10;
    
    internal class ReAttachableTabDragDropStrategy : ITabDragDropStrategy {
        private const string Context = "AggregatorWindow_TabDragDrop";

        private Window ghostWindow;
        private Control ghostTab;
        private Control ghostFacsimile;

        private Guid journeyId;

        private TabItem draggedTab;
        private string caption;
        private int draggedTabStartIndex;
        private int draggedTabTargetIndex;

        private bool isFirstMove;
        private bool isMoveInProgress;

        private PixelPoint clickPoint;
        private PixelPoint clickOffset;
        private IPointer currentPointer;

        private IAggregatorWindow dragStartAggregatorWindowPresenter;
        private IAggregatorWindow dragAndDropPreviewTabAggregatorWindowPresenter;
        private IAggregatorWindow dropTargetAggregatorWindowPresenter;
        private AggregatorWindow dragStartAggregatorWindow;

        private static ITabDragDropStrategy instance;

        private ReAttachableTabDragDropStrategy() { }

        public static ITabDragDropStrategy GetInstance() {
            return instance ??= new ReAttachableTabDragDropStrategy();
        }

        public void OnPointerPressed(IAggregatorWindowView callerAggregatorWindow, object sender, PointerPressedEventArgs e) {
            dragStartAggregatorWindow = (AggregatorWindow)callerAggregatorWindow;

                if (e.GetCurrentPoint(dragStartAggregatorWindow).Properties.IsRightButtonPressed) {
                    ((ITabClickHandler)dragStartAggregatorWindow).HandleTabRightMouseClick(sender);
                    e.Handled = true;
                    return;
                }
                if (e.GetCurrentPoint(dragStartAggregatorWindow).Properties.IsMiddleButtonPressed) {
                    ((ITabClickHandler)dragStartAggregatorWindow).HandleTabMiddleMouseClick(sender);
                    e.Handled = true;
                    return;
                }

                draggedTab = (TabItem)sender;
                caption = ((TabHeaderInfo)draggedTab.Header)?.Caption;

                if (caption is null || draggedTab.IsLoading() || !e.GetCurrentPoint(dragStartAggregatorWindow).Properties.IsLeftButtonPressed) {
                    return;
                }

                dragStartAggregatorWindowPresenter = draggedTab.GetAggregatorWindowPresenter();
                if (dragStartAggregatorWindowPresenter.GetFirstDraggableTab() is null) {
                    return;
                }

                // Determine initial pointer's relative position to the tab's origin
                clickPoint = draggedTab.PointToScreen(e.GetPosition(draggedTab));
                clickOffset = clickPoint - draggedTab.GetTopLeftPoint();
                var draggedTabContent = draggedTab.Content as ITopLevelView;
                draggedTabTargetIndex = draggedTabStartIndex = dragStartAggregatorWindowPresenter.GetAggregatorView().GetTabIndex(draggedTabContent);

                SetPointerCapture(e.Pointer);

                isFirstMove = true;
                dragStartAggregatorWindow.PointerLeave += OnPointerLeave;
                dragStartAggregatorWindow.PointerCaptureLost += OnPointerCaptureLost;
                dragStartAggregatorWindow.PointerMoved += OnPointerMove;
                dragStartAggregatorWindow.PointerReleased += OnPointerReleased;
                
        }

        public void OnClosed() {
            DeleteGhostWindow();
            draggedTab = null;
        }

        private void OnPointerLeave(object sender, PointerEventArgs e) {
            SetPointerCapture(e.Pointer);
        }

        private void OnPointerCaptureLost(object sender, PointerCaptureLostEventArgs e) {
            CancelTabMove(e.Pointer);
        }

        private void OnPointerMove(object sender, PointerEventArgs e) {
            // Cursor in screen coordinates
            var currentPoint = dragStartAggregatorWindow.PointToScreen(e.GetPosition(dragStartAggregatorWindow));
            if (isFirstMove) {
                // Move a bit before showing the ghost window
                var pointDiff = currentPoint - clickPoint;
                if (Math.Abs(pointDiff.X) <= Epsilon && Math.Abs(pointDiff.Y) <= Epsilon) {
                    SetPointerCapture(e.Pointer);
                    return;
                }

                ghostTab = GhostWindow.CreateGhostTab(caption, draggedTab.Bounds.Width, draggedTab.Bounds.Height, draggedTab.Foreground);
                ghostFacsimile = GhostWindow.CreateGhostFacsimile(dragStartAggregatorWindow.Width, dragStartAggregatorWindow.MinWidth);
                ghostWindow = GhostWindow.CreateGhostWindow();

                isFirstMove = false;
                isMoveInProgress = true;
                journeyId = Guid.NewGuid();
                SetPointerCapture(e.Pointer);

            } else {
                var adjustedOrigin = currentPoint - clickOffset;
                var aggregator = RuntimeImplementation.Instance.AggregatorWindows.FindVisibleAggregatorPresenterWithPointOnWindow(adjustedOrigin);

                if (aggregator != null && aggregator.IsPointOnTopBar(adjustedOrigin, Epsilon)) {
                    if (aggregator == dragStartAggregatorWindowPresenter
                        || dragAndDropPreviewTabAggregatorWindowPresenter != null) {

                        ShowTabDragAnimation(aggregator, adjustedOrigin);
                        return;
                    }
                }

                ShowWindowMiniature(adjustedOrigin);
            }
        }

        private void OnPointerReleased(object sender, PointerEventArgs e) {
            EndTabMove(e.Pointer);

            if (!isMoveInProgress) {
                return;
            }

            ShowDraggedTab();
            isMoveInProgress = false;

            var currentPoint = dragStartAggregatorWindow.PointToScreen(e.GetPosition(dragStartAggregatorWindow));
            var adjustedOrigin = currentPoint - clickOffset;
            dropTargetAggregatorWindowPresenter =
                RuntimeImplementation.Instance.AggregatorWindows.FindAggregatorWindowPresenterWithPointOnTopBar(adjustedOrigin, Epsilon);

            // moving tab horizontally on drag start window
            if (dropTargetAggregatorWindowPresenter == dragStartAggregatorWindowPresenter) {
                AdjustTabPositions(dropTargetAggregatorWindowPresenter, adjustedOrigin.X);

                // dropping tab outside any window
            } else if (dropTargetAggregatorWindowPresenter == null) {
                DetachTabToNewWindow(adjustedOrigin);

                // dropping tab into another window
            } else {
                // Do nothing because it was already moved during tab preview
            }

            dropTargetAggregatorWindowPresenter = null;
            draggedTab = null;
        }

        private void ShowTabDragAnimation(IAggregatorWindow aggregatorWindowPresenter, PixelPoint originPoint) {
            if (aggregatorWindowPresenter != dragStartAggregatorWindowPresenter && dragAndDropPreviewTabAggregatorWindowPresenter == null) {
                MoveDraggedTabToTargetAggregator(aggregatorWindowPresenter);
            }

            // we are dragging a tab and the ghost window becomes a ghost tab
            if (!Object.ReferenceEquals(ghostWindow.Content, ghostTab)) {
                ghostWindow.SystemDecorations = SystemDecorations.None;
                ghostWindow.Content = ghostTab;
                ghostWindow.InvalidateMeasure();
                ghostWindow.Show();

                HideDraggedTab();
            }

            var firstDraggableTab = aggregatorWindowPresenter.GetFirstDraggableTab();
            ghostWindow.Position = new PixelPoint(originPoint.X - AdjustForCurvedCorners, firstDraggableTab.GetTopLeftPoint().Y);
            AdjustTabPositions(aggregatorWindowPresenter, originPoint.X);
        }

        private void ShowWindowMiniature(PixelPoint originPoint) {
            // we are dragging out of service studio tabs and the ghost window becomes a screenshot of ss (facsimile)
            if (!ReferenceEquals(ghostWindow.Content, ghostFacsimile)) {
                ghostWindow.SystemDecorations = SystemDecorations.BorderOnly;
                ghostWindow.Content = ghostFacsimile;
                ghostWindow.InvalidateMeasure();
                ghostWindow.Show();

                // when we are dragging a tab the original tab is hidden so here we show it again
                ShowDraggedTab();
                MoveDraggedTabBackToDragStartAggregator();
            }

            ghostWindow.Show();

            ghostWindow.Position = originPoint;
        }

        private void HideDraggedTab() {
            if (GetActiveAggregatorWindowPresenter().HasDraggableTabs() && isMoveInProgress && draggedTab != null) {
                draggedTab.Opacity = 0;
            }
        }

        private void ShowDraggedTab() {
            if (GetActiveAggregatorWindowPresenter().HasDraggableTabs() && isMoveInProgress && draggedTab != null) {
                draggedTab.Opacity = 1;
            }
        }

        private IAggregatorWindow GetActiveAggregatorWindowPresenter() {
            return dragAndDropPreviewTabAggregatorWindowPresenter ?? dragStartAggregatorWindowPresenter;
        }

        private void MoveDraggedTabToTargetAggregator(IAggregatorWindow aggregatorWindowPresenter) {
            MoveTabBetweenWindows(dragStartAggregatorWindowPresenter, aggregatorWindowPresenter, aggregatorWindowPresenter.GetFirstDraggableTabIndex());
            dragAndDropPreviewTabAggregatorWindowPresenter = aggregatorWindowPresenter;
        }

        private void MoveDraggedTabBackToDragStartAggregator() {
            MoveTabBetweenWindows(dragAndDropPreviewTabAggregatorWindowPresenter, dragStartAggregatorWindowPresenter, draggedTabStartIndex);
            dragAndDropPreviewTabAggregatorWindowPresenter = null;
        }

        private void EndTabMove(IPointer pointer) {
            dragStartAggregatorWindow.PointerLeave -= OnPointerLeave;
            dragStartAggregatorWindow.PointerCaptureLost -= OnPointerCaptureLost;
            dragStartAggregatorWindow.PointerMoved -= OnPointerMove;
            dragStartAggregatorWindow.PointerReleased -= OnPointerReleased;

            DeleteGhostWindow();
            pointer?.Capture(null);
        }

        private void AdjustTabPositions(IAggregatorWindow aggregatorWindowPresenter, int xOriginAdjusted) {
            var originalIndex = aggregatorWindowPresenter.GetTabIndex(draggedTab);
            var newIndex = aggregatorWindowPresenter.CalculateTabIndexForPosition(draggedTab, xOriginAdjusted);
            SwapTabPositions(aggregatorWindowPresenter, newIndex, originalIndex);
        }

        private void SwapTabPositions(IAggregatorWindow aggregatorWindowPresenter, int moveToIndex, int originalIndex)
        {
            if (originalIndex < 0 || moveToIndex < 0 || originalIndex == moveToIndex)
            {
                return;
            }

            var aggregatorWindow = aggregatorWindowPresenter.GetAggregatorView();
            draggedTabTargetIndex = moveToIndex;

        }

        private void MoveTabBetweenWindows(
            IAggregatorWindow sourceAggregatorWindowPresenter,
            IAggregatorWindow targetAggregatorWindowPresenter,
            int newTabIndex) {

            if (sourceAggregatorWindowPresenter is null || targetAggregatorWindowPresenter is null) {
                return;
            }

            var sourceAggregatorPresenter = draggedTab.GetAggregatorPresenter();
            if (sourceAggregatorPresenter is null) {
                return;
            }

            var index = sourceAggregatorWindowPresenter.GetAggregatorView().GetTabIndex(sourceAggregatorPresenter.View);
            if (index < 0) {
                return;
            }
                
            sourceAggregatorWindowPresenter.GetAggregatorView().RemoveTab(sourceAggregatorPresenter.View);
            sourceAggregatorWindowPresenter.RemoveAggregator(sourceAggregatorPresenter, removeFromView: false);

            targetAggregatorWindowPresenter.AttachAggregator(sourceAggregatorPresenter);
            targetAggregatorWindowPresenter.Show(); // show window before changing login to avoid dialogs from appearing before main window shown
            targetAggregatorWindowPresenter.SelectedTopLevelPresenter = sourceAggregatorPresenter; // select tab after has been created

            draggedTab = targetAggregatorWindowPresenter.GetAggregatorView().TabItems.ToList().LastOrDefault();

            SwapTabPositions(targetAggregatorWindowPresenter, newTabIndex, targetAggregatorWindowPresenter.GetAggregatorView().TabItems.Count() - 1);
        }

        private void DetachTabToNewWindow(PixelPoint dropPoint) {
            var dragStartAggregatorPresenter = draggedTab.GetAggregatorPresenter();
            if (dragStartAggregatorPresenter is null) {
                return;
            }
            
            var newAggregatorWindow = RuntimeImplementation.Instance.CreateAggregatorWindow();


            var index = dragStartAggregatorWindowPresenter.GetAggregatorView().GetTabIndex(dragStartAggregatorPresenter.View);
            if (index < 0) {
                // Tab was moved to another aggregator
                MoveDraggedTabBackToDragStartAggregator();
            }

            dragStartAggregatorWindow.RemoveTab(dragStartAggregatorPresenter.View);
            dragStartAggregatorWindowPresenter.RemoveAggregator(dragStartAggregatorPresenter, removeFromView: false);
            newAggregatorWindow.AttachAggregator(dragStartAggregatorPresenter);
            draggedTabTargetIndex = newAggregatorWindow.GetFirstDraggableTabIndex();

            newAggregatorWindow.Show(); // show window before changing login to avoid dialogs from appearing before main window shown
            newAggregatorWindow.SelectedTopLevelPresenter = dragStartAggregatorPresenter; // select tab after has been created, otherwise won't be selected
        }
        
        private void SetPointerCapture(IPointer pointer) {
            currentPointer = pointer;
            currentPointer.Capture(dragStartAggregatorWindow);
        }

        private void DeleteGhostWindow() {
            if (ghostWindow is not null) {
                ghostWindow.Close();
                ghostWindow = null;
            }
        }

        private void CancelTabMove(IPointer pointer) {
            EndTabMove(pointer);
            draggedTab = null;
        }
    }
}
