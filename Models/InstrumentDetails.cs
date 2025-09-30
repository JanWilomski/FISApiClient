namespace Cross_FIS_API_1._2.Models
{
    public class InstrumentDetails
    {
        public string GlidAndSymbol { get; set; } = string.Empty;
        public long BidQuantity { get; set; }
        public decimal BidPrice { get; set; }
        public decimal AskPrice { get; set; }
        public long AskQuantity { get; set; }
        public decimal LastPrice { get; set; }
        public long LastQuantity { get; set; }
        public string LastTradeTime { get; set; } = string.Empty;
        public decimal PercentageVariation { get; set; }
        public long Volume { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public string SuspensionIndicator { get; set; } = string.Empty;
        public string VariationSign { get; set; } = string.Empty;
        public decimal ClosePrice { get; set; }
        public string ISIN { get; set; } = string.Empty;
        public string TradingPhase { get; set; } = string.Empty;
    }
}
