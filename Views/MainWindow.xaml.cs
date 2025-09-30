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
            
            // Ustaw domyślne hasło
            if (DataContext is ConnectionViewModel viewModel)
            {
                PasswordBox.Password = viewModel.Password;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionViewModel viewModel)
            {
                viewModel.Password = ((PasswordBox)sender).Password;
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
