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
        public ICommand PauseStrategyCommand { get; }
        public ICommand ResumeStrategyCommand { get; }

        public AlgoMonitorViewModel()
        {
            _strategyManager = AlgoStrategyManagerService.Instance;
            StopStrategyCommand = new RelayCommand(
                async strategy => await ((IAlgoStrategy)strategy!).StopAsync(),
                strategy => strategy is IAlgoStrategy && ((IAlgoStrategy)strategy).IsRunning
            );
            PauseStrategyCommand = new RelayCommand(
                async strategy => await ((IAlgoStrategy)strategy!).PauseAsync(),
                strategy => strategy is IAlgoStrategy && ((IAlgoStrategy)strategy).IsRunning
            );
            ResumeStrategyCommand = new RelayCommand(
                async strategy => await ((IAlgoStrategy)strategy!).ResumeAsync(),
                strategy => strategy is IAlgoStrategy && ((IAlgoStrategy)strategy).Status == AlgoStrategyStatus.Paused
            );
        }
    }
}
