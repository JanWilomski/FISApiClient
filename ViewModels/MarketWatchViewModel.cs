using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class MarketWatchViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        
        // Batch processing
        private readonly ConcurrentQueue<InstrumentDetails> _updateQueue = new();
        private readonly Dictionary<string, MarketWatchInstrument> _instrumentDictionary = new();
        private readonly DispatcherTimer _batchTimer;
        private readonly SemaphoreSlim _batchLock = new(1, 1);
        
        // Performance metrics
        private int _updatesReceived;
        private int _updatesProcessed;
        private DateTime _lastStatsUpdate = DateTime.Now;
        private long _totalUpdateTime;
        private int _batchCount;

        #region Properties

        private ObservableCollection<MarketWatchInstrument> _watchedInstruments = new();
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
        
        // Performance stats
        private string _performanceStats = "";
        public string PerformanceStats
        {
            get => _performanceStats;
            set => SetProperty(ref _performanceStats, value);
        }

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

            // Subskrypcja na aktualizacje z MDS - teraz dodaje do kolejki
            _mdsService.InstrumentDetailsReceived += OnInstrumentDetailsReceived;

            // Batch processing timer - przetwarzaj co 50ms
            _batchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 razy na sekundę
            };
            _batchTimer.Tick += async (s, e) => await ProcessBatchUpdatesAsync();
            _batchTimer.Start();

            // Monitorowanie połączenia
            IsConnected = _mdsService.IsConnected;
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        IsConnected = _mdsService.IsConnected;
                        UpdatePerformanceStats();
                    });
                }
            });
        }

        /// <summary>
        /// Dodaje nowy instrument do MarketWatch i rozpoczyna subskrypcję real-time
        /// </summary>
        public async Task AddInstrumentAsync(Instrument instrument)
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
            if (_instrumentDictionary.ContainsKey(glidAndSymbol))
            {
                MessageBox.Show(
                    $"Instrument {instrument.Symbol} jest już na liście Market Watch!",
                    "Duplikat",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            // Utwórz nowy instrument dla MarketWatch
            var watchInstrument = new MarketWatchInstrument
            {
                GlidAndSymbol = glidAndSymbol,
                Glid = instrument.Glid,
                Symbol = instrument.Symbol,
                Name = instrument.Name,
                ISIN = instrument.ISIN,
                LocalCode = instrument.LocalCode
            };

            // Dodaj do kolekcji i dictionary
            WatchedInstruments.Add(watchInstrument);
            _instrumentDictionary[glidAndSymbol] = watchInstrument;
            InstrumentCount = WatchedInstruments.Count;

            // Rozpocznij subskrypcję real-time
            try
            {
                await _mdsService.RequestInstrumentDetails(glidAndSymbol);
                StatusMessage = $"Dodano {instrument.Symbol} do Market Watch";
                
                // Aktualizuj komendy
                ClearAllCommand.RaiseCanExecuteChanged();
                RefreshAllCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd dodawania instrumentu: {ex.Message}");
                
                // Cofnij dodanie w przypadku błędu
                WatchedInstruments.Remove(watchInstrument);
                _instrumentDictionary.Remove(glidAndSymbol);
                InstrumentCount = WatchedInstruments.Count;
                
                MessageBox.Show(
                    $"Błąd subskrypcji instrumentu: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void RemoveSelectedInstrument()
        {
            if (SelectedInstrument == null) return;

            var instrument = SelectedInstrument;
            
            var result = MessageBox.Show(
                $"Czy na pewno usunąć {instrument.Symbol} z Market Watch?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // Zatrzymaj subskrypcję
                await _mdsService.StopInstrumentDetailsAsync(instrument.GlidAndSymbol);
                
                // Usuń z kolekcji i dictionary
                WatchedInstruments.Remove(instrument);
                _instrumentDictionary.Remove(instrument.GlidAndSymbol);
                InstrumentCount = WatchedInstruments.Count;
                
                StatusMessage = $"Usunięto {instrument.Symbol} z Market Watch";
                
                // Aktualizuj komendy
                ClearAllCommand.RaiseCanExecuteChanged();
                RefreshAllCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd usuwania instrumentu: {ex.Message}");
                MessageBox.Show(
                    $"Błąd zatrzymywania subskrypcji: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private async void ClearAll()
        {
            if (!WatchedInstruments.Any()) return;

            var result = MessageBox.Show(
                $"Czy na pewno usunąć wszystkie {WatchedInstruments.Count} instrumenty z Market Watch?",
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

            // Wyczyść kolekcje
            WatchedInstruments.Clear();
            _instrumentDictionary.Clear();
            InstrumentCount = 0;
            StatusMessage = "Wyczyszczono Market Watch";

            // Aktualizuj komendy
            ClearAllCommand.RaiseCanExecuteChanged();
            RefreshAllCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Odświeża wszystkie instrumenty (ponowne żądanie snapshot)
        /// </summary>
        private async Task RefreshAllInstrumentsAsync()
        {
            if (!_mdsService.IsConnected || !WatchedInstruments.Any()) return;

            StatusMessage = "Odświeżanie wszystkich instrumentów...";

            // Batch request - wyślij wszystkie requesty bez opóźnień
            var tasks = new List<Task>();
            foreach (var instrument in WatchedInstruments)
            {
                tasks.Add(_mdsService.RequestInstrumentDetails(instrument.GlidAndSymbol));
            }

            try
            {
                await Task.WhenAll(tasks);
                StatusMessage = "Odświeżanie zakończone";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd odświeżania: {ex.Message}");
                StatusMessage = "Błąd podczas odświeżania";
            }
        }

        /// <summary>
        /// Event handler dla otrzymywanych aktualizacji - dodaje do kolejki (szybkie, non-blocking)
        /// </summary>
        private void OnInstrumentDetailsReceived(InstrumentDetails details)
        {
            if (string.IsNullOrEmpty(details.GlidAndSymbol)) return;
            
            // Tylko dodaj do kolejki - bez blokowania
            _updateQueue.Enqueue(details);
            Interlocked.Increment(ref _updatesReceived);
        }

        /// <summary>
        /// Przetwarza batch aktualizacji co 50ms
        /// </summary>
        private async Task ProcessBatchUpdatesAsync()
        {
            if (_updateQueue.IsEmpty) return;

            // Nie pozwól na równoległe przetwarzanie batchy
            if (!await _batchLock.WaitAsync(0))
                return;

            try
            {
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                var updates = new List<InstrumentDetails>();
                
                // Zbierz wszystkie dostępne aktualizacje (max 1000 na batch)
                while (updates.Count < 1000 && _updateQueue.TryDequeue(out var detail))
                {
                    updates.Add(detail);
                }

                if (updates.Count == 0) return;

                // Grupuj aktualizacje po instrumencie - bierzemy tylko ostatnią dla każdego
                var latestUpdates = updates
                    .GroupBy(u => u.GlidAndSymbol)
                    .Select(g => g.Last())
                    .ToList();

                // Aktualizuj UI w jednym Dispatcher.Invoke
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var details in latestUpdates)
                    {
                        // O(1) lookup zamiast O(n)
                        if (_instrumentDictionary.TryGetValue(details.GlidAndSymbol, out var watchInstrument))
                        {
                            // Batch update - zawieś notyfikacje
                            watchInstrument.BeginBatchUpdate();
                            watchInstrument.UpdateFromDetails(details);
                            watchInstrument.EndBatchUpdate();
                            
                            Interlocked.Increment(ref _updatesProcessed);
                        }
                    }

                    // Aktualizuj status tylko co 20 batchy (nie przy każdym)
                    if (_batchCount++ % 20 == 0 && latestUpdates.Count > 0)
                    {
                        var lastUpdate = latestUpdates[0];
                        if (_instrumentDictionary.TryGetValue(lastUpdate.GlidAndSymbol, out var instr))
                        {
                            StatusMessage = $"Zaktualizowano {instr.Symbol} | Batch: {latestUpdates.Count} instrumentów";
                        }
                    }
                }, DispatcherPriority.Background); // Niski priorytet - nie blokuj UI

                startTime.Stop();
                Interlocked.Add(ref _totalUpdateTime, startTime.ElapsedMilliseconds);
            }
            finally
            {
                _batchLock.Release();
            }
        }

        /// <summary>
        /// Aktualizuje statystyki wydajności
        /// </summary>
        private void UpdatePerformanceStats()
        {
            var elapsed = (DateTime.Now - _lastStatsUpdate).TotalSeconds;
            if (elapsed >= 1.0)
            {
                var receivedRate = _updatesReceived / elapsed;
                var processedRate = _updatesProcessed / elapsed;
                var avgBatchTime = _batchCount > 0 ? _totalUpdateTime / (double)_batchCount : 0;
                
                PerformanceStats = $"📊 Otrzymano: {receivedRate:F0}/s | Przetw.: {processedRate:F0}/s | " +
                                 $"Kolejka: {_updateQueue.Count} | Śr. batch: {avgBatchTime:F1}ms";
                
                // Reset liczników
                _updatesReceived = 0;
                _updatesProcessed = 0;
                _totalUpdateTime = 0;
                _batchCount = 0;
                _lastStatsUpdate = DateTime.Now;
            }
        }

        /// <summary>
        /// Cleanup przy zamykaniu okna
        /// </summary>
        public async Task CleanupAsync()
        {
            // Zatrzymaj timer
            _batchTimer.Stop();
            
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
            
            // Cleanup
            _batchLock.Dispose();
        }
    }
}