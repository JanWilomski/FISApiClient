using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FISApiClient.Helpers
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void OnPropertyChangedOnUIThread([CallerMemberName] string? propertyName = null)
        {
            if (Application.Current != null && Application.Current.Dispatcher.CheckAccess())
            {
                // Already on UI thread
                OnPropertyChanged(propertyName);
            }
            else
            {
                // Marshal to UI thread
                Application.Current?.Dispatcher.Invoke(() => OnPropertyChanged(propertyName));
            }
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChangedOnUIThread(propertyName);
            return true;
        }
    }
}
