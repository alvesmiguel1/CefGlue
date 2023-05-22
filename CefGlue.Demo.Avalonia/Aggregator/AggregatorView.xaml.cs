using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace ServiceStudio.WebViewImplementation {
    internal partial class AggregatorView : UserControl {

        public AggregatorView() { }
        public AggregatorView(TabHeaderInfo tabHeaderInfo) {
            AvaloniaXamlLoader.Load(this);
            var btn = this.FindControl<Button>("btn");
            btn.Click += BtnOnClick;

            var btnModal = this.FindControl<Button>("btn-modal");
            btnModal.Click += BtnModalOnClick;
            TabHeader = tabHeaderInfo;
        }

        private void BtnModalOnClick(object sender, RoutedEventArgs e)
        {
            var w = new Window { Width = 200, Height = 200 };
            w.ShowDialog(this.GetVisualRoot() as Window);
        }

        private void BtnOnClick(object sender, RoutedEventArgs e)
        {
            var w = new Window { Width = 200, Height = 200 };
            w.Show(this.GetVisualRoot() as Window);
        }

        public TabHeaderInfo TabHeader { get; }
    }
}
