using FISApiClient.Models;

namespace FISApiClient.Helpers
{
    public static class ConfigProvider
    {
        // WARNING: These are hardcoded for now.
        // TODO: Move these to a secure configuration file (e.g., appsettings.json)
        public static ConnectionSettings GetMdsSettings()
        {
            return new ConnectionSettings
            {
                IpAddress = "192.168.45.25",
                Port = "25503",
                User = "103",
                Password = "glglgl",
                Node = "5500",
                Subnode = "4500"
            };
        }

        public static ConnectionSettings GetSleSettings()
        {
            return new ConnectionSettings
            {
                IpAddress = "172.31.136.4",
                Port = "19593",
                User = "151",
                Password = "glglgl",
                Node = "24300",
                Subnode = "14300"
            };
        }
    }

    public class ConnectionSettings
    {
        public string IpAddress { get; set; }
        public string Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Node { get; set; }
        public string Subnode { get; set; }
    }
}