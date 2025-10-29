using System.Linq;
using System.Windows;
using FISApiClient.Models;
using FISApiClient.ViewModels;
using FISApiClient.Views;

namespace FISApiClient.Services
{
    public class NavigationService
    {
        public void ShowGeneralMenuWindow(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            var generalMenuWindow = new GeneralMenuWindow(mdsService, sleService);
            generalMenuWindow.Show();
        }

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

        public void ShowMarketWatchWindow(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            var existingWindow = Application.Current.Windows.OfType<MarketWatchWindow>().FirstOrDefault();
            if (existingWindow != null)
            {
                existingWindow.Activate();
            }
            else
            {
                var marketWatchWindow = new MarketWatchWindow(new MarketWatchViewModel(mdsService), mdsService, sleService);
                marketWatchWindow.Show();
            }
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

        public void ShowAlgoMonitorWindow()
        {
            // This window can be opened once and reused
            var existingWindow = Application.Current.Windows.OfType<AlgoMonitorWindow>().FirstOrDefault();
            if (existingWindow != null)
            {
                existingWindow.Activate();
            }
            else
            {
                var monitorWindow = new AlgoMonitorWindow();
                monitorWindow.Show();
            }
        }
    }
}