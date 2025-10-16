using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class MarketWatchViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;

        #region Properties

        private ObservableCollection<MarketWatchInstrument> _watchedInstruments = new ObservableCollection<MarketWatchInstrument>();
        public ObservableCollection<MarketWatchInstrument> WatchedInstruments
        {
            get => _watchedInstruments;
            set => SetProperty(ref _watchedInstruments, value);
        }

        private MarketWatchInstrument? _selectedInstrument;
        public MarketWatchInstrument? SelectedInstrument
        {
            get => _selectedInstrument;
            set
            {
                if (SetProperty(ref _selectedInstrument, value))
                {
                    RemoveInstrumentCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "Panel Market Watch - dodaj instrumenty z listy instrumentów";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _instrumentCount;
        public int InstrumentCount
        {
            get => _instrumentCount;
            set => SetProperty(ref _instrumentCount, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                }
            }
        }

        public string ConnectionStatusText => IsConnected ? "Połączono ✓" : "Rozłączono ✗";
        public string ConnectionStatusColor => IsConnected ? "#4CAF50" : "#F44336";

        #endregion

        #region Commands

        public RelayCommand RemoveInstrumentCommand { get; }
        public RelayCommand ClearAllCommand { get; }
        public RelayCommand RefreshAllCommand { get; }

        #endregion

        public MarketWatchViewModel(MdsConnectionService mdsService)
        {
            _mdsService = mdsService;

            RemoveInstrumentCommand = new RelayCommand(
                _ => RemoveSelectedInstrument(),
                _ => SelectedInstrument != null
            );

            ClearAllCommand = new RelayCommand(
                _ => ClearAll(),
                _ => WatchedInstruments.Any()
            );

            RefreshAllCommand = new RelayCommand(
                async _ => await RefreshAllInstrumentsAsync(),
                _ => IsConnected && WatchedInstruments.Any()
            );

            // Subskrypcja na aktualizacje z MDS
            _mdsService.InstrumentDetailsReceived += OnInstrumentDetailsReceived;

            // Monitorowanie połączenia
            IsConnected = _mdsService.IsConnected;
            System.Threading.Tasks.Task.Run(async () =>
            {
                while (true)
                {
                    await System.Threading.Tasks.Task.Delay(1000);
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        IsConnected = _mdsService.IsConnected;
                    });
                }
            });
        }

        /// <summary>
        /// Dodaje nowy instrument do MarketWatch i rozpoczyna subskrypcję real-time
        /// </summary>
        public async System.Threading.Tasks.Task AddInstrumentAsync(Instrument instrument)
        {
            if (!_mdsService.IsConnected)
            {
                MessageBox.Show(
                    "Brak połączenia z serwerem MDS!",
                    "Błąd połączenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            string glidAndSymbol = instrument.Glid + instrument.Symbol;

            // Sprawdź czy instrument już jest na liście
            if (WatchedInstruments.Any(i => i.GlidAndSymbol == glidAndSymbol))
            {
                MessageBox.Show(
                    $"Instrument {instrument.Symbol} jest już na liście Market Watch!",
                    "Instrument już dodany",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            // Utwórz nowy wpis
            var watchInstrument = new MarketWatchInstrument
            {
                GlidAndSymbol = glidAndSymbol,
                Glid = instrument.Glid,
                Symbol = instrument.Symbol,
                Name = instrument.Name,
                ISIN = instrument.ISIN
            };

            // Dodaj do listy
            Application.Current.Dispatcher.Invoke(() =>
            {
                WatchedInstruments.Add(watchInstrument);
                InstrumentCount = WatchedInstruments.Count;
                StatusMessage = $"Dodano {instrument.Symbol} - oczekiwanie na dane...";
            });

            // Subskrybuj real-time updates (request 1001)
            try
            {
                await _mdsService.RequestInstrumentDetails(glidAndSymbol);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Subskrypcja aktywna dla {instrument.Symbol}";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Błąd subskrypcji: {ex.Message}";
                });
            }
        }

        /// <summary>
        /// Usuwa wybrany instrument z MarketWatch i zatrzymuje subskrypcję
        /// </summary>
        private async void RemoveSelectedInstrument()
        {
            if (SelectedInstrument == null) return;

            var instrumentToRemove = SelectedInstrument;
            string symbol = instrumentToRemove.Symbol;

            // Zatrzymaj subskrypcję (request 1002)
            try
            {
                await _mdsService.StopInstrumentDetailsAsync(instrumentToRemove.GlidAndSymbol);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd zatrzymywania subskrypcji: {ex.Message}";
            }

            // Usuń z listy
            WatchedInstruments.Remove(instrumentToRemove);
            InstrumentCount = WatchedInstruments.Count;
            StatusMessage = $"Usunięto {symbol} z Market Watch";

            // Aktualizuj komendy
            ClearAllCommand.RaiseCanExecuteChanged();
            RefreshAllCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Czyści całą listę i zatrzymuje wszystkie subskrypcje
        /// </summary>
        private async void ClearAll()
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz usunąć wszystkie instrumenty z Market Watch?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes) return;

            // Zatrzymaj wszystkie subskrypcje
            foreach (var instrument in WatchedInstruments.ToList())
            {
                try
                {
                    await _mdsService.StopInstrumentDetailsAsync(instrument.GlidAndSymbol);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd zatrzymywania subskrypcji dla {instrument.Symbol}: {ex.Message}");
                }
            }

            // Wyczyść listę
            WatchedInstruments.Clear();
            InstrumentCount = 0;
            StatusMessage = "Wyczyszczono Market Watch";

            // Aktualizuj komendy
            ClearAllCommand.RaiseCanExecuteChanged();
            RefreshAllCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Odświeża wszystkie instrumenty (ponowne żądanie snapshot)
        /// </summary>
        private async System.Threading.Tasks.Task RefreshAllInstrumentsAsync()
        {
            if (!_mdsService.IsConnected || !WatchedInstruments.Any()) return;

            StatusMessage = "Odświeżanie wszystkich instrumentów...";

            foreach (var instrument in WatchedInstruments)
            {
                try
                {
                    await _mdsService.RequestInstrumentDetails(instrument.GlidAndSymbol);
                    await System.Threading.Tasks.Task.Delay(50); // Opóźnienie między requestami
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd odświeżania {instrument.Symbol}: {ex.Message}");
                }
            }

            StatusMessage = "Odświeżanie zakończone";
        }

        /// <summary>
        /// Event handler dla otrzymywanych aktualizacji instrumentów
        /// </summary>
        private void OnInstrumentDetailsReceived(InstrumentDetails details)
        {
            if (string.IsNullOrEmpty(details.GlidAndSymbol)) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Znajdź instrument na liście
                var watchInstrument = WatchedInstruments.FirstOrDefault(i => i.GlidAndSymbol == details.GlidAndSymbol);

                if (watchInstrument != null)
                {
                    // Aktualizuj dane
                    watchInstrument.UpdateFromDetails(details);

                    // Aktualizuj status
                    StatusMessage = $"Zaktualizowano {watchInstrument.Symbol} o {watchInstrument.LastUpdateTimeFormatted}";
                }
            });
        }

        /// <summary>
        /// Cleanup przy zamykaniu okna
        /// </summary>
        public async System.Threading.Tasks.Task CleanupAsync()
        {
            // Zatrzymaj wszystkie subskrypcje
            foreach (var instrument in WatchedInstruments.ToList())
            {
                try
                {
                    await _mdsService.StopInstrumentDetailsAsync(instrument.GlidAndSymbol);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd cleanup dla {instrument.Symbol}: {ex.Message}");
                }
            }

            // Odsubskrybuj event
            _mdsService.InstrumentDetailsReceived -= OnInstrumentDetailsReceived;
        }
    }
}