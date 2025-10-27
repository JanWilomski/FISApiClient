using System.Windows;
using FISApiClient.Models;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class ModifyOrderWindow : Window
    {
        private readonly ModifyOrderViewModel _viewModel;

        public ModifyOrderWindow(Order order, SleConnectionService sleService)
        {
            InitializeComponent();
            
            _viewModel = new ModifyOrderViewModel(order, sleService);
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
            
            if (_viewModel != null)
            {
                _viewModel.RequestClose -= OnRequestClose;
            }
        }
    }
}