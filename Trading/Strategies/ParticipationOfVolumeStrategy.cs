using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.Trading.Strategies
{
    public class ParticipationOfVolumeStrategy : ViewModelBase, IAlgoStrategy
    {
        public string StrategyId => "pov_participation";
        public string Name => "Participation of Volume";
        
        private AlgoStrategyStatus _status;
        public AlgoStrategyStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChangedOnUIThread(nameof(IsRunning));
                    StatusChanged?.Invoke(this, value);
                }
            }
        }

        public bool IsRunning => Status == AlgoStrategyStatus.Running;

        private DateTime? _startTime;
        public DateTime? StartTime
        {
            get => _startTime;
            private set => SetProperty(ref _startTime, value);
        }

        private DateTime? _lastUpdateTime;
        public DateTime? LastUpdateTime
        {
            get => _lastUpdateTime;
            private set => SetProperty(ref _lastUpdateTime, value);
        }

        private int _totalTrades;
        public int TotalTrades
        {
            get => _totalTrades;
            private set => SetProperty(ref _totalTrades, value);
        }

        private decimal _currentPnL;
        public decimal CurrentPnL
        {
            get => _currentPnL;
            private set => SetProperty(ref _currentPnL, value);
        }

        private string _progress = string.Empty;
        public string Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, value);
        }

        private string _detailedStatus = string.Empty;
        public string DetailedStatus
        {
            get => _detailedStatus;
            private set => SetProperty(ref _detailedStatus, value);
        }

        public Dictionary<string, object> Parameters { get; private set; } = new();
        public AlgoOrderParams OrderParams { get; set; }

        public event EventHandler<AlgoStrategyStatus>? StatusChanged;
        public event EventHandler<AlgoOrderRequest>? OrderRequested;
        public event EventHandler<AlgoProgressUpdate>? ProgressUpdated;

        private readonly SleConnectionService _sleService;
        private readonly MdsConnectionService _mdsService;
        private CancellationTokenSource? _cts;

        private string? _parentOrderId;
        private readonly object _parentOrderLock = new object();
        private bool _isParentOrderAccepted = false;

        private long _initialVolume = -1;
        private long _totalExecutedQuantity = 0;
        private double _participationRate;

        public ParticipationOfVolumeStrategy(SleConnectionService sleService, MdsConnectionService mdsService, AlgoOrderParams orderParams)
        {
            _sleService = sleService;
            _mdsService = mdsService;
            OrderParams = orderParams;
            Status = AlgoStrategyStatus.Idle;
            DetailedStatus = "Ready";
        }

        public void Initialize(Dictionary<string, object> parameters)
        {
            Parameters = parameters;
            if (parameters.TryGetValue("ParticipationRate", out var rate) && rate is double participationRate)
            {
                _participationRate = participationRate / 100.0; // Convert percentage to a decimal
            }
        }

        public bool ValidateParameters(out string errorMessage)
        {
            if (_participationRate <= 0 || _participationRate > 1)
            {
                errorMessage = "Participation Rate must be between 0 and 100.";
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            StartTime = DateTime.Now;
            Status = AlgoStrategyStatus.Running;
            DetailedStatus = "Running...";
            TotalTrades = 0;
            CurrentPnL = 0;
            Progress = "0%";
            _totalExecutedQuantity = 0;
            _initialVolume = -1; // Reset initial volume

            _mdsService.InstrumentDetailsReceived += OnMarketDataUpdate;
            // Request initial details to get the starting volume
            _mdsService.RequestInstrumentDetails(OrderParams.Instrument.Glid + OrderParams.Instrument.Symbol);

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (Status == AlgoStrategyStatus.Stopped || Status == AlgoStrategyStatus.Completed || Status == AlgoStrategyStatus.Error) 
                return Task.CompletedTask;

            Status = AlgoStrategyStatus.Stopped;
            DetailedStatus = "Stopped by user.";
            _mdsService.InstrumentDetailsReceived -= OnMarketDataUpdate;
            _cts?.Cancel();
            _cts?.Dispose();
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            if (Status != AlgoStrategyStatus.Running) return Task.CompletedTask;

            Status = AlgoStrategyStatus.Paused;
            DetailedStatus = "Paused by user.";
            _mdsService.InstrumentDetailsReceived -= OnMarketDataUpdate;
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            if (Status != AlgoStrategyStatus.Paused) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            Status = AlgoStrategyStatus.Running;
            DetailedStatus = "Resuming...";
            _mdsService.InstrumentDetailsReceived += OnMarketDataUpdate;
            // Request initial details to get the current market volume
            _mdsService.RequestInstrumentDetails(OrderParams.Instrument.Glid + OrderParams.Instrument.Symbol);

            return Task.CompletedTask;
        }

        public void OnMarketDataUpdate(InstrumentDetails marketData)
        {
            if (Status != AlgoStrategyStatus.Running) return;
            if (marketData.GlidAndSymbol != OrderParams.Instrument.Glid + OrderParams.Instrument.Symbol) return;

            LastUpdateTime = DateTime.Now;

            if (_initialVolume == -1)
            {
                _initialVolume = marketData.Volume;
                Debug.WriteLine($"[{Name}] Initial volume set to: {_initialVolume}");
                DetailedStatus = $"Initial volume captured: {_initialVolume}";
                return;
            }

            // *** POPRAWKA: Odejmij własne transakcje od total volume aby uzyskać czysty volume rynku ***
            // Problem: marketData.Volume zawiera WSZYSTKIE transakcje, włączając nasze własne
            // Rozwiązanie: Odejmujemy _totalExecutedQuantity aby uzyskać tylko zewnętrzny volume rynku
            long actualMarketVolume = marketData.Volume - _totalExecutedQuantity;
            long marketVolumeChange = actualMarketVolume - _initialVolume;
            
            Debug.WriteLine($"[{Name}] Total Volume: {marketData.Volume}, Our Volume: {_totalExecutedQuantity}, Market Volume: {actualMarketVolume}, Change: {marketVolumeChange}");
            
            if (marketVolumeChange <= 0) return;

            // Oblicz target volume strategii według wzoru POV
            // Formuła: targetAlgoVolume = marketVolume * (participationRate / (1 - participationRate))
            // Przykład: przy 20% participation rate: targetAlgo = marketVolume * (0.2 / 0.8) = marketVolume * 0.25
            long targetAlgoVolume = (long)(marketVolumeChange * (_participationRate / (1 - _participationRate)));
            long neededVolume = targetAlgoVolume - _totalExecutedQuantity;

            Debug.WriteLine($"[{Name}] Target Algo Volume: {targetAlgoVolume}, Needed: {neededVolume}");

            if (neededVolume <= 0) return;

            long lastTradeQuantity = marketData.LastQuantity;
            if (lastTradeQuantity <= 0) return; // Don't react to updates without a trade

            // Oblicz quantity do zlecenia - minimum z potrzebnego volume i ostatniej transakcji
            long orderQuantity = Math.Min(neededVolume, lastTradeQuantity);
            orderQuantity = Math.Min(orderQuantity, OrderParams.TotalQuantity - _totalExecutedQuantity);

            if (orderQuantity <= 0) return;

            Debug.WriteLine($"[{Name}] Sending order for {orderQuantity} shares (LastTradeQty: {lastTradeQuantity})");

            // Strategia zawsze wysyła Market orders aby zapewnić realizację
            var orderRequest = new AlgoOrderRequest
            {
                ParentOrderId = _parentOrderId, // Link to the parent order
                LocalCode = OrderParams.Instrument.LocalCode,
                Glid = OrderParams.Instrument.Glid,
                Side = OrderParams.Side,
                Quantity = orderQuantity,
                Modality = OrderModality.Market,
                Price = 0, // Price must be 0 for Market orders
                Validity = OrderValidity.IOC, // Always use IOC for this strategy's market orders

                // Use parameters from the common order params
                ClientReference = OrderParams.ClientReference,
                ClearingAccount = OrderParams.ClearingAccount,
                ClientCodeType = OrderParams.ClientCodeType,
                AllocationCode = OrderParams.AllocationCode,
                Memo = OrderParams.Memo,
                SecondClientCodeType = OrderParams.SecondClientCodeType,
                FloorTraderId = OrderParams.FloorTraderId,
                ClientFreeField1 = OrderParams.ClientFreeField1,
                Currency = OrderParams.Currency
            };

            _ = Task.Run(() => OrderRequested?.Invoke(this, orderRequest));

            _totalExecutedQuantity += orderQuantity;
            TotalTrades++;
            Progress = $"{(_totalExecutedQuantity * 100.0 / OrderParams.TotalQuantity):F2}%";
            DetailedStatus = $"Sent order for {orderQuantity} shares. Total executed: {_totalExecutedQuantity}";
            
            _ = Task.Run(() => ProgressUpdated?.Invoke(this, new AlgoProgressUpdate
            {
                ExecutedQuantity = _totalExecutedQuantity,
                RemainingQuantity = OrderParams.TotalQuantity - _totalExecutedQuantity,
                Message = DetailedStatus,
                ProgressPercentage = _totalExecutedQuantity * 100.0 / OrderParams.TotalQuantity
            }));

            if (_totalExecutedQuantity >= OrderParams.TotalQuantity)
            {
                Status = AlgoStrategyStatus.Completed;
                DetailedStatus = "Strategy completed.";
                StopAsync();
            }
        }

        public async Task StartWithParentOrderAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Status = AlgoStrategyStatus.Initializing;
            DetailedStatus = "Sending parent order...";

            _sleService.ParentOrderAccepted += OnParentOrderAccepted;
            _sleService.ParentOrderRejected += OnParentOrderRejected;

            bool success = await _sleService.SendOrderAsync(
                localCode: OrderParams.Instrument.LocalCode,
                glid: OrderParams.Instrument.Glid,
                side: OrderParams.Side,
                quantity: OrderParams.TotalQuantity,
                modality: OrderParams.OrderType,
                price: OrderParams.LimitPrice ?? 0,
                validity: OrderParams.Validity,
                clientReference: OrderParams.ClientReference,
                internalReference: "",
                clientCodeType: OrderParams.ClientCodeType,
                clearingAccount: OrderParams.ClearingAccount,
                allocationCode: OrderParams.AllocationCode,
                memo: OrderParams.Memo,
                secondClientCodeType: OrderParams.SecondClientCodeType,
                floorTraderId: OrderParams.FloorTraderId,
                clientFreeField1: OrderParams.ClientFreeField1,
                currency: OrderParams.Currency,
                sliceAvailable: SliceAvailableType.Yes, // This marks it as a parent order
                workTactic: null,
                workPercentVolume: null
            );

            if (!success)
            {
                Status = AlgoStrategyStatus.Error;
                DetailedStatus = "Failed to send parent order.";
                _sleService.ParentOrderAccepted -= OnParentOrderAccepted;
                _sleService.ParentOrderRejected -= OnParentOrderRejected;
            }
        }

        private void OnParentOrderAccepted(string parentOrderId)
        {
            lock (_parentOrderLock)
            {
                _parentOrderId = parentOrderId;
                _isParentOrderAccepted = true;
                Status = AlgoStrategyStatus.Running;
                DetailedStatus = $"Parent order accepted (ID: {parentOrderId}). Monitoring market...";

                // Now that parent is accepted, start listening to market data
                _mdsService.InstrumentDetailsReceived += OnMarketDataUpdate;
                _mdsService.RequestInstrumentDetails(OrderParams.Instrument.Glid + OrderParams.Instrument.Symbol);
            }
        }

        private void OnParentOrderRejected(string clientOrderId, string reason)
        {
            lock (_parentOrderLock)
            {
                Status = AlgoStrategyStatus.Error;
                DetailedStatus = $"Parent order rejected: {reason}";
                _sleService.ParentOrderAccepted -= OnParentOrderAccepted;
                _sleService.ParentOrderRejected -= OnParentOrderRejected;
            }
        }
    }
}