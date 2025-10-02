using System;
using System.Windows;
using Cross_FIS_API_1._2.Helpers;
using Cross_FIS_API_1._2.Models;

namespace Cross_FIS_API_1._2.ViewModels
{
    public class InstrumentDetailsViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        private readonly Instrument _instrument;
        private bool _isRequestInProgress = false;

        #region Properties

        public string WindowTitle => $"Szczegóły instrumentu - {_instrument.Symbol}";

        public string Symbol => _instrument.Symbol;
        public string Name => _instrument.Name;
        public string Glid => _instrument.Glid;
        public string ISIN => _instrument.ISIN;

        private InstrumentDetails? _details;
        public InstrumentDetails? Details
        {
            get => _details;
            set
            {
                if (SetProperty(ref _details, value))
                {
                    UpdateAllProperties();
                }
            }
        }

        private string _bidQuantity = "-";
        public string BidQuantity
        {
            get => _bidQuantity;
            set => SetProperty(ref _bidQuantity, value);
        }

        private string _bidPrice = "-";
        public string BidPrice
        {
            get => _bidPrice;
            set => SetProperty(ref _bidPrice, value);
        }

        private string _askPrice = "-";
        public string AskPrice
        {
            get => _askPrice;
            set => SetProperty(ref _askPrice, value);
        }

        private string _askQuantity = "-";
        public string AskQuantity
        {
            get => _askQuantity;
            set => SetProperty(ref _askQuantity, value);
        }

        private string _lastPrice = "-";
        public string LastPrice
        {
            get => _lastPrice;
            set => SetProperty(ref _lastPrice, value);
        }

        private string _lastQuantity = "-";
        public string LastQuantity
        {
            get => _lastQuantity;
            set => SetProperty(ref _lastQuantity, value);
        }

        private string _lastTradeTime = "-";
        public string LastTradeTime
        {
            get => _lastTradeTime;
            set => SetProperty(ref _lastTradeTime, value);
        }

        private string _percentageVariation = "-";
        public string PercentageVariation
        {
            get => _percentageVariation;
            set => SetProperty(ref _percentageVariation, value);
        }

        private string _variationColor = "#666666";
        public string VariationColor
        {
            get => _variationColor;
            set => SetProperty(ref _variationColor, value);
        }

        private string _volume = "-";
        public string Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        private string _openPrice = "-";
        public string OpenPrice
        {
            get => _openPrice;
            set => SetProperty(ref _openPrice, value);
        }

        private string _highPrice = "-";
        public string HighPrice
        {
            get => _highPrice;
            set => SetProperty(ref _highPrice, value);
        }

        private string _lowPrice = "-";
        public string LowPrice
        {
            get => _lowPrice;
            set => SetProperty(ref _lowPrice, value);
        }

        private string _closePrice = "-";
        public string ClosePrice
        {
            get => _closePrice;
            set => SetProperty(ref _closePrice, value);
        }

        private string _tradingPhase = "-";
        public string TradingPhase
        {
            get => _tradingPhase;
            set => SetProperty(ref _tradingPhase, value);
        }

        private string _suspensionIndicator = "-";
        public string SuspensionIndicator
        {
            get => _suspensionIndicator;
            set => SetProperty(ref _suspensionIndicator, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    RefreshCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "Ładowanie danych...";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _spread = "-";
        public string Spread
        {
            get => _spread;
            set => SetProperty(ref _spread, value);
        }

        private string _spreadPercentage = "-";
        public string SpreadPercentage
        {
            get => _spreadPercentage;
            set => SetProperty(ref _spreadPercentage, value);
        }

        #endregion

        #region Commands

        public RelayCommand RefreshCommand { get; }
        public RelayCommand CloseCommand { get; }

        #endregion

        public event Action? RequestClose;

        public InstrumentDetailsViewModel(Instrument instrument, MdsConnectionService mdsService)
        {
            _instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            _mdsService = mdsService ?? throw new ArgumentNullException(nameof(mdsService));

            RefreshCommand = new RelayCommand(
                async _ => await LoadDetailsAsync(),
                _ => !IsLoading && _mdsService.IsConnected
            );

            CloseCommand = new RelayCommand(
                _ => RequestClose?.Invoke(),
                _ => true
            );

            // Podpięcie eventu
            _mdsService.InstrumentDetailsReceived += OnInstrumentDetailsReceived;

            // Automatyczne załadowanie danych
            _ = LoadDetailsAsync();
        }

        private async System.Threading.Tasks.Task LoadDetailsAsync()
        {
            if (!_mdsService.IsConnected)
            {
                StatusMessage = "Brak połączenia z serwerem";
                MessageBox.Show(
                    "Brak połączenia z serwerem MDS/SLC",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Zapobiegaj wielokrotnym jednoczesnym requestom
            if (_isRequestInProgress)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Request already in progress, ignoring");
                return;
            }

            _isRequestInProgress = true;
            IsLoading = true;
            StatusMessage = "Pobieranie danych z serwera...";
            
            System.Diagnostics.Debug.WriteLine($"[ViewModel] LoadDetailsAsync started");

            try
            {
                string glidAndSymbol = _instrument.Glid + _instrument.Symbol;
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Requesting details for: {glidAndSymbol}");
                
                await _mdsService.RequestInstrumentDetails(glidAndSymbol);
                
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Request sent, waiting for response...");
                
                // Czekaj maksymalnie 10 sekund na odpowiedź
                int waitTime = 0;
                int maxWait = 10000; // 10 sekund
                int checkInterval = 100; // sprawdzaj co 100ms
                
                DateTime requestTime = DateTime.Now;
                
                while (waitTime < maxWait)
                {
                    await System.Threading.Tasks.Task.Delay(checkInterval);
                    waitTime += checkInterval;
                    
                    // Sprawdź czy IsLoading zostało wyłączone przez OnInstrumentDetailsReceived
                    if (!IsLoading)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ViewModel] Data received after {waitTime}ms");
                        _isRequestInProgress = false;
                        return; // Dane otrzymane
                    }
                }
                
                // Timeout - nie otrzymano danych
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Timeout after {maxWait}ms");
                IsLoading = false;
                StatusMessage = "Timeout: Nie otrzymano odpowiedzi od serwera";
                MessageBox.Show(
                    "Serwer nie odpowiedział w ciągu 10 sekund.\nSpróbuj ponownie.",
                    "Timeout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Exception: {ex.Message}");
                StatusMessage = $"Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas pobierania szczegółów:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                IsLoading = false;
            }
            finally
            {
                _isRequestInProgress = false;
            }
        }

        private void OnInstrumentDetailsReceived(InstrumentDetails details)
        {
            // Debug - sprawdź co przyszło
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Received details for: '{details.GlidAndSymbol}'");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Expected GLID: '{_instrument.Glid}'");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Expected Symbol: '{_instrument.Symbol}'");
            
            string expectedGlidAndSymbol = _instrument.Glid + _instrument.Symbol;
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Expected full: '{expectedGlidAndSymbol}'");
            
            // Sprawdź czy to dane dla tego instrumentu
            // Porównanie: czy GlidAndSymbol zaczyna się od naszego GLID
            if (!details.GlidAndSymbol.StartsWith(_instrument.Glid))
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] GLID mismatch - ignoring");
                return;
            }
            
            // Wyciągnij symbol z odpowiedzi (po GLID)
            string receivedSymbol = details.GlidAndSymbol.Length > 12 
                ? details.GlidAndSymbol.Substring(12).Trim() 
                : "";
            
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Received symbol: '{receivedSymbol}'");
            
            // Porównaj symbole (ignorując wielkość liter i białe znaki)
            if (!receivedSymbol.Equals(_instrument.Symbol.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Symbol mismatch - ignoring");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ViewModel] Match found! Updating UI...");
            
            // Uruchom na wątku UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                Details = details;
                IsLoading = false;
                StatusMessage = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";
            });
        }

        private void UpdateAllProperties()
        {
            if (Details == null) return;

            BidQuantity = Details.BidQuantity > 0 ? Details.BidQuantity.ToString("N0") : "-";
            BidPrice = Details.BidPrice > 0 ? Details.BidPrice.ToString("N2") : "-";
            AskPrice = Details.AskPrice > 0 ? Details.AskPrice.ToString("N2") : "-";
            AskQuantity = Details.AskQuantity > 0 ? Details.AskQuantity.ToString("N0") : "-";
            LastPrice = Details.LastPrice > 0 ? Details.LastPrice.ToString("N2") : "-";
            LastQuantity = Details.LastQuantity > 0 ? Details.LastQuantity.ToString("N0") : "-";
            LastTradeTime = !string.IsNullOrEmpty(Details.LastTradeTime) ? Details.LastTradeTime : "-";
            Volume = Details.Volume > 0 ? Details.Volume.ToString("N0") : "-";
            OpenPrice = Details.OpenPrice > 0 ? Details.OpenPrice.ToString("N2") : "-";
            HighPrice = Details.HighPrice > 0 ? Details.HighPrice.ToString("N2") : "-";
            LowPrice = Details.LowPrice > 0 ? Details.LowPrice.ToString("N2") : "-";
            ClosePrice = Details.ClosePrice > 0 ? Details.ClosePrice.ToString("N2") : "-";
            TradingPhase = !string.IsNullOrEmpty(Details.TradingPhase) ? Details.TradingPhase : "-";
            SuspensionIndicator = !string.IsNullOrEmpty(Details.SuspensionIndicator) ? Details.SuspensionIndicator : "-";

            // Formatowanie zmiany procentowej
            if (Details.PercentageVariation != 0)
            {
                string sign = Details.VariationSign == "+" ? "+" : Details.VariationSign == "-" ? "-" : "";
                PercentageVariation = $"{sign}{Details.PercentageVariation:N2}%";
                
                // Kolor w zależności od znaku
                VariationColor = Details.VariationSign == "+" ? "#4CAF50" : 
                                Details.VariationSign == "-" ? "#F44336" : "#666666";
            }
            else
            {
                PercentageVariation = "0.00%";
                VariationColor = "#666666";
            }

            // Oblicz spread
            if (Details.BidPrice > 0 && Details.AskPrice > 0)
            {
                decimal spreadValue = Details.AskPrice - Details.BidPrice;
                Spread = spreadValue.ToString("N2");
                
                if (Details.BidPrice > 0)
                {
                    decimal spreadPercent = (spreadValue / Details.BidPrice) * 100;
                    SpreadPercentage = spreadPercent.ToString("N2") + "%";
                }
                else
                {
                    SpreadPercentage = "-";
                }
            }
            else
            {
                Spread = "-";
                SpreadPercentage = "-";
            }
        }

        public void Cleanup()
        {
            _mdsService.InstrumentDetailsReceived -= OnInstrumentDetailsReceived;
        }
    }
}
