using System.Windows;
using FISApiClient.Models;
using FISApiClient.Views;

namespace FISApiClient.Services
{
    public class NavigationService
    {
        public void ShowInstrumentListWindow(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            var instrumentWindow = new InstrumentListWindow(mdsService, sleService);
            instrumentWindow.Show();
        }

        public void ShowInstrumentDetailsWindow(Instrument instrument, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            var detailsWindow = new InstrumentDetailsWindow(instrument, mdsService, sleService);
            detailsWindow.Show();
        }

        public void ShowOrderBookWindow(SleConnectionService sleService)
        {
            if (!sleService.IsConnected)
            {
                MessageBox.Show(
                    "Najpierw połącz się z serwerem SLE w głównym oknie.",
                    "Brak połączenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var orderBookWindow = new OrderBookWindow(sleService);
            orderBookWindow.Show();
        }
    }
}