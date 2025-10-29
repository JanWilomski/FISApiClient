using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FISApiClient.Models;

namespace FISApiClient.Trading.Strategies
{
    /// <summary>
    /// Base interface for all algorithmic trading strategies
    /// </summary>
    public interface IAlgoStrategy
    {
        /// <summary>
        /// Unique identifier for the strategy
        /// </summary>
        string StrategyId { get; }

        /// <summary>
        /// Display name of the strategy
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Current status of the strategy
        /// </summary>
        AlgoStrategyStatus Status { get; }

        /// <summary>
        /// Parameters specific to this strategy
        /// </summary>
        Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Order parameters (common for all strategies)
        /// </summary>
        AlgoOrderParams OrderParams { get; set; }

        /// <summary>
        /// Event fired when strategy status changes
        /// </summary>
        event EventHandler<AlgoStrategyStatus>? StatusChanged;

        /// <summary>
        /// Event fired when an order is about to be sent
        /// </summary>
        event EventHandler<AlgoOrderRequest>? OrderRequested;

        /// <summary>
        /// Event fired when strategy progress updates
        /// </summary>
        event EventHandler<AlgoProgressUpdate>? ProgressUpdated;

        /// <summary>
        /// Initialize the strategy with parameters
        /// </summary>
        void Initialize(Dictionary<string, object> parameters);

        /// <summary>
        /// Validate strategy parameters
        /// </summary>
        bool ValidateParameters(out string errorMessage);

        /// <summary>
        /// Start executing the strategy
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stop the strategy execution
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Pause the strategy execution
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// Resume paused strategy
        /// </summary>
        Task ResumeAsync();

        /// <summary>
        /// Handle market data update (for volume-based strategies)
        /// </summary>
        void OnMarketDataUpdate(InstrumentDetails marketData);
    }

    /// <summary>
    /// Status of algorithmic strategy
    /// </summary>
    public enum AlgoStrategyStatus
    {
        Idle,
        Initializing,
        Running,
        Paused,
        Completed,
        Stopped,
        Error
    }

    /// <summary>
    /// Common order parameters for all strategies
    /// </summary>
    public class AlgoOrderParams
    {
        public Instrument Instrument { get; set; } = null!;
        public OrderSide Side { get; set; }
        public long TotalQuantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public OrderModality OrderType { get; set; }
        
        // FIS specific parameters
        public string ClearingAccount { get; set; } = string.Empty;
        public string ClientCodeType { get; set; } = string.Empty;
        public string AllocationCode { get; set; } = string.Empty;
        public string ClientReference { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to send an order
    /// </summary>
    public class AlgoOrderRequest
    {
        public string LocalCode { get; set; } = string.Empty;
        public string Glid { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public long Quantity { get; set; }
        public OrderModality Modality { get; set; }
        public decimal Price { get; set; }
        public OrderValidity Validity { get; set; }
        public string ClientReference { get; set; } = string.Empty;
        public string ClearingAccount { get; set; } = string.Empty;
        public string ClientCodeType { get; set; } = string.Empty;
        public string AllocationCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Progress update from strategy
    /// </summary>
    public class AlgoProgressUpdate
    {
        public long ExecutedQuantity { get; set; }
        public long RemainingQuantity { get; set; }
        public int OrdersSent { get; set; }
        public int OrdersFilled { get; set; }
        public string Message { get; set; } = string.Empty;
        public double ProgressPercentage { get; set; }
    }
}