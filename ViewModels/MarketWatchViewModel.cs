using System;
﻿using System.Collections.Concurrent;
﻿using System.Collections.Generic;
﻿using System.Collections.ObjectModel;
﻿using System.IO;
﻿using System.Linq;
﻿using System.Threading;
﻿using System.Threading.Tasks;
﻿using System.Windows;
﻿using System.Windows.Threading;
﻿using FISApiClient.Helpers;
﻿using FISApiClient.Models;
﻿using FISApiClient.Services;
﻿using Microsoft.Win32;
﻿
﻿namespace FISApiClient.ViewModels
﻿{
﻿    public class MarketWatchViewModel : ViewModelBase
﻿    {
﻿        private readonly MdsConnectionService _mdsService;
﻿        private string? _currentWatchlistFilePath;
﻿        
﻿        // Batch processing
﻿        private readonly ConcurrentQueue<InstrumentDetails> _updateQueue = new();
﻿        private readonly Dictionary<string, MarketWatchInstrument> _instrumentDictionary = new();
﻿        private readonly DispatcherTimer _batchTimer;
﻿        private readonly SemaphoreSlim _batchLock = new(1, 1);
﻿        
﻿        // Performance metrics
﻿        private int _updatesReceived;
﻿        private int _updatesProcessed;
﻿        private DateTime _lastStatsUpdate = DateTime.Now;
﻿        private long _totalUpdateTime;
﻿        private int _batchCount;
﻿
﻿        #region Properties
﻿
﻿        private ObservableCollection<MarketWatchInstrument> _watchedInstruments = new();
﻿        public ObservableCollection<MarketWatchInstrument> WatchedInstruments
﻿        {
﻿            get => _watchedInstruments;
﻿            set => SetProperty(ref _watchedInstruments, value);
﻿        }
﻿
﻿        private MarketWatchInstrument? _selectedInstrument;
﻿        public MarketWatchInstrument? SelectedInstrument
﻿        {
﻿            get => _selectedInstrument;
﻿            set
﻿            {
﻿                if (SetProperty(ref _selectedInstrument, value))
﻿                {
﻿                    RemoveInstrumentCommand.RaiseCanExecuteChanged();
﻿                }
﻿            }
﻿        }
﻿
﻿        private string _statusMessage = "Gotowy. Otwórz listę lub dodaj instrumenty.";
﻿        public string StatusMessage
﻿        {
﻿            get => _statusMessage;
﻿            set => SetProperty(ref _statusMessage, value);
﻿        }
﻿        
﻿        private string _currentFileName = "Nowa lista";
﻿        public string CurrentFileName
﻿        {
﻿            get => _currentFileName;
﻿            set => SetProperty(ref _currentFileName, value);
﻿        }
﻿
﻿        private int _instrumentCount;
﻿        public int InstrumentCount
﻿        {
﻿            get => _instrumentCount;
﻿            set => SetProperty(ref _instrumentCount, value);
﻿        }
﻿
﻿        private bool _isConnected;
﻿        public bool IsConnected
﻿        {
﻿            get => _isConnected;
﻿            set
﻿            {
﻿                if (SetProperty(ref _isConnected, value))
﻿                {
﻿                    OnPropertyChanged(nameof(ConnectionStatusText));
﻿                    OnPropertyChanged(nameof(ConnectionStatusColor));
﻿                }
﻿            }
﻿        }
﻿
﻿        public string ConnectionStatusText => IsConnected ? "Połączono ✓" : "Rozłączono ✗";
﻿        public string ConnectionStatusColor => IsConnected ? "#4CAF50" : "#F44336";
﻿        
﻿        private string _performanceStats = "";
﻿        public string PerformanceStats
﻿        {
﻿            get => _performanceStats;
﻿            set => SetProperty(ref _performanceStats, value);
﻿        }
﻿        
﻿        #endregion
﻿
﻿        #region Commands
﻿
﻿        public RelayCommand OpenWatchlistCommand { get; }
﻿        public RelayCommand SaveWatchlistAsCommand { get; }
﻿        public RelayCommand RemoveInstrumentCommand { get; }
﻿        public RelayCommand ClearAllCommand { get; }
﻿        public RelayCommand RefreshAllCommand { get; }
﻿
﻿        #endregion
﻿
﻿        public MarketWatchViewModel(MdsConnectionService mdsService)
﻿        {
﻿            _mdsService = mdsService;
﻿
﻿            OpenWatchlistCommand = new RelayCommand(async _ => await OpenWatchlistAsync());
﻿            SaveWatchlistAsCommand = new RelayCommand(async _ => await SaveWatchlistAsAsync(), _ => WatchedInstruments.Any());
﻿            
﻿            RemoveInstrumentCommand = new RelayCommand(
﻿                async _ => await RemoveSelectedInstrument(),
﻿                _ => SelectedInstrument != null
﻿            );
﻿
﻿            ClearAllCommand = new RelayCommand(
﻿                async _ => await ClearAll(),
﻿                _ => WatchedInstruments.Any()
﻿            );
﻿
﻿            RefreshAllCommand = new RelayCommand(
﻿                async _ => await RefreshAllInstrumentsAsync(),
﻿                _ => IsConnected && WatchedInstruments.Any()
﻿            );
﻿            
﻿            _mdsService.InstrumentDetailsReceived += OnInstrumentDetailsReceived;
﻿
﻿            _batchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
﻿            _batchTimer.Tick += async (s, e) => await ProcessBatchUpdatesAsync();
﻿            _batchTimer.Start();
﻿
﻿            IsConnected = _mdsService.IsConnected;
﻿            
﻿            Task.Run(async () =>
﻿            {
﻿                while (true)
﻿                {
﻿                    await Task.Delay(1000);
﻿                    Application.Current?.Dispatcher.Invoke(() =>
﻿                    {
﻿                        IsConnected = _mdsService.IsConnected;
﻿                        UpdatePerformanceStats();
﻿                    });
﻿                }
﻿            });
﻿        }
﻿        
﻿        private async Task OpenWatchlistAsync()
﻿        {
﻿            var openFileDialog = new OpenFileDialog
﻿            {
﻿                Filter = "Pliki JSON (*.json)|*.json|Wszystkie pliki (*.*)|*.*",
﻿                Title = "Otwórz listę obserwowanych"
﻿            };
﻿
﻿            if (openFileDialog.ShowDialog() == true)
﻿            {
﻿                try
﻿                {
﻿                    StatusMessage = "Wczytywanie pliku...";
﻿                    await ClearAllInstrumentsAsync(silent: true);
﻿                    
﻿                    var instruments = await WatchListService.LoadWatchlistFromFileAsync(openFileDialog.FileName);
﻿                    
﻿                    foreach (var instrument in instruments)
﻿                    {
﻿                        await AddInstrumentAsync(instrument, silent: true);
﻿                    }
﻿
﻿                    _currentWatchlistFilePath = openFileDialog.FileName;
﻿                    CurrentFileName = Path.GetFileName(_currentWatchlistFilePath);
﻿                    StatusMessage = $"Pomyślnie załadowano {instruments.Count} instrumentów z pliku {CurrentFileName}.";
﻿                }
﻿                catch (Exception ex)
﻿                {
﻿                    StatusMessage = "Błąd ładowania pliku.";
﻿                    MessageBox.Show($"Nie udało się wczytać pliku listy obserwowanych:\n{ex.Message}", "Błąd odczytu", MessageBoxButton.OK, MessageBoxImage.Error);
﻿                }
﻿            }
﻿        }
﻿        
﻿        private async Task SaveWatchlistAsAsync()
﻿        {
﻿            var saveFileDialog = new SaveFileDialog
﻿            {
﻿                Filter = "Pliki JSON (*.json)|*.json",
﻿                Title = "Zapisz listę obserwowanych jako...",
﻿                FileName = "moja-lista.json"
﻿            };
﻿
﻿            if (saveFileDialog.ShowDialog() == true)
﻿            {
﻿                try
﻿                {
﻿                    StatusMessage = "Zapisywanie pliku...";
﻿                    var instrumentsToSave = WatchedInstruments.Select(wi => new Instrument
﻿                    {
﻿                        Glid = wi.Glid, Symbol = wi.Symbol, Name = wi.Name, ISIN = wi.ISIN, LocalCode = wi.LocalCode
﻿                    }).ToList();
﻿
﻿                    await WatchListService.SaveWatchlistToFileAsync(saveFileDialog.FileName, instrumentsToSave);
﻿                    
﻿                    _currentWatchlistFilePath = saveFileDialog.FileName;
﻿                    CurrentFileName = Path.GetFileName(_currentWatchlistFilePath);
﻿                    StatusMessage = $"Pomyślnie zapisano listę do pliku {CurrentFileName}.";
﻿                }
﻿                catch (Exception ex)
﻿                {
﻿                    StatusMessage = "Błąd zapisu pliku.";
﻿                    MessageBox.Show($"Nie udało się zapisać pliku listy obserwowanych:\n{ex.Message}", "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Error);
﻿                }
﻿            }
﻿        }
﻿
﻿        public async Task AddInstrumentAsync(Instrument instrument, bool silent = false)
﻿        {
﻿            if (!_mdsService.IsConnected)
﻿            {
﻿                if (!silent) MessageBox.Show("Brak połączenia z serwerem MDS!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
﻿                return;
﻿            }
﻿
﻿            string glidAndSymbol = instrument.Glid + instrument.Symbol;
﻿
﻿            if (_instrumentDictionary.ContainsKey(glidAndSymbol))
﻿            {
﻿                if (!silent) MessageBox.Show($"Instrument {instrument.Symbol} jest już na liście.", "Duplikat", MessageBoxButton.OK, MessageBoxImage.Information);
﻿                return;
﻿            }
﻿
﻿            var watchInstrument = new MarketWatchInstrument
﻿            {
﻿                GlidAndSymbol = glidAndSymbol,
﻿                Glid = instrument.Glid,
﻿                Symbol = instrument.Symbol,
﻿                Name = instrument.Name,
﻿                ISIN = instrument.ISIN,
﻿                LocalCode = instrument.LocalCode
﻿            };
﻿
﻿            WatchedInstruments.Add(watchInstrument);
﻿            _instrumentDictionary[glidAndSymbol] = watchInstrument;
﻿            InstrumentCount = WatchedInstruments.Count;
﻿            SaveWatchlistAsCommand.RaiseCanExecuteChanged();
﻿
﻿            try
﻿            {
﻿                await _mdsService.RequestInstrumentDetails(glidAndSymbol);
﻿                StatusMessage = $"Dodano {instrument.Symbol}";
﻿            }
﻿            catch (Exception ex)
﻿            {
﻿                System.Diagnostics.Debug.WriteLine($"Błąd dodawania instrumentu: {ex.Message}");
﻿                WatchedInstruments.Remove(watchInstrument);
﻿                _instrumentDictionary.Remove(glidAndSymbol);
﻿                InstrumentCount = WatchedInstruments.Count;
﻿                SaveWatchlistAsCommand.RaiseCanExecuteChanged();
﻿                if (!silent) MessageBox.Show($"Błąd subskrypcji instrumentu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
﻿            }
﻿        }
﻿
﻿        private async Task RemoveSelectedInstrument()
﻿        {
﻿            if (SelectedInstrument == null) return;
﻿
﻿            // No confirmation needed for this workflow
﻿            try
﻿            {
﻿                await _mdsService.StopInstrumentDetailsAsync(SelectedInstrument.GlidAndSymbol);
﻿                
﻿                _instrumentDictionary.Remove(SelectedInstrument.GlidAndSymbol);
﻿                WatchedInstruments.Remove(SelectedInstrument);
﻿                InstrumentCount = WatchedInstruments.Count;
﻿                SaveWatchlistAsCommand.RaiseCanExecuteChanged();
﻿                StatusMessage = $"Usunięto instrument";
﻿            }
﻿            catch (Exception ex)
﻿            {
﻿                System.Diagnostics.Debug.WriteLine($"Błąd usuwania instrumentu: {ex.Message}");
﻿                MessageBox.Show($"Błąd zatrzymywania subskrypcji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
﻿            }
﻿        }
﻿
﻿        private async Task ClearAll()
﻿        {
﻿            var result = MessageBox.Show($"Czy na pewno chcesz wyczyścić bieżącą listę? Niezapisane zmiany zostaną utracone.", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
﻿            if (result != MessageBoxResult.Yes) return;
﻿            
﻿            await ClearAllInstrumentsAsync();
﻿            _currentWatchlistFilePath = null;
﻿            CurrentFileName = "Nowa lista";
﻿        }
﻿
﻿        private async Task ClearAllInstrumentsAsync(bool silent = false)
﻿        {
﻿            if (!WatchedInstruments.Any()) return;
﻿            
﻿            foreach (var instrument in WatchedInstruments.ToList())
﻿            {
﻿                try
﻿                {
﻿                    await _mdsService.StopInstrumentDetailsAsync(instrument.GlidAndSymbol);
﻿                }
﻿                catch (Exception ex)
﻿                {
﻿                    System.Diagnostics.Debug.WriteLine($"Błąd zatrzymywania subskrypcji dla {instrument.Symbol}: {ex.Message}");
﻿                }
﻿            }
﻿
﻿            WatchedInstruments.Clear();
﻿            _instrumentDictionary.Clear();
﻿            InstrumentCount = 0;
﻿            if(!silent) StatusMessage = "Wyczyszczono bieżącą listę";
﻿
﻿            ClearAllCommand.RaiseCanExecuteChanged();
﻿            RefreshAllCommand.RaiseCanExecuteChanged();
﻿            SaveWatchlistAsCommand.RaiseCanExecuteChanged();
﻿        }
﻿
﻿        private async Task RefreshAllInstrumentsAsync()
﻿        {
﻿            if (!_mdsService.IsConnected || !WatchedInstruments.Any()) return;
﻿
﻿            StatusMessage = "Odświeżanie wszystkich instrumentów...";
﻿            var tasks = WatchedInstruments.Select(instrument => _mdsService.RequestInstrumentDetails(instrument.GlidAndSymbol)).ToList();
﻿            try
﻿            {
﻿                await Task.WhenAll(tasks);
﻿                StatusMessage = "Odświeżanie zakończone";
﻿            }
﻿            catch (Exception ex)
﻿            {
﻿                System.Diagnostics.Debug.WriteLine($"Błąd odświeżania: {ex.Message}");
﻿                StatusMessage = "Błąd podczas odświeżania";
﻿            }
﻿        }
﻿
﻿        private void OnInstrumentDetailsReceived(InstrumentDetails details)
﻿        {
﻿            if (string.IsNullOrEmpty(details.GlidAndSymbol)) return;
﻿            _updateQueue.Enqueue(details);
﻿            Interlocked.Increment(ref _updatesReceived);
﻿        }
﻿
﻿        private async Task ProcessBatchUpdatesAsync()
﻿        {
﻿            if (_updateQueue.IsEmpty) return;
﻿
﻿            if (!await _batchLock.WaitAsync(0)) return;
﻿
﻿            try
﻿            {
﻿                var startTime = System.Diagnostics.Stopwatch.StartNew();
﻿                var updates = new List<InstrumentDetails>();
﻿                
﻿                while (updates.Count < 1000 && _updateQueue.TryDequeue(out var detail))
﻿                {
﻿                    updates.Add(detail);
﻿                }
﻿
﻿                if (updates.Count == 0) return;
﻿
﻿                var latestUpdates = updates.GroupBy(u => u.GlidAndSymbol).Select(g => g.Last()).ToList();
﻿
﻿                await Application.Current.Dispatcher.InvokeAsync(() =>
﻿                {
﻿                    foreach (var details in latestUpdates)
﻿                    {
﻿                        if (_instrumentDictionary.TryGetValue(details.GlidAndSymbol, out var watchInstrument))
﻿                        {
﻿                            watchInstrument.BeginBatchUpdate();
﻿                            watchInstrument.UpdateFromDetails(details);
﻿                            watchInstrument.EndBatchUpdate();
﻿                            Interlocked.Increment(ref _updatesProcessed);
﻿                        }
﻿                    }
﻿
﻿                    if (_batchCount++ % 20 == 0 && latestUpdates.Count > 0)
﻿                    {
﻿                        var lastUpdate = latestUpdates[0];
﻿                        if (_instrumentDictionary.TryGetValue(lastUpdate.GlidAndSymbol, out var instr))
﻿                        {
﻿                            StatusMessage = $"Zaktualizowano {instr.Symbol} | Batch: {latestUpdates.Count} instrumentów";
﻿                        }
﻿                    }
﻿                }, DispatcherPriority.Background);
﻿
﻿                startTime.Stop();
﻿                Interlocked.Add(ref _totalUpdateTime, startTime.ElapsedMilliseconds);
﻿            }
﻿            finally
﻿            {
﻿                _batchLock.Release();
﻿            }
﻿        }
﻿
﻿        private void UpdatePerformanceStats()
﻿        {
﻿            var elapsed = (DateTime.Now - _lastStatsUpdate).TotalSeconds;
﻿            if (elapsed >= 1.0)
﻿            {
﻿                var receivedRate = _updatesReceived / elapsed;
﻿                var processedRate = _updatesProcessed / elapsed;
﻿                var avgBatchTime = _batchCount > 0 ? _totalUpdateTime / (double)_batchCount : 0;
﻿                
﻿                PerformanceStats = $"📊 Otrzymano: {receivedRate:F0}/s | Przetw.: {processedRate:F0}/s | " +
﻿                                 $"Kolejka: {_updateQueue.Count} | Śr. batch: {avgBatchTime:F1}ms";
﻿                
﻿                _updatesReceived = 0;
﻿                _updatesProcessed = 0;
﻿                _totalUpdateTime = 0;
﻿                _batchCount = 0;
﻿                _lastStatsUpdate = DateTime.Now;
﻿            }
﻿        }
﻿
﻿        public async Task CleanupAsync()
﻿        {
﻿            _batchTimer.Stop();
﻿            
﻿            foreach (var instrument in WatchedInstruments.ToList())
﻿            {
﻿                try
﻿                {
﻿                    await _mdsService.StopInstrumentDetailsAsync(instrument.GlidAndSymbol);
﻿                }
﻿                catch (Exception ex)
﻿                {
﻿                    System.Diagnostics.Debug.WriteLine($"Błąd cleanup dla {instrument.Symbol}: {ex.Message}");
﻿                }
﻿            }
﻿
﻿            _mdsService.InstrumentDetailsReceived -= OnInstrumentDetailsReceived;
﻿            _batchLock.Dispose();
﻿        }
﻿    }
﻿}
﻿