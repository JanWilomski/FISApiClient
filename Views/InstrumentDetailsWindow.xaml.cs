using System.Windows;
using FISApiClient.Models;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class InstrumentDetailsWindow : Window
    {
        private InstrumentDetailsViewModel? _viewModel;

        public InstrumentDetailsWindow(Instrument instrument, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            InitializeComponent();
            
            _viewModel = new InstrumentDetailsViewModel(instrument, mdsService, sleService);
            _viewModel.RequestClose += OnRequestClose;
            DataContext = _viewModel;
        }

        private void OnRequestClose()
        {
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            
            // Cleanup
            if (_viewModel != null)
            {
                _viewModel.RequestClose -= OnRequestClose;
                _viewModel.Cleanup();
            }
        }
    }
}
