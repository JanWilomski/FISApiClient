
using FISApiClient.Models;
using FISApiClient.Trading.Strategies;

namespace FISApiClient.Helpers
{
    public class FisOrderParametersProvider
    {
        // In a real app, this would come from config, user settings, etc.
        public string ClientCodeType => "C";
        public string ClearingAccount => "0100";
        public string AllocationCode => "0959";
        public string Memo => "7841";
        public string SecondClientCodeType => "B";
        public string FloorTraderId => "0959";
        public string ClientFreeField1 => "100";
        public string ClientReference => "784";
        public string Currency => "PLN";
        public string ContraFirm => "";
        public OrderValidity DefaultValidity => OrderValidity.Day;

        public void PopulateAlgoOrderParams(AlgoOrderParams orderParams)
        {
            orderParams.ClearingAccount = ClearingAccount;
            orderParams.ClientCodeType = ClientCodeType;
            orderParams.AllocationCode = AllocationCode;
            orderParams.ClientReference = ClientReference;
            orderParams.Memo = Memo;
            orderParams.SecondClientCodeType = SecondClientCodeType;
            orderParams.FloorTraderId = FloorTraderId;
            orderParams.ClientFreeField1 = ClientFreeField1;
            orderParams.Currency = Currency;
            orderParams.Validity = DefaultValidity;
        }
    }
}
