using System;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class InstrumentDetailsViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;
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

        #region Order Entry Properties

        private bool _isBuy = true;
        public bool IsBuy
        {
            get => _isBuy;
            set
            {
                if (SetProperty(ref _isBuy, value))
                {
                    OnPropertyChanged(nameof(IsSell));
                }
            }
        }

        public bool IsSell
        {
            get => !_isBuy;
            set => IsBuy = !value;
        }

        private string _orderQuantity = "100";
        public string OrderQuantity
        {
            get => _orderQuantity;
            set => SetProperty(ref _orderQuantity, value);
        }

        private string _orderPrice = "";
        public string OrderPrice
        {
            get => _orderPrice;
            set => SetProperty(ref _orderPrice, value);
        }

        private string _selectedModality = "L";
        public string SelectedModality
        {
            get => _selectedModality;
            set
            {
                if (SetProperty(ref _selectedModality, value))
                {
                    OnPropertyChanged(nameof(IsPriceEnabled));
                    SendOrderCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedValidity = "J";
        public string SelectedValidity
        {
            get => _selectedValidity;
            set => SetProperty(ref _selectedValidity, value);
        }

        public bool IsPriceEnabled => SelectedModality == "L";

        public bool IsSleConnected => _sleService?.IsConnected ?? false;

        private bool _isSendingOrder;
        public bool IsSendingOrder
        {
            get => _isSendingOrder;
            set
            {
                if (SetProperty(ref _isSendingOrder, value))
                {
                    SendOrderCommand.RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region Commands

        public RelayCommand RefreshCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand SendOrderCommand { get; }
        public RelayCommand QuickBuyCommand { get; }
        public RelayCommand QuickSellCommand { get; }

        #endregion

        public event Action? RequestClose;

        public InstrumentDetailsViewModel(Instrument instrument, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            _instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            _mdsService = mdsService ?? throw new ArgumentNullException(nameof(mdsService));
            _sleService = sleService ?? throw new ArgumentNullException(nameof(sleService));

            RefreshCommand = new RelayCommand(
                async _ => await LoadDetailsAsync(),
                _ => !IsLoading && _mdsService.IsConnected
            );

            CloseCommand = new RelayCommand(
                _ => RequestClose?.Invoke(),
                _ => true
            );

            SendOrderCommand = new RelayCommand(
                async _ => await SendOrderAsync(),
                _ => CanSendOrder()
            );

            QuickBuyCommand = new RelayCommand(
                async _ => await QuickOrder(true),
                _ => IsSleConnected && !IsSendingOrder && Details != null && Details.AskPrice > 0
            );

            QuickSellCommand = new RelayCommand(
                async _ => await QuickOrder(false),
                _ => IsSleConnected && !IsSendingOrder && Details != null && Details.BidPrice > 0
            );

            // Podpięcie eventu
            _mdsService.InstrumentDetailsReceived += OnInstrumentDetailsReceived;

            // Automatyczne załadowanie danych
            _ = LoadDetailsAsync();
        }

        private bool CanSendOrder()
        {
            if (!IsSleConnected || IsSendingOrder)
                return false;

            if (string.IsNullOrWhiteSpace(OrderQuantity))
                return false;

            if (!long.TryParse(OrderQuantity, out long qty) || qty <= 0)
                return false;

            // Sprawdź cenę tylko dla Limit orders
            if (SelectedModality == "L")
            {
                if (string.IsNullOrWhiteSpace(OrderPrice))
                    return false;

                if (!decimal.TryParse(OrderPrice, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out decimal price) || price <= 0)
                    return false;
            }

            return true;
        }

        private async System.Threading.Tasks.Task SendOrderAsync()
        {
            if (!CanSendOrder())
            {
                MessageBox.Show(
                    "Sprawdź poprawność parametrów zlecenia",
                    "Błąd walidacji",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (!_sleService.IsConnected)
            {
                MessageBox.Show(
                    "Brak połączenia z serwerem SLE!\nPołącz się z serwerem Order Entry przed wysłaniem zlecenia.",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // Potwierdzenie zlecenia
            string sideText = IsBuy ? "KUPNO" : "SPRZEDAŻ";
            string modalityText = SelectedModality == "L" ? "Limit" : 
                                 SelectedModality == "B" ? "At Best" : "Market";
            string priceText = SelectedModality == "L" ? $" po cenie {OrderPrice}" : "";
            
            var result = MessageBox.Show(
                $"Czy na pewno chcesz wysłać zlecenie?\n\n" +
                $"Instrument: {Symbol} ({Name})\n" +
                $"Typ: {sideText}\n" +
                $"Ilość: {OrderQuantity}\n" +
                $"Modalność: {modalityText}{priceText}\n" +
                $"Ważność: {GetValidityDescription(SelectedValidity)}\n\n" +
                $"--- Parametry FIS ---\n" +
                $"Client Code Type: {ClientCodeType}\n" +
                $"Clearing Account: {ClearingAccount}\n" +
                $"Allocation Code: {AllocationCode}\n" +
                $"Floor Trader ID: {FloorTraderId}\n" +
                $"Client Reference: {ClientReference}\n\n" +
                $"GLID: {Glid}",
                "Potwierdzenie zlecenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            IsSendingOrder = true;
            StatusMessage = "Wysyłanie zlecenia...";

            try
            {
                long quantity = long.Parse(OrderQuantity);
                decimal price = !string.IsNullOrEmpty(OrderPrice) && SelectedModality == "L" 
                    ? decimal.Parse(OrderPrice, System.Globalization.NumberStyles.Any, 
                                    System.Globalization.CultureInfo.InvariantCulture) : 0;

                int side = IsBuy ? 0 : 1;
                
                
                bool success = await _sleService.SendOrderAsync(
                    _instrument.LocalCode,  // ← LocalCode do pola G (Stockcode)
                    _instrument.Glid,       // ← GLID do Field 106
                    side,
                    quantity,
                    SelectedModality,
                    price,
                    SelectedValidity,
                    ClientReference,
                    "",
                    ClientCodeType,
                    ClearingAccount,
                    AllocationCode,
                    Memo,
                    SecondClientCodeType,
                    FloorTraderId,
                    ClientFreeField1,
                    Currency
                );

                if (success)
                {
                    StatusMessage = "✓ Zlecenie wysłane pomyślnie";
                    MessageBox.Show(
                        $"Zlecenie zostało wysłane do serwera SLE.\n\n" +
                        $"Szczegóły:\n" +
                        $"- Instrument: {Symbol} (LocalCode: {_instrument.LocalCode})\n" +
                        $"- GLID: {Glid}\n" +
                        $"- Strona: {sideText}\n" +
                        $"- Ilość: {quantity}\n" +
                        $"- Cena: {(price > 0 ? price.ToString("N2") : "Market")}\n" +
                        $"- Clearing Account: {ClearingAccount}\n" +
                        $"- Client Code Type: {ClientCodeType}\n\n" +
                        $"Oczekuj na potwierdzenie w systemie real-time (request 2019).",
                        "Zlecenie wysłane",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    StatusMessage = "✗ Błąd wysyłania zlecenia";
                    MessageBox.Show(
                        "Nie udało się wysłać zlecenia.\nSprawdź połączenie z serwerem SLE i logi debugowania.",
                        "Błąd",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas składania zlecenia:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsSendingOrder = false;
                SendOrderCommand.RaiseCanExecuteChanged();
            }
        }

        private async System.Threading.Tasks.Task QuickOrder(bool isBuy)
        {
            if (Details == null) return;

            // Ustaw parametry quick order
            IsBuy = isBuy;
            SelectedModality = "L";
            
            // Użyj Ask price dla buy, Bid price dla sell
            if (isBuy && Details.AskPrice > 0)
            {
                OrderPrice = Details.AskPrice.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (!isBuy && Details.BidPrice > 0)
            {
                OrderPrice = Details.BidPrice.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            }

            // Wyślij zlecenie
            await SendOrderAsync();
        }

        private string GetValidityDescription(string validity)
        {
            return validity switch
            {
                "J" => "Day (dzień)",
                "K" => "FOK (Fill or Kill)",
                "E" => "E&E (Execute & Eliminate)",
                "R" => "GTC (Good Till Cancelled)",
                "V" => "Auction (aukcja)",
                "C" => "Closing (zamknięcie)",
                _ => validity
            };
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
            System.Diagnostics.Debug.WriteLine($"[ViewModel] ========== RECEIVED DETAILS ==========");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Received GlidAndSymbol: '{details.GlidAndSymbol}' (Length: {details.GlidAndSymbol.Length})");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Expected GLID: '{_instrument.Glid}' (Length: {_instrument.Glid.Length})");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Expected Symbol: '{_instrument.Symbol}' (Length: {_instrument.Symbol.Length})");
            
            // Wyciągnij GLID i Symbol z odpowiedzi
            string receivedGlid = details.GlidAndSymbol.Length >= 12 
                ? details.GlidAndSymbol.Substring(0, 12) 
                : details.GlidAndSymbol;
            
            string receivedSymbol = details.GlidAndSymbol.Length > 12 
                ? details.GlidAndSymbol.Substring(12) 
                : "";
            
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Parsed received GLID: '{receivedGlid}'");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Parsed received Symbol: '{receivedSymbol}'");
            
            // Porównaj GLID (z uwzględnieniem białych znaków i wielkości liter)
            bool glidMatches = receivedGlid.Trim().Equals(_instrument.Glid.Trim(), StringComparison.OrdinalIgnoreCase);
            System.Diagnostics.Debug.WriteLine($"[ViewModel] GLID matches: {glidMatches}");
            
            if (!glidMatches)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] GLID mismatch - ignoring");
                System.Diagnostics.Debug.WriteLine($"[ViewModel] =====================================");
                return;
            }
            
            // Porównaj symbole (ignorując wielkość liter i białe znaki)
            bool symbolMatches = receivedSymbol.Trim().Equals(_instrument.Symbol.Trim(), StringComparison.OrdinalIgnoreCase);
            System.Diagnostics.Debug.WriteLine($"[ViewModel] Symbol matches: {symbolMatches}");
            
            if (!symbolMatches)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] Symbol mismatch - ignoring");
                System.Diagnostics.Debug.WriteLine($"[ViewModel] =====================================");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ViewModel] ✓ MATCH FOUND! Updating UI...");
            System.Diagnostics.Debug.WriteLine($"[ViewModel] =====================================");
            
            // Uruchom na wątku UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                Details = details;
                IsLoading = false;
                StatusMessage = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";
                
                // Zaktualizuj przyciski Quick Order
                QuickBuyCommand.RaiseCanExecuteChanged();
                QuickSellCommand.RaiseCanExecuteChanged();
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
            Volume = Details.Volume > 0 ? Details.Volume.ToString("N0") : "Brak danych";
            
            // Sprawdź czy mamy dane OHLC
            bool hasOHLC = Details.OpenPrice > 0 || Details.HighPrice > 0 || 
                           Details.LowPrice > 0 || Details.ClosePrice > 0;
            
            if (hasOHLC)
            {
                OpenPrice = Details.OpenPrice > 0 ? Details.OpenPrice.ToString("N2") : "-";
                HighPrice = Details.HighPrice > 0 ? Details.HighPrice.ToString("N2") : "-";
                LowPrice = Details.LowPrice > 0 ? Details.LowPrice.ToString("N2") : "-";
                ClosePrice = Details.ClosePrice > 0 ? Details.ClosePrice.ToString("N2") : "-";
            }
            else
            {
                OpenPrice = "Brak danych";
                HighPrice = "Brak danych";
                LowPrice = "Brak danych";
                ClosePrice = "Brak danych";
            }
            
            TradingPhase = !string.IsNullOrEmpty(Details.TradingPhase) ? Details.TradingPhase : "Nieznana";
            SuspensionIndicator = !string.IsNullOrEmpty(Details.SuspensionIndicator) ? Details.SuspensionIndicator : "-";

            // Formatowanie zmiany procentowej
            if (Details.PercentageVariation != 0)
            {
                string sign = Details.VariationSign == "+" ? "+" : Details.VariationSign == "-" ? "-" : "";
                PercentageVariation = $"{sign}{Details.PercentageVariation:N2}%";
                
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
        
        
        #region FIS Workstation Parameters

        private string _clientCodeType = "C"; // Origin: Client
        public string ClientCodeType
        {
            get => _clientCodeType;
            set => SetProperty(ref _clientCodeType, value);
        }

        private string _clearingAccount = "0100"; // Clearing Account
        public string ClearingAccount
        {
            get => _clearingAccount;
            set => SetProperty(ref _clearingAccount, value);
        }

        private string _allocationCode = "0959"; // Allocation receptor
        public string AllocationCode
        {
            get => _allocationCode;
            set => SetProperty(ref _allocationCode, value);
        }

        private string _memo = "7841"; // Memo
        public string Memo
        {
            get => _memo;
            set => SetProperty(ref _memo, value);
        }

        private string _secondClientCodeType = "B"; // Originator Origin: External B (Broker)
        public string SecondClientCodeType
        {
            get => _secondClientCodeType;
            set => SetProperty(ref _secondClientCodeType, value);
        }

        private string _floorTraderId = "0959"; // Own Broker D
        public string FloorTraderId
        {
            get => _floorTraderId;
            set => SetProperty(ref _floorTraderId, value);
        }

        private string _clientFreeField1 = "100"; // Custom
        public string ClientFreeField1
        {
            get => _clientFreeField1;
            set => SetProperty(ref _clientFreeField1, value);
        }

        private string _clientReference = "784"; // Cl. Ref
        public string ClientReference
        {
            get => _clientReference;
            set => SetProperty(ref _clientReference, value);
        }

        private string _currency = "PLN"; // Currency
        public string Currency
        {
            get => _currency;
            set => SetProperty(ref _currency, value);
        }

        #endregion
    }
}
