using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ServiceStudio.WebViewImplementation {
    public class GhostWindow {
        private const int FacsimileShrinkageFactor = 3;
        private const string ImagesNamespace = "Xilium.CefGlue.Demo.Avalonia.AggregatorWindow.Images.";
        private const string GhostFacsimileLightImageUri = "resm:" + ImagesNamespace + "GhostTabWindow-Light@2x.png";
        private const string GhostFacsimileDarkImageUri = "resm:" + ImagesNamespace + "GhostTabWindow-Dark@2x.png";
        private const string DarkThemeTabBackgroundColor = "#202327";
        private const string LightThemeTabBackgroundColor = "#F7F8FA";
        private const string DarkThemeTabBorderColor = "#ff3b3d41";
        private const string LightThemeTabBorderColor = "#ffe0e2e4";

        public static Window CreateGhostWindow() {
            return new Window() {
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

        public static Control CreateGhostTab(string caption, double width, double height, IBrush foregroundBrush) {
            var textBlock = new TextBlock() {
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

            var canvas = new Canvas() {
                Width = 20,
                Height = 10,
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            // closing cross
            canvas.Children.Add(new Line() {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(8, 8),
                Stroke = foregroundBrush,
                StrokeThickness = 1
            });
            canvas.Children.Add(new Line() {
                StartPoint = new Point(8, 0),
                EndPoint = new Point(0, 8),
                Stroke = foregroundBrush,
                StrokeThickness = 1
            });

            var dockPanel = new DockPanel() {
                Height = height - 1, // top border pixel
                Background = Brush.Parse(LightThemeTabBackgroundColor),
                VerticalAlignment = VerticalAlignment.Top,
            };
            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(canvas);

            var borderLineBrushColor = LightThemeTabBorderColor;
            var border = new Border {
                Height = height,
                Width = width,
                Background = Brushes.Transparent,
                BorderBrush = Brush.Parse(borderLineBrushColor),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(1, 1, 1, 0),
                Child = dockPanel
            };

            var osSpecificPanelTopMargin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? -4 : 0;

            var panel = new Panel() {
                Width = width,
                Margin = new Thickness(0, osSpecificPanelTopMargin, 0, 0),
                Background = Brushes.Transparent,
            };
            panel.Children.Add(border);

            return panel;
        }

        public static Control CreateGhostFacsimile(double aggregatorWidth, double aggregatorMinWidth) {
            var width = GetSizeForFacsimile(aggregatorWidth, aggregatorMinWidth, FacsimileShrinkageFactor);
            // load bitmap image
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            var ghostWindowThemeUri = GhostFacsimileLightImageUri;

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

        private static int GetSizeForFacsimile(double currentSize, double minSize, int facsimileShrinkageFactor) =>
            (double.IsNaN(currentSize) || currentSize < minSize) ? (int) (minSize/facsimileShrinkageFactor) : (int) (currentSize/facsimileShrinkageFactor);
    }
}
