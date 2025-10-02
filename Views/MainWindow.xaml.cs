using System.Windows;
using System.Windows.Controls;
using Cross_FIS_API_1._2.ViewModels;

namespace Cross_FIS_API_1._2
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
                var instrumentWindow = new Views.InstrumentListWindow(mdsService);
                instrumentWindow.Show();
            }
        }
    }
}
