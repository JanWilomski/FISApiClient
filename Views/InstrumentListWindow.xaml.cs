using System.Windows;
using System.Windows.Input;
using Cross_FIS_API_1._2.Models;
using Cross_FIS_API_1._2.ViewModels;

namespace Cross_FIS_API_1._2.Views
{
    /// <summary>
    /// Okno wyświetlające listę instrumentów finansowych
    /// </summary>
    public partial class InstrumentListWindow : Window
    {
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;

        public InstrumentListWindow(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            InitializeComponent();
            _mdsService = mdsService;
            _sleService = sleService;
            DataContext = new InstrumentListViewModel(mdsService);
        }

        /// <summary>
        /// Obsługa podwójnego kliknięcia wiersza w DataGrid
        /// Otwiera okno ze szczegółami wybranego instrumentu
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Sprawdź, czy kliknięto wiersz (a nie w nagłówek lub pustą przestrzeń)
            if (DataContext is InstrumentListViewModel viewModel && 
                viewModel.SelectedInstrument != null)
            {
                OpenInstrumentDetails(viewModel.SelectedInstrument);
            }
        }

        /// <summary>
        /// Otwiera nowe okno ze szczegółowymi informacjami o instrumencie
        /// </summary>
        /// <param name="instrument">Instrument do wyświetlenia</param>
        private void OpenInstrumentDetails(Instrument instrument)
        {
            // Utwórz nowe okno ze szczegółami instrumentu, przekaż oba serwisy
            var detailsWindow = new InstrumentDetailsWindow(instrument, _mdsService, _sleService);
            detailsWindow.Owner = this; // Ustaw to okno jako właściciela
            detailsWindow.Show();
        }
    }
}
