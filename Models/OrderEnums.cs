using System.ComponentModel;

namespace FISApiClient.Models
{
    public enum OrderSide
    {
        [Description("Buy")]
        Buy,
        [Description("Sell")]
        Sell,
        [Description("Unknown")]
        Unknown
    }

    public enum OrderModality
    {
        [Description("Limit (L)")]
        Limit,
        [Description("Market (M)")]
        Market,
        [Description("Stop (S)")]
        Stop,
        [Description("Pegged (P)")]
        Pegged,
        [Description("Unknown")]
        Unknown
    }

    public enum OrderValidity
    {
        [Description("Day (J)")]
        Day,
        [Description("Fill or Kill (K)")]
        FOK,
        [Description("Immediate or Cancel (I)")]
        IOC,
        [Description("Good Till Cancelled (G)")]
        GTC,
        [Description("Unknown")]
        Unknown
    }

    public enum OrderStatus
    {
        [Description("Accepted")]
        Accepted,
        [Description("Rejected")]
        Rejected,
        [Description("Executed")]
        Executed,
        [Description("Partially Executed")]
        PartiallyExecuted,
        [Description("Cancelled")]
        Cancelled,
        [Description("Modified")]
        Modified,
        [Description("Working")]
                Working,
                [Description("Unknown")]
                Unknown
            }
        
                public enum SliceAvailableType
                {
                    [Description("Order cannot manage childs")]
                    No,
                    [Description("Order can manage childs")]
                    Yes
                }
            
                public enum OrderType
                {
                    [Description("Standard")]
                    Standard,
                    [Description("Parent Order for Algo (EDA)")]
                    EDA
                }
            }        