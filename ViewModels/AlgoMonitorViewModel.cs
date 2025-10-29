using System.Collections.ObjectModel;
using System.Windows.Input;
using FISApiClient.Helpers;
using FISApiClient.Services;
using FISApiClient.Trading.Strategies;

namespace FISApiClient.ViewModels
{
    public class AlgoMonitorViewModel : ViewModelBase
    {
        private readonly AlgoStrategyManagerService _strategyManager;

        public ObservableCollection<IAlgoStrategy> ActiveStrategies => _strategyManager.ActiveStrategies;

        public ICommand StopStrategyCommand { get; }

        public AlgoMonitorViewModel()
        {
            _strategyManager = AlgoStrategyManagerService.Instance;
            StopStrategyCommand = new RelayCommand(
                async strategy => await ((IAlgoStrategy)strategy!).StopAsync(),
                strategy => strategy is IAlgoStrategy
            );
        }
    }
}
