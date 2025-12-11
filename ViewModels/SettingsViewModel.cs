using System.Threading.Tasks;
using FISApiClient.Helpers;
using FISApiClient.Services;

namespace FISApiClient.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    ApplyAndSaveTheme();
                }
            }
        }

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            _ = LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            var isDark = await _settingsService.LoadIsDarkModeAsync();
            // Set the field directly to avoid triggering the setter logic on an initial load
            _isDarkMode = isDark;
            OnPropertyChanged(nameof(IsDarkMode));
            // Apply the initial theme without saving it again
            ThemeService.ApplyTheme(_isDarkMode ? ThemeService.Theme.Dark : ThemeService.Theme.Light);
        }

        private void ApplyAndSaveTheme()
        {
            ThemeService.ApplyTheme(IsDarkMode ? ThemeService.Theme.Dark : ThemeService.Theme.Light);
            _ = _settingsService.SaveIsDarkModeAsync(IsDarkMode);
        }
    }
}
