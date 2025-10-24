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

        #region Properties

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

        public long RemainingQuantity => Quantity - ExecutedQuantity;

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set => SetProperty(ref _price, value);
        }

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
                _ => OrderStatus.Unknown
            };
        }

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

        private string _clientReference = string.Empty;
        public string ClientReference
        {
            get => _clientReference;
            set => SetProperty(ref _clientReference, value);
        }

        private string _rejectReason = string.Empty;
        public string RejectReason
        {
            get => _rejectReason;
            set => SetProperty(ref _rejectReason, value);
        }

        private decimal _averagePrice;
        public decimal AveragePrice
        {
            get => _averagePrice;
            set => SetProperty(ref _averagePrice, value);
        }

        #endregion

        #region Helper Methods

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
