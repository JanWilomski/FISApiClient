using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FISApiClient.Models;

namespace FISApiClient.Trading.Strategies
{
    public class ParticipationOfVolumeStrategy : IAlgoStrategy
    {
        public string StrategyId => "pov_participation";
        public string Name => "Participation of Volume";
        public AlgoStrategyStatus Status { get; private set; }
        public Dictionary<string, object> Parameters { get; private set; } = new();
        public AlgoOrderParams OrderParams { get; set; }

        public event EventHandler<AlgoStrategyStatus>? StatusChanged;
        public event EventHandler<AlgoOrderRequest>? OrderRequested;
        public event EventHandler<AlgoProgressUpdate>? ProgressUpdated;

        private readonly SleConnectionService _sleService;
        private readonly MdsConnectionService _mdsService;
        private CancellationTokenSource? _cts;

        private long _initialVolume = -1;
        private long _totalExecutedQuantity = 0;
        private double _participationRate;

        public ParticipationOfVolumeStrategy(SleConnectionService sleService, MdsConnectionService mdsService, AlgoOrderParams orderParams)
        {
            _sleService = sleService;
            _mdsService = mdsService;
            OrderParams = orderParams;
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
            Status = AlgoStrategyStatus.Running;
            StatusChanged?.Invoke(this, Status);

            _mdsService.InstrumentDetailsReceived += OnMarketDataUpdate;
            // Request initial details to get the starting volume
            _mdsService.RequestInstrumentDetails(OrderParams.Instrument.Glid + OrderParams.Instrument.Symbol);

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Status = AlgoStrategyStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            _mdsService.InstrumentDetailsReceived -= OnMarketDataUpdate;
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public void OnMarketDataUpdate(InstrumentDetails marketData)
        {
            if (Status != AlgoStrategyStatus.Running) return;
            if (marketData.GlidAndSymbol != OrderParams.Instrument.Glid + OrderParams.Instrument.Symbol) return;

            if (_initialVolume == -1)
            {
                _initialVolume = marketData.Volume;
                Debug.WriteLine($"[{Name}] Initial volume set to: {_initialVolume}");
                return;
            }

            long marketVolumeChange = marketData.Volume - _initialVolume;
            if (marketVolumeChange <= 0) return;

            long targetAlgoVolume = (long)(marketVolumeChange * (_participationRate / (1 - _participationRate)));
            long neededVolume = targetAlgoVolume - _totalExecutedQuantity;

            if (neededVolume <= 0) return;

            long lastTradeQuantity = marketData.LastQuantity;
            if (lastTradeQuantity <= 0) return; // Don't react to updates without a trade

            long orderQuantity = Math.Min(neededVolume, lastTradeQuantity);
            orderQuantity = Math.Min(orderQuantity, OrderParams.TotalQuantity - _totalExecutedQuantity); // Don't exceed total order quantity

            if (orderQuantity <= 0) return;

            // This strategy always sends Market orders to ensure participation.
            var orderRequest = new AlgoOrderRequest
            {
                LocalCode = OrderParams.Instrument.LocalCode,
                Glid = OrderParams.Instrument.Glid,
                Side = OrderParams.Side,
                Quantity = orderQuantity,
                Modality = OrderModality.Market, // Always Market
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

            OrderRequested?.Invoke(this, orderRequest);

            _totalExecutedQuantity += orderQuantity;
            
            ProgressUpdated?.Invoke(this, new AlgoProgressUpdate
            {
                ExecutedQuantity = _totalExecutedQuantity,
                RemainingQuantity = OrderParams.TotalQuantity - _totalExecutedQuantity,
                Message = $"Sent order for {orderQuantity} shares."
            });

            if (_totalExecutedQuantity >= OrderParams.TotalQuantity)
            {
                Status = AlgoStrategyStatus.Completed;
                StatusChanged?.Invoke(this, Status);
                StopAsync();
            }
        }

        // Unimplemented methods
        public Task PauseAsync() => Task.CompletedTask;
        public Task ResumeAsync() => Task.CompletedTask;
    }
}
