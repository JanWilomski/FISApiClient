using System.Windows;
using Cross_FIS_API_1._2.Models;
using Cross_FIS_API_1._2.ViewModels;

namespace Cross_FIS_API_1._2.Views
{
    public partial class InstrumentListWindow : Window
    {
        public InstrumentListWindow(MdsConnectionService mdsService)
        {
            InitializeComponent();
            DataContext = new InstrumentListViewModel(mdsService);
        }
    }
}
