using System.Windows;
using System.Windows.Controls;
using FISApiClient.ViewModels;

namespace FISApiClient
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Ustaw domyślne hasła
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

        private void BtnInstruments_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                var mdsService = viewModel.GetMdsService();
                var sleService = viewModel.GetSleService(); // Pobierz również SleConnectionService
                var instrumentWindow = new Views.InstrumentListWindow(mdsService, sleService);
                instrumentWindow.Show();
            }
        }
    }
}
