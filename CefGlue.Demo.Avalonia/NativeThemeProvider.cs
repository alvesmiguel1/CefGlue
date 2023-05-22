using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using ServiceStudio.WebViewImplementation.Framework;
using ServiceStudio.WebViewImplementation.Themes;

namespace ServiceStudio.WebViewImplementation {
    internal static class NativeThemeProvider {
        public enum ThemeName {
            Light,
            Dark
        }

        public static ThemeName Theme => ThemeName.Light;

        public static void Initialize() {
            LoadTheme();
        }

        private static void LoadTheme() {
            Application.Current.Styles.Add(CreateTheme(Theme));
        }

        private static ITheme CreateTheme(ThemeName theme) {
            return theme switch {
                ThemeName.Dark  => new DarkTheme(),
                ThemeName.Light => new LightTheme(),
                _               => new LightTheme()
            };
        }
    }
}
