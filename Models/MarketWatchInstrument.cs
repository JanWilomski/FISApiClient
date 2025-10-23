using FISApiClient.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FISApiClient.Models
{
    /// <summary>
    /// Reprezentuje pojedynczy instrument w panelu MarketWatch z real-time danymi
    /// </summary>
    public class MarketWatchInstrument : ViewModelBase
    {
        public string GlidAndSymbol { get; set; } = string.Empty;
        public string Glid { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ISIN { get; set; } = string.Empty;

        #region Real-time Properties

        private decimal _bidPrice;
        private decimal _previousBidPrice;
        
        // Batch update support
        private bool _isBatchUpdate;
        private readonly List<string> _batchPropertyNames = new();

        /// <summary>
        /// Rozpoczyna batch update - zawiesza PropertyChanged notifications
        /// </summary>
        public void BeginBatchUpdate()
        {
            _isBatchUpdate = true;
            _batchPropertyNames.Clear();
        }

        /// <summary>
        /// Kończy batch update - wywołuje wszystkie odroczone PropertyChanged
        /// </summary>
        public void EndBatchUpdate()
        {
            _isBatchUpdate = false;
    
            // Wywołaj wszystkie odroczone notyfikacje
            foreach (var propertyName in _batchPropertyNames.Distinct())
            {
                OnPropertyChanged(propertyName);
            }
    
            _batchPropertyNames.Clear();
        }

        /// <summary>
        /// SetProperty z obsługą batch update
        /// </summary>
        protected new bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
    
            if (_isBatchUpdate && !string.IsNullOrEmpty(propertyName))
            {
                // Odrocz notyfikację
                _batchPropertyNames.Add(propertyName);
            }
            else
            {
                // Standardowa notyfikacja
                OnPropertyChanged(propertyName);
            }
    
            return true;
        }
        public decimal BidPrice
        {
            get => _bidPrice;
            set
            {
                if (_bidPrice != value && value > 0)
                {
                    _previousBidPrice = _bidPrice;
                    if (SetProperty(ref _bidPrice, value))
                    {
                        OnPropertyChanged(nameof(BidPriceFormatted));
                        UpdateSpread();
                        TriggerBidPriceFlash();
                    }
                }
                else if (SetProperty(ref _bidPrice, value))
                {
                    OnPropertyChanged(nameof(BidPriceFormatted));
                    UpdateSpread();
                }
            }
        }

        public string BidPriceFormatted => BidPrice > 0 ? BidPrice.ToString("N2") : "-";

        private string _bidPriceFlashColor = "Transparent";
        public string BidPriceFlashColor
        {
            get => _bidPriceFlashColor;
            set => SetProperty(ref _bidPriceFlashColor, value);
        }

        private long _bidQuantity;
        public long BidQuantity
        {
            get => _bidQuantity;
            set
            {
                if (SetProperty(ref _bidQuantity, value))
                {
                    OnPropertyChanged(nameof(BidQuantityFormatted));
                }
            }
        }

        public string BidQuantityFormatted => BidQuantity > 0 ? BidQuantity.ToString("N0") : "-";

        private decimal _askPrice;
        private decimal _previousAskPrice;
        public decimal AskPrice
        {
            get => _askPrice;
            set
            {
                if (_askPrice != value && value > 0)
                {
                    _previousAskPrice = _askPrice;
                    if (SetProperty(ref _askPrice, value))
                    {
                        OnPropertyChanged(nameof(AskPriceFormatted));
                        UpdateSpread();
                        TriggerAskPriceFlash();
                    }
                }
                else if (SetProperty(ref _askPrice, value))
                {
                    OnPropertyChanged(nameof(AskPriceFormatted));
                    UpdateSpread();
                }
            }
        }

        public string AskPriceFormatted => AskPrice > 0 ? AskPrice.ToString("N2") : "-";

        private string _askPriceFlashColor = "Transparent";
        public string AskPriceFlashColor
        {
            get => _askPriceFlashColor;
            set => SetProperty(ref _askPriceFlashColor, value);
        }

        private long _askQuantity;
        public long AskQuantity
        {
            get => _askQuantity;
            set
            {
                if (SetProperty(ref _askQuantity, value))
                {
                    OnPropertyChanged(nameof(AskQuantityFormatted));
                }
            }
        }

        public string AskQuantityFormatted => AskQuantity > 0 ? AskQuantity.ToString("N0") : "-";

        private decimal _lastPrice;
        private decimal _previousLastPrice;
        public decimal LastPrice
        {
            get => _lastPrice;
            set
            {
                if (_lastPrice != value && value > 0)
                {
                    _previousLastPrice = _lastPrice;
                    if (SetProperty(ref _lastPrice, value))
                    {
                        OnPropertyChanged(nameof(LastPriceFormatted));
                        TriggerLastPriceFlash();
                    }
                }
                else if (SetProperty(ref _lastPrice, value))
                {
                    OnPropertyChanged(nameof(LastPriceFormatted));
                }
            }
        }

        public string LastPriceFormatted => LastPrice > 0 ? LastPrice.ToString("N2") : "-";

        private string _lastPriceFlashColor = "Transparent";
        public string LastPriceFlashColor
        {
            get => _lastPriceFlashColor;
            set => SetProperty(ref _lastPriceFlashColor, value);
        }

        private long _lastQuantity;
        public long LastQuantity
        {
            get => _lastQuantity;
            set
            {
                if (SetProperty(ref _lastQuantity, value))
                {
                    OnPropertyChanged(nameof(LastQuantityFormatted));
                }
            }
        }

        public string LastQuantityFormatted => LastQuantity > 0 ? LastQuantity.ToString("N0") : "-";

        private string _lastTradeTime = string.Empty;
        public string LastTradeTime
        {
            get => _lastTradeTime;
            set => SetProperty(ref _lastTradeTime, value);
        }

        private long _volume;
        public long Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    OnPropertyChanged(nameof(VolumeFormatted));
                }
            }
        }

        public string VolumeFormatted => Volume > 0 ? Volume.ToString("N0") : "-";

        private decimal _percentageVariation;
        public decimal PercentageVariation
        {
            get => _percentageVariation;
            set
            {
                if (SetProperty(ref _percentageVariation, value))
                {
                    OnPropertyChanged(nameof(PercentageVariationFormatted));
                }
            }
        }

        public string PercentageVariationFormatted
        {
            get
            {
                if (PercentageVariation == 0) return "-";
                string sign = VariationSign == "+" ? "+" : VariationSign == "-" ? "−" : "";
                return $"{sign}{Math.Abs(PercentageVariation):N2}%";
            }
        }

        private string _variationSign = string.Empty;
        public string VariationSign
        {
            get => _variationSign;
            set
            {
                if (SetProperty(ref _variationSign, value))
                {
                    OnPropertyChanged(nameof(PercentageVariationFormatted));
                    OnPropertyChanged(nameof(VariationColor));
                }
            }
        }

        public string VariationColor
        {
            get
            {
                if (VariationSign == "+") return "#4CAF50"; // Zielony
                if (VariationSign == "-") return "#F44336"; // Czerwony
                return "#666666"; // Szary
            }
        }

        private decimal _openPrice;
        public decimal OpenPrice
        {
            get => _openPrice;
            set
            {
                if (SetProperty(ref _openPrice, value))
                {
                    OnPropertyChanged(nameof(OpenPriceFormatted));
                }
            }
        }

        public string OpenPriceFormatted => OpenPrice > 0 ? OpenPrice.ToString("N2") : "-";

        private decimal _highPrice;
        public decimal HighPrice
        {
            get => _highPrice;
            set
            {
                if (SetProperty(ref _highPrice, value))
                {
                    OnPropertyChanged(nameof(HighPriceFormatted));
                }
            }
        }

        public string HighPriceFormatted => HighPrice > 0 ? HighPrice.ToString("N2") : "-";

        private decimal _lowPrice;
        public decimal LowPrice
        {
            get => _lowPrice;
            set
            {
                if (SetProperty(ref _lowPrice, value))
                {
                    OnPropertyChanged(nameof(LowPriceFormatted));
                }
            }
        }

        public string LowPriceFormatted => LowPrice > 0 ? LowPrice.ToString("N2") : "-";

        private decimal _closePrice;
        public decimal ClosePrice
        {
            get => _closePrice;
            set
            {
                if (SetProperty(ref _closePrice, value))
                {
                    OnPropertyChanged(nameof(ClosePriceFormatted));
                }
            }
        }

        public string ClosePriceFormatted => ClosePrice > 0 ? ClosePrice.ToString("N2") : "-";

        private string _spread = "-";
        public string Spread
        {
            get => _spread;
            set => SetProperty(ref _spread, value);
        }

        private string _tradingPhase = "-";
        public string TradingPhase
        {
            get => _tradingPhase;
            set => SetProperty(ref _tradingPhase, value);
        }

        private DateTime _lastUpdateTime = DateTime.Now;
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set
            {
                if (SetProperty(ref _lastUpdateTime, value))
                {
                    OnPropertyChanged(nameof(LastUpdateTimeFormatted));
                }
            }
        }

        public string LastUpdateTimeFormatted => LastUpdateTime.ToString("HH:mm:ss");

        #endregion

        /// <summary>
        /// Aktualizuje dane instrumentu na podstawie InstrumentDetails
        /// </summary>
        public void UpdateFromDetails(InstrumentDetails details)
        {
            BidPrice = details.BidPrice;
            BidQuantity = details.BidQuantity;
            AskPrice = details.AskPrice;
            AskQuantity = details.AskQuantity;
            LastPrice = details.LastPrice;
            LastQuantity = details.LastQuantity;
            LastTradeTime = details.LastTradeTime;
            Volume = details.Volume;
            PercentageVariation = details.PercentageVariation;
            VariationSign = details.VariationSign;
            OpenPrice = details.OpenPrice;
            HighPrice = details.HighPrice;
            LowPrice = details.LowPrice;
            ClosePrice = details.ClosePrice;
            TradingPhase = details.TradingPhase;
            LastUpdateTime = DateTime.Now;

            UpdateSpread();
        }

        /// <summary>
        /// Wyzwala efekt flash dla ceny Bid
        /// </summary>
        private void TriggerBidPriceFlash()
        {
            if (_previousBidPrice == 0) return; // Pierwsza wartość, nie flashuj

            // Określ kolor: zielony jeśli wzrosła, czerwony jeśli spadła
            if (BidPrice > _previousBidPrice)
            {
                BidPriceFlashColor = "#4CAF4F"; // Light green
            }
            else if (BidPrice < _previousBidPrice)
            {
                BidPriceFlashColor = "#FA1302"; // Light red/pink
            }

            // Reset koloru po 100ms
            var timer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    BidPriceFlashColor = "Transparent";
                });
            }, null, 100, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Wyzwala efekt flash dla ceny Ask
        /// </summary>
        private void TriggerAskPriceFlash()
        {
            if (_previousAskPrice == 0) return; // Pierwsza wartość, nie flashuj

            // Określ kolor: zielony jeśli wzrosła, czerwony jeśli spadła
            if (AskPrice > _previousAskPrice)
            {
                AskPriceFlashColor = "#4CAF4F"; // green
            }
            else if (AskPrice < _previousAskPrice)
            {
                AskPriceFlashColor = "#FA1302"; // red
            }

            // Reset koloru po 100ms
            var timer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    AskPriceFlashColor = "Transparent";
                });
            }, null, 100, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Wyzwala efekt flash dla Last Price
        /// </summary>
        private void TriggerLastPriceFlash()
        {
            if (_previousLastPrice == 0) return; // Pierwsza wartość, nie flashuj

            // Określ kolor: zielony jeśli wzrosła, czerwony jeśli spadła
            if (LastPrice > _previousLastPrice)
            {
                LastPriceFlashColor = "#4CAF4F"; // green
            }
            else if (LastPrice < _previousLastPrice)
            {
                LastPriceFlashColor = "#FA1302"; // red
            }

            // Reset koloru po 400ms
            var timer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    LastPriceFlashColor = "Transparent";
                });
            }, null, 400, System.Threading.Timeout.Infinite);
        }

        private void UpdateSpread()
        {
            if (AskPrice > 0 && BidPrice > 0)
            {
                decimal spreadValue = AskPrice - BidPrice;
                decimal spreadPercent = (spreadValue / BidPrice) * 100;
                Spread = $"{spreadValue:N2} ({spreadPercent:N2}%)";
            }
            else
            {
                Spread = "-";
            }
        }
    }
}