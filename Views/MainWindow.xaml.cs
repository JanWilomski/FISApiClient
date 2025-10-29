using System.Windows;
using System.Windows.Controls;
using FISApiClient.Services;
using FISApiClient.ViewModels;

namespace FISApiClient
{
    public partial class MainWindow : Window
    {
        private readonly NavigationService _navigationService;

        public MainWindow()
        {
            InitializeComponent();
            
            _navigationService = new NavigationService();

            if (DataContext is ConnectionViewModel viewModel)
            {
                MdsPasswordBox.Password = viewModel.MdsPassword;
                SlePasswordBox.Password = viewModel.SlePassword;
            } 
        }

        private void MdsPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                viewModel.MdsPassword = ((PasswordBox)sender).Password;
            }
        }

        private void SlePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                viewModel.SlePassword = ((PasswordBox)sender).Password;
            }
        }

        private void BtnGeneralMenu_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                var mdsService = viewModel.GetMdsService();
                var sleService = viewModel.GetSleService();
                _navigationService.ShowGeneralMenuWindow(mdsService, sleService);
            }
        }
    }
}
