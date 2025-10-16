using System.Windows;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class MarketWatchWindow : Window
    {
        private readonly MarketWatchViewModel _viewModel;

        public MarketWatchWindow(MarketWatchViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Zatrzymaj wszystkie subskrypcje przed zamknięciem
            await _viewModel.CleanupAsync();
        }
    }
}