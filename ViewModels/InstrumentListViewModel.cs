using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;
using FISApiClient.Services;
using System.Diagnostics;

namespace FISApiClient.ViewModels
{
    public class InstrumentListViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;
        private readonly InstrumentCacheService _cacheService;
        private readonly NavigationService _navigationService;

        // Flaga do śledzenia czy instrumenty są obecnie ładowane z serwera
        private bool _isLoadingFromServer = false;
        private List<Instrument> _receivedInstruments = new List<Instrument>();

        #region Properties

        private ObservableCollection<Instrument> _instruments = new ObservableCollection<Instrument>();
        public ObservableCollection<Instrument> Instruments
        {
            get => _instruments;
            set => SetProperty(ref _instruments, value);
        }

        private ObservableCollection<Instrument> _filteredInstruments = new ObservableCollection<Instrument>();
        public ObservableCollection<Instrument> FilteredInstruments
        {
            get => _filteredInstruments;
            set => SetProperty(ref _filteredInstruments, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterInstruments();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    LoadInstrumentsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "Kliknij 'Pobierz instrumenty' aby załadować dane";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            set => SetProperty(ref _filteredCount, value);
        }

        private Instrument? _selectedInstrument;
        public Instrument? SelectedInstrument
        {
            get => _selectedInstrument;
            set => SetProperty(ref _selectedInstrument, value);
        }

        #endregion

        #region Commands

        public RelayCommand LoadInstrumentsCommand { get; }
        public RelayCommand ClearSearchCommand { get; }
        public RelayCommand ExportToCsvCommand { get; }
        public RelayCommand OpenMarketWatchCommand { get; }
        public RelayCommand AddToMarketWatchCommand { get; }

        #endregion

        public InstrumentListViewModel(MdsConnectionService mdsService, SleConnectionService sleService)
        {
            _mdsService = mdsService;
            _sleService = sleService;
            _cacheService = new InstrumentCacheService();
            _navigationService = new NavigationService();

            LoadInstrumentsCommand = new RelayCommand(
                async _ => await LoadInstrumentsAsync(),
                _ => !IsLoading && _mdsService.IsConnected
            );

            ClearSearchCommand = new RelayCommand(
                _ => SearchText = string.Empty,
                _ => !string.IsNullOrEmpty(SearchText)
            );

            ExportToCsvCommand = new RelayCommand(
                _ => ExportToCsv(),
                _ => Instruments.Any()
            );

            OpenMarketWatchCommand = new RelayCommand(
                _ => OpenMarketWatch(),
                _ => _mdsService.IsConnected
            );

            AddToMarketWatchCommand = new RelayCommand(
                async _ => await AddSelectedToMarketWatchAsync(),
                _ => SelectedInstrument != null && _mdsService.IsConnected
            );

            // Podpięcie eventu do otrzymywania instrumentów
            _mdsService.InstrumentsReceived += OnInstrumentsReceived;

            LoadInstrumentsAsync();
        }

        private async System.Threading.Tasks.Task LoadInstrumentsAsync()
        {
            if (!_mdsService.IsConnected)
            {
                MessageBox.Show(
                    "Najpierw połącz się z serwerem MDS/SLC!",
                    "Brak połączenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            IsLoading = true;
            StatusMessage = "Sprawdzanie cache...";
            
            try
            {
                // Wyczyść obecną listę
                Instruments.Clear();
                FilteredInstruments.Clear();
                TotalCount = 0;
                FilteredCount = 0;

                // Sprawdź czy istnieje cache dla dzisiejszej daty
                if (_cacheService.HasTodayCache())
                {
                    Debug.WriteLine("[InstrumentListVM] Cache for today found, loading from cache");
                    StatusMessage = "Ładowanie z cache...";

                    var cacheData = await _cacheService.LoadCacheAsync();

                    if (cacheData.HasValue)
                    {
                        Debug.WriteLine($"[InstrumentListVM] Successfully loaded {cacheData.Value.instruments.Count} instruments from cache");
                        
                        // Załaduj instrumenty
                        foreach (var instrument in cacheData.Value.instruments)
                        {
                            Instruments.Add(instrument);
                        }

                        // Załaduj szczegóły do cache w MdsConnectionService
                        _mdsService.LoadInstrumentDetailsCache(cacheData.Value.details);

                        TotalCount = Instruments.Count;
                        FilterInstruments();

                        StatusMessage = $"Załadowano {TotalCount} instrumentów z cache (dzisiejsza data)";
                        IsLoading = false;
                        return;
                    }
                    else
                    {
                        Debug.WriteLine("[InstrumentListVM] Failed to load from cache, will fetch from server");
                        StatusMessage = "Błąd odczytu cache, pobieranie z serwera...";
                    }
                }
                else
                {
                    Debug.WriteLine("[InstrumentListVM] No cache for today, fetching from server");
                    StatusMessage = "Brak cache, pobieranie z serwera...";
                }

                // Brak cache lub błąd odczytu - pobierz z serwera
                _isLoadingFromServer = true;
                _receivedInstruments.Clear();
                
                StatusMessage = "Pobieranie instrumentów z serwera...";

                // Wyślij żądania dla wszystkich rynków
                await _mdsService.RequestAllInstrumentsAsync();

                StatusMessage = "Oczekiwanie na odpowiedzi od serwera...";

                // Ustaw timeout - jeśli po 30 sekundach nie otrzymamy wszystkich danych
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(30000); // 30 sekund
                    if (_isLoadingFromServer && IsLoading)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FinishLoadingFromServer();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas ładowania instrumentów:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                IsLoading = false;
                _isLoadingFromServer = false;
            }
        }

        private void OnInstrumentsReceived(List<Instrument> newInstruments)
        {
            if (!_isLoadingFromServer)
            {
                return; // Ignoruj jeśli nie ładujemy obecnie z serwera
            }

            // Uruchom na wątku UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                Debug.WriteLine($"[InstrumentListVM] Received {newInstruments.Count} instruments from server");

                // Dodaj do tymczasowej listy
                _receivedInstruments.AddRange(newInstruments);

                // Dodaj do wyświetlanej listy
                foreach (var instrument in newInstruments)
                {
                    Instruments.Add(instrument);
                }

                TotalCount = Instruments.Count;
                FilterInstruments();

                StatusMessage = $"Pobrano {TotalCount} instrumentów (w trakcie...)";

                // Sprawdź czy mamy już wszystkie instrumenty (prosty heurystyk - po 5 sekundach bez nowych danych)
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    var currentCount = TotalCount;
                    await System.Threading.Tasks.Task.Delay(5000); // 5 sekund
                    
                    if (_isLoadingFromServer && currentCount == TotalCount)
                    {
                        // Liczba się nie zmieniła przez 5 sekund - prawdopodobnie skończyliśmy
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FinishLoadingFromServer();
                        });
                    }
                });
            });
        }

        private async void FinishLoadingFromServer()
        {
            if (!_isLoadingFromServer)
                return;

            _isLoadingFromServer = false;

            Debug.WriteLine($"[InstrumentListVM] Finishing server load, total instruments: {_receivedInstruments.Count}");
            StatusMessage = $"Zapisywanie do cache...";

            try
            {
                // Zapisz do cache
                var detailsCache = _mdsService.GetInstrumentDetailsCache();
                await _cacheService.SaveCacheAsync(_receivedInstruments.ToList(), detailsCache);

                Debug.WriteLine($"[InstrumentListVM] Cache saved successfully");
                StatusMessage = $"Pobrano {TotalCount} instrumentów (zapisano w cache)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InstrumentListVM] Error saving cache: {ex.Message}");
                StatusMessage = $"Pobrano {TotalCount} instrumentów (błąd zapisu cache)";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterInstruments()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredInstruments.Clear();
                foreach (var instrument in Instruments)
                {
                    FilteredInstruments.Add(instrument);
                }
            }
            else
            {
                var searchLower = SearchText.ToLower();
                var filtered = Instruments.Where(i =>
                    i.Symbol.ToLower().Contains(searchLower) ||
                    i.Name.ToLower().Contains(searchLower) ||
                    i.ISIN.ToLower().Contains(searchLower) ||
                    i.Glid.ToLower().Contains(searchLower)
                ).ToList();

                FilteredInstruments.Clear();
                foreach (var instrument in filtered)
                {
                    FilteredInstruments.Add(instrument);
                }
            }

            FilteredCount = FilteredInstruments.Count;
        }

        private void ExportToCsv()
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"instruments_{DateTime.Now:yyyy-MM-dd}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("GLID,Symbol,Name,LocalCode,ISIN");

                    foreach (var instrument in Instruments)
                    {
                        csv.AppendLine($"\"{instrument.Glid}\",\"{instrument.Symbol}\",\"{instrument.Name}\",\"{instrument.LocalCode}\",\"{instrument.ISIN}\"");
                    }

                    System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString());

                    MessageBox.Show(
                        $"Wyeksportowano {Instruments.Count} instrumentów do pliku CSV.",
                        "Eksport zakończony",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas eksportu:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private MarketWatchViewModel OpenMarketWatch()
        {
            return _navigationService.ShowMarketWatchWindow(_mdsService, _sleService);
        }

        private async System.Threading.Tasks.Task AddSelectedToMarketWatchAsync()
        {
            if (SelectedInstrument == null) return;

            // Otwórz Market Watch i pobierz jego ViewModel
            var marketWatchViewModel = OpenMarketWatch();

            // Dodaj instrument
            await marketWatchViewModel.AddInstrumentAsync(SelectedInstrument);
        }
    }
}