using System;
using System.Windows.Input;

namespace FISApiClient.ViewModels
{
    /// <summary>
    /// Implementacja ICommand dla wzorca MVVM
    /// Obsługuje wywołania metod z ViewModelu oraz warunki CanExecute
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <summary>
        /// Zdarzenie informujące o zmianie możliwości wykonania komendy
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Konstruktor RelayCommand
        /// </summary>
        /// <param name="execute">Akcja do wykonania</param>
        /// <param name="canExecute">Warunek określający czy komenda może być wykonana (opcjonalny)</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Określa czy komenda może być wykonana
        /// </summary>
        /// <param name="parameter">Parametr komendy</param>
        /// <returns>True jeśli komenda może być wykonana, false w przeciwnym razie</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Wykonuje komendę
        /// </summary>
        /// <param name="parameter">Parametr komendy</param>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Ręcznie wywołuje ponowną ocenę CanExecute
        /// Przydatne gdy stan aplikacji się zmienił i trzeba zaktualizować dostępność komend
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
