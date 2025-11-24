using System;
using Microsoft.Extensions.Configuration;

namespace FISApiClient.Helpers
{
    public static class ConfigProvider
    {
        public static ConnectionSettings GetMdsSettings()
        {
            return App.Configuration.GetSection("MdsSettings").Get<ConnectionSettings>() ?? throw new InvalidOperationException("MdsSettings not found in appsettings.json");
        }

        public static ConnectionSettings GetSleSettings()
        {
            return App.Configuration.GetSection("SleSettings").Get<ConnectionSettings>() ?? throw new InvalidOperationException("SleSettings not found in appsettings.json");
        }
    }

    public class ConnectionSettings
    {
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;
        public string Subnode { get; set; } = string.Empty;
    }
}