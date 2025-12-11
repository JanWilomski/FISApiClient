using System.IO;
using System.Windows;
using FISApiClient.Services;
using Microsoft.Extensions.Configuration;

namespace FISApiClient
{
    public partial class App : Application
    {
        public static IConfiguration? Configuration { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            // Load and apply theme on startup
            LoadAndApplyTheme();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private async void LoadAndApplyTheme()
        {
            var settingsService = new SettingsService();
            var isDark = await settingsService.LoadIsDarkModeAsync();
            ThemeService.ApplyTheme(isDark ? ThemeService.Theme.Dark : ThemeService.Theme.Light);
        }
    }
}
