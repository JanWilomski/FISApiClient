using System.Windows.Controls;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class ConnectionView : UserControl
    {
        public ConnectionView(ConnectionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
