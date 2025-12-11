using System;
using System.Linq;
using System.Windows;

namespace FISApiClient.Services
{
    public static class ThemeService
    {
        public enum Theme
        {
            Light,
            Dark
        }

        public static void ApplyTheme(Theme theme)
        {
            // First, remove any existing theme dictionaries to avoid conflicts
            var existingTheme = Application.Current.Resources.MergedDictionaries.FirstOrDefault(
                d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
            
            if (existingTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
            }

            // Determine the URI for the new theme
            string themeUri = theme switch
            {
                Theme.Dark => "Themes/DarkTheme.xaml",
                _ => "Themes/LightTheme.xaml",
            };

            // Add the new theme dictionary
            var newTheme = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(newTheme);
        }
    }
}
