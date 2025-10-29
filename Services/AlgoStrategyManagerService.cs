using System.Collections.ObjectModel;
using System.Linq;
using FISApiClient.Trading.Strategies;

namespace FISApiClient.Services
{
    public class AlgoStrategyManagerService
    {
        private static readonly AlgoStrategyManagerService _instance = new();
        public static AlgoStrategyManagerService Instance => _instance;

        public ObservableCollection<IAlgoStrategy> ActiveStrategies { get; } = new();

        private AlgoStrategyManagerService() { }

        public void Register(IAlgoStrategy strategy)
        {
            if (!ActiveStrategies.Contains(strategy))
            {
                ActiveStrategies.Add(strategy);
                strategy.StatusChanged += OnStrategyStatusChanged;
            }
        }

        public void Unregister(IAlgoStrategy strategy)
        {
            if (ActiveStrategies.Contains(strategy))
            {
                strategy.StatusChanged -= OnStrategyStatusChanged;
                ActiveStrategies.Remove(strategy);
            }
        }

        private void OnStrategyStatusChanged(object? sender, AlgoStrategyStatus status)
        {
            if (sender is IAlgoStrategy strategy)
            {
                if (status == AlgoStrategyStatus.Completed || 
                    status == AlgoStrategyStatus.Stopped || 
                    status == AlgoStrategyStatus.Error)
                {
                    // Ensure removal is done on the UI thread if collection is bound
                    App.Current.Dispatcher.Invoke(() => Unregister(strategy));
                }
            }
        }
    }
}
