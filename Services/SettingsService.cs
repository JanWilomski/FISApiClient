using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FISApiClient.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private const string IsDarkModeKey = "IsDarkMode";

        public SettingsService(string settingsFile = "usersettings.json")
        {
            _settingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), settingsFile);
        }

        public async Task<bool> LoadIsDarkModeAsync()
        {
            if (!File.Exists(_settingsFilePath)) return false;

            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
                return settings != null && settings.TryGetValue(IsDarkModeKey, out var isDark) && isDark;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                return false;
            }
        }

        public async Task SaveIsDarkModeAsync(bool isDarkMode)
        {
            try
            {
                var settings = new Dictionary<string, bool>
                {
                    [IsDarkModeKey] = isDarkMode
                };
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                await File.WriteAllTextAsync(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
