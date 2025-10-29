namespace FISApiClient.Models
{
    /// <summary>
    /// Information about an algorithmic trading strategy
    /// </summary>
    public class AlgoStrategyInfo
    {
        /// <summary>
        /// Unique identifier for the strategy
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the strategy
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Category of the strategy (Volume, Time, Passive, Aggressive)
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Short description for the strategy list
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description shown when strategy is selected
        /// </summary>
        public string DetailedDescription { get; set; } = string.Empty;
    }
}