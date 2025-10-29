using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FISApiClient.Helpers;
using FISApiClient.Models;
using FISApiClient.Services; // Added for Manager Service
using FISApiClient.Trading.Strategies;

namespace FISApiClient.ViewModels
{
    public class AlgoStrategyViewModel : ViewModelBase
    {
        private readonly Instrument _instrument;
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;
        private readonly FisOrderParametersProvider _fisParamsProvider;
        private IAlgoStrategy? _activeStrategy;
        private CancellationTokenSource? _cts;

        private AlgoStrategyInfo? _selectedStrategy;
        private string _side = "Buy";
        private string _totalQuantity = "100";
        private string _limitPrice = "";
        private string _orderType = "Limit";

        private string _participationRate = "20";
        public string ParticipationRate
        {
            get => _participationRate;
            set => SetProperty(ref _participationRate, value);
        }

        public AlgoStrategyViewModel(Instrument instrument, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            _instrument = instrument;
            _mdsService = mdsService;
            _sleService = sleService;
            _fisParamsProvider = new FisOrderParametersProvider();

            InitializeAvailableStrategies();
            InitializeCommands();

            _limitPrice = "";
        }

        #region Properties

        public ObservableCollection<AlgoStrategyInfo> AvailableStrategies { get; } = new();

        public AlgoStrategyInfo? SelectedStrategy
        {
            get => _selectedStrategy;
            set
            {
                if (SetProperty(ref _selectedStrategy, value))
                {
                    OnPropertyChanged(nameof(HasSelectedStrategy));
                    OnPropertyChanged(nameof(HasNoSelectedStrategy));
                    OnPropertyChanged(nameof(CanStartStrategy));
                }
            }
        }

        public bool HasSelectedStrategy => SelectedStrategy != null;
        public bool HasNoSelectedStrategy => SelectedStrategy == null;
        public bool CanStartStrategy => SelectedStrategy != null && _activeStrategy == null;

        public string InstrumentInfo => $"{_instrument.Symbol} ({_instrument.Name}) - ISIN: {_instrument.ISIN}";

        public string Side
        {
            get => _side;
            set => SetProperty(ref _side, value);
        }

        public string TotalQuantity
        {
            get => _totalQuantity;
            set => SetProperty(ref _totalQuantity, value);
        }

        public string LimitPrice
        {
            get => _limitPrice;
            set => SetProperty(ref _limitPrice, value);
        }

        public string OrderType
        {
            get => _orderType;
            set => SetProperty(ref _orderType, value);
        }

        #endregion

        #region Commands

        public ICommand StartStrategyCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private void InitializeCommands()
        {
            StartStrategyCommand = new RelayCommand(async _ => await ExecuteStartStrategy(), _ => CanExecuteStartStrategy(null));
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private bool CanExecuteStartStrategy(object? parameter)
        {
            return SelectedStrategy != null && _activeStrategy == null;
        }

        private async Task ExecuteStartStrategy()
        {
            if (SelectedStrategy == null) return;

            // 1. Validate Parameters
            if (!long.TryParse(TotalQuantity, out var totalQuantity) || totalQuantity <= 0)
            {
                MessageBox.Show("Całkowita ilość musi być dodatnią liczbą.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Enum.TryParse<OrderSide>(Side, true, out var orderSide))
            {
                MessageBox.Show("Nieprawidłowy kierunek zlecenia.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? limitPrice = null;
            if (OrderType == "Limit")
            {
                if (!decimal.TryParse(LimitPrice, out var price) || price <= 0)
                {
                    MessageBox.Show("Cena limit musi być dodatnią liczbą dla zlecenia typu Limit.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                limitPrice = price;
            }

            // 2. Create Strategy Instance
            var orderParams = new AlgoOrderParams
            {
                Instrument = _instrument,
                Side = orderSide,
                TotalQuantity = totalQuantity,
                LimitPrice = limitPrice,
                OrderType = OrderType == "Limit" ? OrderModality.Limit : OrderModality.Market,
            };
            
            // *** Crucial: Use the centralized provider for FIS params ***
            _fisParamsProvider.PopulateAlgoOrderParams(orderParams);


            switch (SelectedStrategy.Id)
            {
                case "pov":
                    if (!double.TryParse(ParticipationRate, out var rate) || rate <= 0 || rate > 100)
                    {
                        MessageBox.Show("Udział w wolumenie musi być liczbą z zakresu (0, 100].", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    _activeStrategy = new ParticipationOfVolumeStrategy(_sleService, _mdsService, orderParams);
                    _activeStrategy.Initialize(new Dictionary<string, object> { { "ParticipationRate", rate } });
                    break;
                default:
                    MessageBox.Show($"Strategia '{SelectedStrategy.Name}' nie jest jeszcze zaimplementowana.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
            }

            // 3. Validate and Start Strategy
            if (!_activeStrategy.ValidateParameters(out var errorMessage))
            {
                MessageBox.Show($"Błąd parametrów strategii: {errorMessage}", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                _activeStrategy = null;
                return;
            }

            _activeStrategy.OrderRequested += OnStrategyOrderRequested;
            _activeStrategy.StatusChanged += OnStrategyStatusChanged;

            _cts = new CancellationTokenSource();
            
            // Register with the manager BEFORE starting
            AlgoStrategyManagerService.Instance.Register(_activeStrategy);
            
            await _activeStrategy.StartAsync(_cts.Token);

            MessageBox.Show($"Strategia '{_activeStrategy.Name}' została uruchomiona.", "Strategia Aktywna", MessageBoxButton.OK, MessageBoxImage.Information);
            RequestClose?.Invoke();
        }

        private async void OnStrategyOrderRequested(object? sender, AlgoOrderRequest request)
        {
            try
            {
                await _sleService.SendOrderAsync(
                    request.LocalCode,
                    request.Glid,
                    request.Side,
                    request.Quantity,
                    request.Modality,
                    request.Price,
                    request.Validity,
                    request.ClientReference,
                    "", // contraFirm
                    request.ClientCodeType,
                    request.ClearingAccount,
                    request.AllocationCode,
                    request.Memo,
                    request.SecondClientCodeType,
                    request.FloorTraderId,
                    request.ClientFreeField1,
                    request.Currency
                );
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"[AlgoVM] Failed to send order from strategy: {ex.Message}");
            }
        }

        private void OnStrategyStatusChanged(object? sender, AlgoStrategyStatus status)
        {
            if (status == AlgoStrategyStatus.Completed || status == AlgoStrategyStatus.Stopped || status == AlgoStrategyStatus.Error)
            {
                if (_activeStrategy != null)
                {
                    // The manager will handle unregistering, just clean up local state
                    _activeStrategy.OrderRequested -= OnStrategyOrderRequested;
                    _activeStrategy.StatusChanged -= OnStrategyStatusChanged;
                    _activeStrategy = null;
                }
                _cts?.Dispose();
                _cts = null;
                
                Application.Current.Dispatcher.Invoke(() => 
                    ((RelayCommand)StartStrategyCommand).RaiseCanExecuteChanged()
                );
            }
        }

        private void ExecuteCancel(object? parameter)
        {
            if (_activeStrategy != null)
            {
                var result = MessageBox.Show(
                    "Aktywna strategia jest uruchomiona. Czy na pewno chcesz ją zatrzymać i zamknąć okno?",
                    "Potwierdzenie",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _activeStrategy?.StopAsync(); // This will trigger status change and cleanup
                    RequestClose?.Invoke();
                }
            }
            else
            {
                RequestClose?.Invoke();
            }
        }

        #endregion

        #region Methods

        public void SelectStrategy(AlgoStrategyInfo strategy)
        {
            SelectedStrategy = strategy;
        }

        private void InitializeAvailableStrategies()
        {
            // Mock strategies - will be replaced with real implementations
            AvailableStrategies.Add(new AlgoStrategyInfo
            {
                Id = "vwap",
                Name = "VWAP",
                Category = "Volume",
                Description = "Volume Weighted Average Price - realizacja po średniej ważonej wolumenem",
                DetailedDescription = "Strategia VWAP dzieli zlecenie na mniejsze części i wykonuje je stopniowo, " +
                    "starając się osiągnąć cenę zbliżoną do średniej ważonej wolumenem w zadanym okresie czasu. " +
                    "Idealna do dużych zleceń, które mogłyby wpłynąć na cenę rynkową."
            });

            AvailableStrategies.Add(new AlgoStrategyInfo
            {
                Id = "twap",
                Name = "TWAP",
                Category = "Time",
                Description = "Time Weighted Average Price - równomierne rozłożenie w czasie",
                DetailedDescription = "Strategia TWAP dzieli zlecenie na równe części i wykonuje je w regularnych " +
                    "odstępach czasu. Zapewnia równomierne uczestnictwo w rynku w wybranym okresie, minimalizując " +
                    "wpływ na cenę."
            });

            AvailableStrategies.Add(new AlgoStrategyInfo
            {
                Id = "pov",
                Name = "POV (% Volume)",
                Category = "Volume",
                Description = "Percentage of Volume - realizacja jako procent bieżącego wolumenu",
                DetailedDescription = "Strategia POV wykonuje zlecenie jako określony procent aktualnego wolumenu " +
                    "rynkowego. Automatycznie dostosowuje tempo realizacji do warunków rynkowych, zachowując " +
                    "dyskrecję i minimalizując wpływ cenowy."
            });

            AvailableStrategies.Add(new AlgoStrategyInfo
            {
                Id = "iceberg",
                Name = "Iceberg",
                Category = "Passive",
                Description = "Ukrywanie wielkości zlecenia - pokazywanie tylko części",
                DetailedDescription = "Strategia Iceberg ukrywa rzeczywistą wielkość zlecenia, pokazując na rynku " +
                    "tylko niewielką część. Po realizacji widocznej części, automatycznie wystawia kolejną, " +
                    "aż do wykonania całości. Minimalizuje wpływ informacyjny na rynek."
            });

            AvailableStrategies.Add(new AlgoStrategyInfo
            {
                Id = "sniper",
                Name = "Sniper",
                Category = "Aggressive",
                Description = "Agresywna realizacja przy sprzyjających warunkach cenowych",
                DetailedDescription = "Strategia Sniper monitoruje rynek i wykonuje zlecenie agresywnie, gdy cena " +
                    "osiągnie zadany poziom lub pojawią się sprzyjające warunki. Idealna do wykorzystania " +
                    "krótkoterminowych okazji rynkowych."
            });

            AvailableStrategies.Add(new AlgoStrategyInfo
            {
                Id = "peg",
                Name = "Pegged",
                Category = "Passive",
                Description = "Śledzenie najlepszych cen - automatyczne dostosowanie",
                DetailedDescription = "Strategia Pegged automatycznie dostosowuje cenę zlecenia, aby pozostawać " +
                    "na czołowej pozycji w księdze zleceń (tuż za/przed najlepszą ofertą). Zapewnia priorytet " +
                    "realizacji przy minimalnym wpływie cenowym."
            });
        }

        #endregion

        #region INotifyPropertyChanged

        public event Action? RequestClose;

        #endregion
    }
}
