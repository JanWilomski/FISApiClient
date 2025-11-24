using System.IO;
using System.Windows;
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

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
