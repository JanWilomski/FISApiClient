using System;
using System.Threading.Tasks;
using System.Windows;
using Cross_FIS_API_1._2.Helpers;
using Cross_FIS_API_1._2.Models;

namespace Cross_FIS_API_1._2.ViewModels
{
    public class ConnectionViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;

        #region Properties

        private string _ipAddress = "192.168.45.25";
        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        private string _port = "25503";
        public string Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        private string _user = "103";
        public string User
        {
            get => _user;
            set => SetProperty(ref _user, value);
        }

        private string _password = "glglgl";
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private string _node = "5500";
        public string Node
        {
            get => _node;
            set => SetProperty(ref _node, value);
        }

        private string _subnode = "4500";
        public string Subnode
        {
            get => _subnode;
            set => SetProperty(ref _subnode, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(IsNotConnected));
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                    ConnectCommand.RaiseCanExecuteChanged();
                    DisconnectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsNotConnected => !IsConnected;

        public string ConnectionStatusText => IsConnected ? "Połączono" : "Rozłączono";
        
        public string ConnectionStatusColor => IsConnected ? "#4CAF50" : "#F44336";

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                if (SetProperty(ref _isConnecting, value))
                {
                    ConnectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "Gotowy do połączenia";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region Commands

        public RelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }

        #endregion

        public ConnectionViewModel()
        {
            _mdsService = new MdsConnectionService();

            ConnectCommand = new RelayCommand(
                async _ => await ConnectAsync(),
                _ => !IsConnected && !IsConnecting
            );

            DisconnectCommand = new RelayCommand(
                _ => Disconnect(),
                _ => IsConnected
            );
        }

        private async Task ConnectAsync()
        {
            if (IsConnecting) return;

            if (!ValidateInputs())
            {
                StatusMessage = "Błąd: Sprawdź poprawność danych wejściowych";
                return;
            }

            IsConnecting = true;
            StatusMessage = "Łączenie z serwerem MDS...";

            try
            {
                bool success = await _mdsService.ConnectAndLoginAsync(
                    IpAddress,
                    int.Parse(Port),
                    User,
                    Password,
                    Node,
                    Subnode
                );

                if (success)
                {
                    IsConnected = true;
                    StatusMessage = $"Pomyślnie połączono z {IpAddress}:{Port}";
                    MessageBox.Show(
                        "Połączenie z serwerem MDS/SLC zostało nawiązane pomyślnie!",
                        "Sukces",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    StatusMessage = "Błąd połączenia: Nieprawidłowe dane logowania lub serwer niedostępny";
                    MessageBox.Show(
                        "Nie udało się połączyć z serwerem MDS/SLC.\nSprawdź dane połączenia i spróbuj ponownie.",
                        "Błąd połączenia",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas łączenia:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private void Disconnect()
        {
            try
            {
                _mdsService.Disconnect();
                IsConnected = false;
                StatusMessage = "Rozłączono z serwerem";
                MessageBox.Show(
                    "Rozłączono z serwerem MDS/SLC",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd podczas rozłączania: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas rozłączania:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                MessageBox.Show("Adres IP nie może być pusty", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(Port, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Port musi być liczbą z zakresu 1-65535", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(User))
            {
                MessageBox.Show("Nazwa użytkownika nie może być pusta", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Hasło nie może być puste", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(Node))
            {
                MessageBox.Show("Node nie może być pusty", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(Subnode))
            {
                MessageBox.Show("Subnode nie może być pusty", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        public MdsConnectionService GetMdsService() => _mdsService;
    }
}
