using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class AlgoStrategyViewModel : INotifyPropertyChanged
    {
        private readonly Instrument _instrument;
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;

        private AlgoStrategyInfo? _selectedStrategy;
        private string _side = "Buy";
        private string _totalQuantity = "100";
        private string _limitPrice = "";
        private string _orderType = "Limit";

        public AlgoStrategyViewModel(Instrument instrument, MdsConnectionService mdsService, SleConnectionService sleService)
        {
            _instrument = instrument;
            _mdsService = mdsService;
            _sleService = sleService;

            InitializeAvailableStrategies();
            InitializeCommands();

            // Leave price empty - user will enter it manually
            _limitPrice = "";
        }

        #region Properties

        public ObservableCollection<AlgoStrategyInfo> AvailableStrategies { get; } = new();

        public AlgoStrategyInfo? SelectedStrategy
        {
            get => _selectedStrategy;
            set
            {
                if (_selectedStrategy != value)
                {
                    _selectedStrategy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedStrategy));
                    OnPropertyChanged(nameof(HasNoSelectedStrategy));
                    OnPropertyChanged(nameof(CanStartStrategy));
                }
            }
        }

        public bool HasSelectedStrategy => SelectedStrategy != null;
        public bool HasNoSelectedStrategy => SelectedStrategy == null;
        public bool CanStartStrategy => SelectedStrategy != null;

        public string InstrumentInfo => $"{_instrument.Symbol} ({_instrument.Name}) - ISIN: {_instrument.ISIN}";

        public string Side
        {
            get => _side;
            set
            {
                if (_side != value)
                {
                    _side = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TotalQuantity
        {
            get => _totalQuantity;
            set
            {
                if (_totalQuantity != value)
                {
                    _totalQuantity = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LimitPrice
        {
            get => _limitPrice;
            set
            {
                if (_limitPrice != value)
                {
                    _limitPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OrderType
        {
            get => _orderType;
            set
            {
                if (_orderType != value)
                {
                    _orderType = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand StartStrategyCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private void InitializeCommands()
        {
            StartStrategyCommand = new RelayCommand(ExecuteStartStrategy, CanExecuteStartStrategy);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private bool CanExecuteStartStrategy(object? parameter)
        {
            return SelectedStrategy != null;
        }

        private void ExecuteStartStrategy(object? parameter)
        {
            if (SelectedStrategy == null)
                return;

            // TODO: Implement strategy execution
            // For now, just show a message
            System.Windows.MessageBox.Show(
                $"Uruchamianie strategii: {SelectedStrategy.Name}\n\n" +
                $"Instrument: {_instrument.Symbol}\n" +
                $"Kierunek: {Side}\n" +
                $"Całkowita ilość: {TotalQuantity}\n" +
                $"Cena limit: {LimitPrice}\n\n" +
                $"Funkcjonalność będzie dostępna po implementacji strategii.",
                "Strategia Algo",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            RequestClose?.Invoke();
        }

        private void ExecuteCancel(object? parameter)
        {
            RequestClose?.Invoke();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? RequestClose;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}