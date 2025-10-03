using System.Windows;
using Cross_FIS_API_1._2.Models;
using Cross_FIS_API_1._2.ViewModels;

namespace Cross_FIS_API_1._2.Views
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
