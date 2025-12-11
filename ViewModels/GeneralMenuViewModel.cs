using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;
using FISApiClient.Services;

namespace FISApiClient.ViewModels
{
    public class GeneralMenuViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;
        private readonly NavigationService _navigationService;

        public RelayCommand OpenInstrumentListCommand { get; }
        public RelayCommand OpenMarketWatchCommand { get; }
        public RelayCommand OpenOrderBookCommand { get; }
        public RelayCommand OpenAlgoMonitorCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand CloseApplicationCommand { get; }

        public GeneralMenuViewModel(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            _mdsService = mdsService;
            _sleService = sleService;
            _navigationService = new NavigationService();

            OpenInstrumentListCommand = new RelayCommand(
                _ => _navigationService.ShowInstrumentListWindow(_mdsService, _sleService),
                _ => _mdsService.IsConnected
            );

            OpenMarketWatchCommand = new RelayCommand(
                _ => _navigationService.ShowMarketWatchWindow(_mdsService, _sleService),
                _ => _mdsService.IsConnected
            );

            OpenOrderBookCommand = new RelayCommand(
                _ => _navigationService.ShowOrderBookWindow(_sleService),
                _ => _sleService.IsConnected
            );

            OpenAlgoMonitorCommand = new RelayCommand(
                _ => _navigationService.ShowAlgoMonitorWindow(),
                _ => true // Algo Monitor can be opened regardless of connection status
            );
            
            OpenSettingsCommand = new RelayCommand(
                _ => _navigationService.ShowSettingsWindow(),
                _ => true
            );

            CloseApplicationCommand = new RelayCommand(
                _ => Application.Current.Shutdown(),
                _ => true
            );
        }
    }
}