using System.Windows;
using FISApiClient.Models;
using FISApiClient.ViewModels;

namespace FISApiClient.Views
{
    public partial class GeneralMenuWindow : Window
    {
        public GeneralMenuWindow(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            InitializeComponent();
            DataContext = new GeneralMenuViewModel(mdsService, sleService);
        }
    }
}