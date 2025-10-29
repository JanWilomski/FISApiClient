using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FISApiClient.Models;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class AlgoStrategyWindow : Window
    {
        private AlgoStrategyViewModel? _viewModel;

        public AlgoStrategyWindow(Instrument instrument, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            InitializeComponent();
            
            _viewModel = new AlgoStrategyViewModel(instrument, mdsService, sleService);
            _viewModel.RequestClose += OnRequestClose;
            DataContext = _viewModel;
        }

        private void Strategy_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is AlgoStrategyInfo strategy)
            {
                _viewModel?.SelectStrategy(strategy);
            }
        }

        private void OnRequestClose()
        {
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            
            if (_viewModel != null)
            {
                _viewModel.RequestClose -= OnRequestClose;
            }
        }
    }
}