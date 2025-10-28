using System.Windows;
using FISApiClient.Models;
using FISApiClient.Services;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    /// <summary>
    /// Okno wyświetlające Order Book (książkę zleceń)
    /// </summary>
    public partial class OrderBookWindow : Window
    {
        private OrderBookViewModel? _viewModel;

        private InstrumentCacheService? _cacheService;

        public OrderBookWindow(SleConnectionService sleService)
        {
            InitializeComponent();
    
            _viewModel = new OrderBookViewModel(sleService);
            DataContext = _viewModel;

            System.Diagnostics.Debug.WriteLine("[OrderBookWindow] Window initialized");
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            
            // Cleanup
            if (_viewModel != null)
            {
                _viewModel.Cleanup();
            }

            System.Diagnostics.Debug.WriteLine("[OrderBookWindow] Window closed and cleaned up");
        }
    }
}