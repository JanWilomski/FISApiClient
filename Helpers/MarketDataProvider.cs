using System.Collections.Generic;

namespace FISApiClient.Helpers
{
    public static class MarketDataProvider
    {
        public static List<int> GetExchanges()
        {
            return new List<int> { 40, 330, 331, 332 };
        }

        public static List<int> GetMarkets()
        {
            return new List<int> { 1, 2, 3, 4, 5, 9, 16, 17, 20 };
        }
    }
}