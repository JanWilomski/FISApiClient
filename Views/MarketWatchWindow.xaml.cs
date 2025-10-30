using System.Windows;
using System.Windows.Input;
using FISApiClient.Models;
using FISApiClient.Services;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class MarketWatchWindow : Window
    {
        private readonly MarketWatchViewModel _viewModel;
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;
        private readonly NavigationService _navigationService;

        public MarketWatchWindow(MarketWatchViewModel viewModel, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _mdsService = mdsService;
            _sleService = sleService;
            _navigationService = new NavigationService();
            DataContext = _viewModel;
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedInstrument != null)
            {
                // Konwertuj MarketWatchInstrument na Instrument
                var selectedInstrument = _viewModel.SelectedInstrument;
                var instrument = new Instrument
                {
                    Glid = selectedInstrument.Glid,
                    Symbol = selectedInstrument.Symbol,
                    Name = selectedInstrument.Name,
                    ISIN = selectedInstrument.ISIN,
                    LocalCode = selectedInstrument.LocalCode
                };

                _navigationService.ShowInstrumentDetailsWindow(instrument, _mdsService, _sleService);
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Zatrzymaj wszystkie subskrypcje przed zamknięciem
            await _viewModel.CleanupAsync();
        }
    }
}