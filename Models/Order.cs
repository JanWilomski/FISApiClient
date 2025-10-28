using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FISApiClient.Models
{
    /// <summary>
    /// Reprezentuje pojedyncze zlecenie w Order Book
    /// </summary>
    public class Order : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region Basic Properties

        private string _orderId = string.Empty;
        public string OrderId
        {
            get => _orderId;
            set => SetProperty(ref _orderId, value);
        }

        private string _sleReference = string.Empty;
        public string SleReference
        {
            get => _sleReference;
            set => SetProperty(ref _sleReference, value);
        }
        
        private string _localCode = string.Empty;
        public string LocalCode
        {
            get => _localCode;
            set => SetProperty(ref _localCode, value);
        }

        private string _instrument = string.Empty;
        public string Instrument
        {
            get => _instrument;
            set => SetProperty(ref _instrument, value);
        }

        private OrderSide _side = OrderSide.Unknown;
        public OrderSide Side
        {
            get => _side;
            set => SetProperty(ref _side, value);
        }

        public void SetSide(string value)
        {
            Side = value switch
            {
                "0" => OrderSide.Buy,
                "1" => OrderSide.Sell,
                _ => OrderSide.Unknown
            };
        }

        #endregion

        #region Quantity Properties

        private long _quantity;
        public long Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private long _executedQuantity;
        public long ExecutedQuantity
        {
            get => _executedQuantity;
            set
            {
                if (SetProperty(ref _executedQuantity, value))
                {
                    OnPropertyChanged(nameof(RemainingQuantity));
                }
            }
        }

        private long _remainingQuantity;
        public long RemainingQuantity
        {
            get => _remainingQuantity;
            set => SetProperty(ref _remainingQuantity, value);
        }

        private long _displayedQuantity;
        public long DisplayedQuantity
        {
            get => _displayedQuantity;
            set => SetProperty(ref _displayedQuantity, value);
        }

        private long _minimumQuantity;
        public long MinimumQuantity
        {
            get => _minimumQuantity;
            set => SetProperty(ref _minimumQuantity, value);
        }

        #endregion

        #region Price Properties

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set => SetProperty(ref _price, value);
        }

        private decimal _executionPrice;
        public decimal ExecutionPrice
        {
            get => _executionPrice;
            set => SetProperty(ref _executionPrice, value);
        }

        private decimal _averagePrice;
        public decimal AveragePrice
        {
            get => _averagePrice;
            set => SetProperty(ref _averagePrice, value);
        }

        #endregion

        #region Order Type Properties

        private OrderModality _modality = OrderModality.Unknown;
        public OrderModality Modality
        {
            get => _modality;
            set => SetProperty(ref _modality, value);
        }

        public void SetModality(string value)
        {
            Modality = value switch
            {
                "L" => OrderModality.Limit,
                "M" => OrderModality.Market,
                "S" => OrderModality.Stop,
                "P" => OrderModality.Pegged,
                _ => OrderModality.Unknown
            };
        }

        private OrderValidity _validity = OrderValidity.Unknown;
        public OrderValidity Validity
        {
            get => _validity;
            set => SetProperty(ref _validity, value);
        }

        public void SetValidity(string value)
        {
            Validity = value switch
            {
                "J" => OrderValidity.Day,
                "K" => OrderValidity.FOK,
                "I" => OrderValidity.IOC,
                "G" => OrderValidity.GTC,
                _ => OrderValidity.Unknown
            };
        }

        #endregion

        #region Status Properties

        private OrderStatus _status = OrderStatus.Unknown;
        public OrderStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public void SetStatus(string value)
        {
            Status = value switch
            {
                "A" => OrderStatus.Accepted,
                "C" => OrderStatus.Rejected,
                "E" => OrderStatus.Executed,
                "P" => OrderStatus.PartiallyExecuted,
                "X" => OrderStatus.Cancelled,
                "M" => OrderStatus.Modified,
                "W" => OrderStatus.Working,
                "O" => OrderStatus.Accepted, // "O" = Acknowledged
                "R" => OrderStatus.Rejected,
                "T" => OrderStatus.Executed, // "T X" = Totally executed
                "L" => OrderStatus.PartiallyExecuted, // "L X" = Partially executed and eliminated
                "N" => OrderStatus.Modified, // "N" = In modification
                _ => OrderStatus.Unknown
            };
        }

        #endregion

        #region Time Properties

        private DateTime _orderTime;
        public DateTime OrderTime
        {
            get => _orderTime;
            set => SetProperty(ref _orderTime, value);
        }

        private DateTime? _lastUpdateTime;
        public DateTime? LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }

        private DateTime? _tradeTime;
        public DateTime? TradeTime
        {
            get => _tradeTime;
            set => SetProperty(ref _tradeTime, value);
        }

        private DateTime? _rejectTime;
        public DateTime? RejectTime
        {
            get => _rejectTime;
            set => SetProperty(ref _rejectTime, value);
        }

        #endregion

        #region Exchange Properties

        private string _exchangeNumber = string.Empty;
        public string ExchangeNumber
        {
            get => _exchangeNumber;
            set => SetProperty(ref _exchangeNumber, value);
        }

        private string _tradeNumber = string.Empty;
        public string TradeNumber
        {
            get => _tradeNumber;
            set => SetProperty(ref _tradeNumber, value);
        }

        private int _numberOfExecutions;
        public int NumberOfExecutions
        {
            get => _numberOfExecutions;
            set => SetProperty(ref _numberOfExecutions, value);
        }

        #endregion

        #region Client Properties

        private string _clientReference = string.Empty;
        public string ClientReference
        {
            get => _clientReference;
            set => SetProperty(ref _clientReference, value);
        }

        private string _internalReference = string.Empty;
        public string InternalReference
        {
            get => _internalReference;
            set => SetProperty(ref _internalReference, value);
        }

        private string _clientCodeType = string.Empty;
        public string ClientCodeType
        {
            get => _clientCodeType;
            set => SetProperty(ref _clientCodeType, value);
        }

        private string _allocationCode = string.Empty;
        public string AllocationCode
        {
            get => _allocationCode;
            set => SetProperty(ref _allocationCode, value);
        }

        private string _clearingAccount = string.Empty;
        public string ClearingAccount
        {
            get => _clearingAccount;
            set => SetProperty(ref _clearingAccount, value);
        }

        private string _memo = string.Empty;
        public string Memo
        {
            get => _memo;
            set => SetProperty(ref _memo, value);
        }

        #endregion

        #region Reject Properties

        private string _rejectReason = string.Empty;
        public string RejectReason
        {
            get => _rejectReason;
            set => SetProperty(ref _rejectReason, value);
        }

        private string _rejectType = string.Empty;
        public string RejectType
        {
            get => _rejectType;
            set => SetProperty(ref _rejectType, value);
        }

        private string _rejectedCommandType = string.Empty;
        public string RejectedCommandType
        {
            get => _rejectedCommandType;
            set => SetProperty(ref _rejectedCommandType, value);
        }

        #endregion

        #region Additional Properties

        private string _glid = string.Empty;
        public string GLID
        {
            get => _glid;
            set => SetProperty(ref _glid, value);
        }

        private string _currency = string.Empty;
        public string Currency
        {
            get => _currency;
            set => SetProperty(ref _currency, value);
        }

        private string _floorTraderId = string.Empty;
        public string FloorTraderId
        {
            get => _floorTraderId;
            set => SetProperty(ref _floorTraderId, value);
        }

        private string _tradeType = string.Empty;
        public string TradeType
        {
            get => _tradeType;
            set => SetProperty(ref _tradeType, value);
        }

        private string _acknowledgementType = string.Empty;
        public string AcknowledgementType
        {
            get => _acknowledgementType;
            set => SetProperty(ref _acknowledgementType, value);
        }

        private string _sleIndex = string.Empty;
        public string SleIndex
        {
            get => _sleIndex;
            set => SetProperty(ref _sleIndex, value);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parsuje datę w formacie YYYYMMDDHHMMSS
        /// </summary>
        public static DateTime? ParseOrderDateTime(string dateTimeStr)
        {
            if (string.IsNullOrWhiteSpace(dateTimeStr) || dateTimeStr.Length != 14)
                return null;

            try
            {
                int year = int.Parse(dateTimeStr.Substring(0, 4));
                int month = int.Parse(dateTimeStr.Substring(4, 2));
                int day = int.Parse(dateTimeStr.Substring(6, 2));
                int hour = int.Parse(dateTimeStr.Substring(8, 2));
                int minute = int.Parse(dateTimeStr.Substring(10, 2));
                int second = int.Parse(dateTimeStr.Substring(12, 2));

                return new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Zwraca czytelny opis strony (Buy/Sell)
        /// </summary>
        public string GetSideDescription()
        {
            return Side switch
            {
                OrderSide.Buy => "Buy",
                OrderSide.Sell => "Sell",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Zwraca czytelny opis modalności
        /// </summary>
        public string GetModalityDescription()
        {
            return Modality switch
            {
                OrderModality.Limit => "Limit",
                OrderModality.Market => "Market",
                OrderModality.Stop => "Stop",
                OrderModality.Pegged => "Pegged",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Zwraca czytelny opis ważności
        /// </summary>
        public string GetValidityDescription()
        {
            return Validity switch
            {
                OrderValidity.Day => "Day",
                OrderValidity.FOK => "FOK",
                OrderValidity.IOC => "IOC",
                OrderValidity.GTC => "GTC",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Zwraca czytelny opis statusu
        /// </summary>
        public string GetStatusDescription()
        {
            return Status switch
            {
                OrderStatus.Accepted => "Accepted",
                OrderStatus.Rejected => "Rejected",
                OrderStatus.Executed => "Executed",
                OrderStatus.PartiallyExecuted => "Partially Executed",
                OrderStatus.Cancelled => "Cancelled",
                OrderStatus.Modified => "Modified",
                OrderStatus.Working => "Working",
                _ => "Unknown"
            };
        }

        #endregion
    }
}