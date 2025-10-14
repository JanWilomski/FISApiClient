namespace FISApiClient.Models
{
    public class Instrument
    {
        public string Glid { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LocalCode { get; set; } = string.Empty;
        public string ISIN { get; set; } = string.Empty;
    }
}
