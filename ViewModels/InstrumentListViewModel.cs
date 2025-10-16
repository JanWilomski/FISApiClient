using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class InstrumentListViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        private Views.MarketWatchWindow? _marketWatchWindow;

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

        public InstrumentListViewModel(MdsConnectionService mdsService)
        {
            _mdsService = mdsService;

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
            StatusMessage = "Pobieranie instrumentów...";
            
            try
            {
                // Wyczyść obecną listę
                Instruments.Clear();
                FilteredInstruments.Clear();
                TotalCount = 0;
                FilteredCount = 0;

                // Wyślij żądania dla wszystkich rynków
                await _mdsService.RequestAllInstrumentsAsync();

                StatusMessage = "Oczekiwanie na odpowiedzi od serwera...";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas pobierania instrumentów:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                // IsLoading pozostanie true dopóki nie otrzymamy wszystkich odpowiedzi
                // Zostanie wyłączone w OnInstrumentsReceived po timeout
            }
        }

        private void OnInstrumentsReceived(List<Instrument> newInstruments)
        {
            // Uruchom na wątku UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var instrument in newInstruments)
                {
                    Instruments.Add(instrument);
                }

                TotalCount = Instruments.Count;
                FilterInstruments();

                StatusMessage = $"Pobrano {TotalCount} instrumentów";
                IsLoading = false;
            });
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
                    DefaultExt = ".csv",
                    FileName = $"Instruments_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var writer = new System.IO.StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Nagłówek
                        writer.WriteLine("GLID,Symbol,Name,ISIN");

                        // Dane
                        foreach (var instrument in FilteredInstruments)
                        {
                            writer.WriteLine($"\"{instrument.Glid}\",\"{instrument.Symbol}\",\"{instrument.Name}\",\"{instrument.ISIN}\"");
                        }
                    }

                    MessageBox.Show(
                        $"Wyeksportowano {FilteredCount} instrumentów do pliku:\n{saveDialog.FileName}",
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

        /// <summary>
        /// Otwiera okno Market Watch lub przynosi na wierzch jeśli już otwarte
        /// </summary>
        private void OpenMarketWatch()
        {
            if (_marketWatchWindow == null || !_marketWatchWindow.IsLoaded)
            {
                var viewModel = new MarketWatchViewModel(_mdsService);
                _marketWatchWindow = new Views.MarketWatchWindow(viewModel);
                _marketWatchWindow.Closed += (s, e) => _marketWatchWindow = null;
                _marketWatchWindow.Show();
            }
            else
            {
                // Okno już istnieje - przywróć je na wierzch
                if (_marketWatchWindow.WindowState == WindowState.Minimized)
                {
                    _marketWatchWindow.WindowState = WindowState.Normal;
                }
                _marketWatchWindow.Activate();
            }
        }

        /// <summary>
        /// Dodaje wybrany instrument do Market Watch
        /// </summary>
        private async System.Threading.Tasks.Task AddSelectedToMarketWatchAsync()
        {
            if (SelectedInstrument == null) return;

            // Otwórz Market Watch jeśli nie jest otwarte
            if (_marketWatchWindow == null || !_marketWatchWindow.IsLoaded)
            {
                OpenMarketWatch();
            }

            // Dodaj instrument
            if (_marketWatchWindow?.DataContext is MarketWatchViewModel viewModel)
            {
                await viewModel.AddInstrumentAsync(SelectedInstrument);
            }
        }
    }
}
