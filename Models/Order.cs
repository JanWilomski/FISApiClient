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

        private string _side = string.Empty;
        public string Side
        {
            get => _side;
            set => SetProperty(ref _side, value);
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

        private string _modality = string.Empty;
        public string Modality
        {
            get => _modality;
            set => SetProperty(ref _modality, value);
        }

        private string _validity = string.Empty;
        public string Validity
        {
            get => _validity;
            set => SetProperty(ref _validity, value);
        }

        private string _status = string.Empty;
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
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
                "0" => "Buy",
                "1" => "Sell",
                _ => Side
            };
        }

        /// <summary>
        /// Zwraca czytelny opis modalności
        /// </summary>
        public string GetModalityDescription()
        {
            return Modality switch
            {
                "L" => "Limit",
                "M" => "Market",
                "S" => "Stop",
                "P" => "Pegged",
                _ => Modality
            };
        }

        /// <summary>
        /// Zwraca czytelny opis ważności
        /// </summary>
        public string GetValidityDescription()
        {
            return Validity switch
            {
                "J" => "Day",
                "K" => "FOK",
                "I" => "IOC",
                "G" => "GTC",
                _ => Validity
            };
        }

        /// <summary>
        /// Zwraca czytelny opis statusu
        /// </summary>
        public string GetStatusDescription()
        {
            return Status switch
            {
                "A" => "Accepted",
                "C" => "Rejected",
                "E" => "Executed",
                "P" => "Partially Executed",
                "X" => "Cancelled",
                "M" => "Modified",
                "W" => "Working",
                _ => Status
            };
        }

        #endregion
    }
}
