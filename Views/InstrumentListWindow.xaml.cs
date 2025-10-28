using System.Windows;
using System.Windows.Input;
using FISApiClient.Models;
using FISApiClient.Services;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class InstrumentListWindow : Window
    {
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;
        private readonly NavigationService _navigationService;

        public InstrumentListWindow(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            InitializeComponent();
            _mdsService = mdsService;
            _sleService = sleService;
            _navigationService = new NavigationService();
            DataContext = new InstrumentListViewModel(mdsService, sleService);
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is InstrumentListViewModel viewModel && 
                viewModel.SelectedInstrument != null)
            {
                _navigationService.ShowInstrumentDetailsWindow(viewModel.SelectedInstrument, _mdsService, _sleService);
            }
        }

        private void BtnOrderBook_Click(object sender, RoutedEventArgs e)
        {
            if (_sleService == null)
            {
                MessageBox.Show(
                    "Serwis SLE nie jest dostępny.",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            _navigationService.ShowOrderBookWindow(_sleService);

            System.Diagnostics.Debug.WriteLine("[InstrumentListWindow] Order Book window opened");
        }
    }
}