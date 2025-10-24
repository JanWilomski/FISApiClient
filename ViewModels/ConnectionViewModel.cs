using System;
using System.Threading.Tasks;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class ConnectionViewModel : ViewModelBase
    {
        private readonly MdsConnectionService _mdsService;
        private readonly SleConnectionService _sleService;

        #region MDS Properties

        private string _mdsIpAddress;
        public string MdsIpAddress
        {
            get => _mdsIpAddress;
            set => SetProperty(ref _mdsIpAddress, value);
        }

        private string _mdsPort;
        public string MdsPort
        {
            get => _mdsPort;
            set => SetProperty(ref _mdsPort, value);
        }

        private string _mdsUser;
        public string MdsUser
        {
            get => _mdsUser;
            set => SetProperty(ref _mdsUser, value);
        }

        private string _mdsPassword;
        public string MdsPassword
        {
            get => _mdsPassword;
            set => SetProperty(ref _mdsPassword, value);
        }

        private string _mdsNode;
        public string MdsNode
        {
            get => _mdsNode;
            set => SetProperty(ref _mdsNode, value);
        }

        private string _mdsSubnode;
        public string MdsSubnode
        {
            get => _mdsSubnode;
            set => SetProperty(ref _mdsSubnode, value);
        }

        private bool _isMdsConnected;
        public bool IsMdsConnected
        {
            get => _isMdsConnected;
            set
            {
                if (SetProperty(ref _isMdsConnected, value))
                {
                    OnPropertyChanged(nameof(IsMdsNotConnected));
                    OnPropertyChanged(nameof(MdsConnectionStatusText));
                    OnPropertyChanged(nameof(MdsConnectionStatusColor));
                    OnPropertyChanged(nameof(CanShowInstruments));
                    ConnectMdsCommand.RaiseCanExecuteChanged();
                    DisconnectMdsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsMdsNotConnected => !IsMdsConnected;
        public string MdsConnectionStatusText => IsMdsConnected ? "Połączono" : "Rozłączono";
        public string MdsConnectionStatusColor => IsMdsConnected ? "#4CAF50" : "#F44336";

        private bool _isMdsConnecting;
        public bool IsMdsConnecting
        {
            get => _isMdsConnecting;
            set
            {
                if (SetProperty(ref _isMdsConnecting, value))
                {
                    ConnectMdsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region SLE Properties

        private string _sleIpAddress;
        public string SleIpAddress
        {
            get => _sleIpAddress;
            set => SetProperty(ref _sleIpAddress, value);
        }

        private string _slePort;
        public string SlePort
        {
            get => _slePort;
            set => SetProperty(ref _slePort, value);
        }

        private string _sleUser;
        public string SleUser
        {
            get => _sleUser;
            set => SetProperty(ref _sleUser, value);
        }

        private string _slePassword;
        public string SlePassword
        {
            get => _slePassword;
            set => SetProperty(ref _slePassword, value);
        }

        private string _sleNode;
        public string SleNode
        {
            get => _sleNode;
            set => SetProperty(ref _sleNode, value);
        }

        private string _sleSubnode;
        public string SleSubnode
        {
            get => _sleSubnode;
            set => SetProperty(ref _sleSubnode, value);
        }

        private bool _isSleConnected;
        public bool IsSleConnected
        {
            get => _isSleConnected;
            set
            {
                if (SetProperty(ref _isSleConnected, value))
                {
                    OnPropertyChanged(nameof(IsSleNotConnected));
                    OnPropertyChanged(nameof(SleConnectionStatusText));
                    OnPropertyChanged(nameof(SleConnectionStatusColor));
                    ConnectSleCommand.RaiseCanExecuteChanged();
                    DisconnectSleCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsSleNotConnected => !IsSleConnected;
        public string SleConnectionStatusText => IsSleConnected ? "Połączono" : "Rozłączono";
        public string SleConnectionStatusColor => IsSleConnected ? "#4CAF50" : "#F44336";

        private bool _isSleConnecting;
        public bool IsSleConnecting
        {
            get => _isSleConnecting;
            set
            {
                if (SetProperty(ref _isSleConnecting, value))
                {
                    ConnectSleCommand.RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region General Properties

        private string _statusMessage = "Gotowy do połączenia";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool CanShowInstruments => IsMdsConnected;

        #endregion

        #region Commands

        public RelayCommand ConnectMdsCommand { get; }
        public RelayCommand DisconnectMdsCommand { get; }
        public RelayCommand ConnectSleCommand { get; }
        public RelayCommand DisconnectSleCommand { get; }

        #endregion

        public ConnectionViewModel()
        {
            _mdsService = new MdsConnectionService();
            _sleService = new SleConnectionService();

            // Load settings from ConfigProvider
            var mdsSettings = ConfigProvider.GetMdsSettings();
            MdsIpAddress = mdsSettings.IpAddress;
            MdsPort = mdsSettings.Port;
            MdsUser = mdsSettings.User;
            MdsPassword = mdsSettings.Password;
            MdsNode = mdsSettings.Node;
            MdsSubnode = mdsSettings.Subnode;

            var sleSettings = ConfigProvider.GetSleSettings();
            SleIpAddress = sleSettings.IpAddress;
            SlePort = sleSettings.Port;
            SleUser = sleSettings.User;
            SlePassword = sleSettings.Password;
            SleNode = sleSettings.Node;
            SleSubnode = sleSettings.Subnode;

            ConnectMdsCommand = new RelayCommand(
                async _ => await ConnectMdsAsync(),
                _ => !IsMdsConnected && !IsMdsConnecting
            );

            DisconnectMdsCommand = new RelayCommand(
                _ => DisconnectMds(),
                _ => IsMdsConnected
            );

            ConnectSleCommand = new RelayCommand(
                async _ => await ConnectSleAsync(),
                _ => !IsSleConnected && !IsSleConnecting
            );

            DisconnectSleCommand = new RelayCommand(
                _ => DisconnectSle(),
                _ => IsSleConnected
            );
        }

        #region MDS Methods

        private async Task ConnectMdsAsync()
        {
            if (IsMdsConnecting) return;

            if (!ValidateMdsInputs())
            {
                StatusMessage = "Błąd: Sprawdź poprawność danych wejściowych MDS";
                return;
            }

            IsMdsConnecting = true;
            StatusMessage = "Łączenie z serwerem MDS...";

            try
            {
                bool success = await _mdsService.ConnectAndLoginAsync(
                    MdsIpAddress,
                    int.Parse(MdsPort),
                    MdsUser,
                    MdsPassword,
                    MdsNode,
                    MdsSubnode
                );

                if (success)
                {
                    IsMdsConnected = true;
                    StatusMessage = $"Pomyślnie połączono z MDS ({MdsIpAddress}:{MdsPort})";
                    // MessageBox.Show(
                    //     "Połączenie z serwerem MDS/SLC zostało nawiązane pomyślnie!",
                    //     "Sukces",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Information
                    // );
                }
                else
                {
                    StatusMessage = "Błąd połączenia MDS: Nieprawidłowe dane logowania lub serwer niedostępny";
                    // MessageBox.Show(
                    //     "Nie udało się połączyć z serwerem MDS/SLC.\nSprawdź dane połączenia i spróbuj ponownie.",
                    //     "Błąd połączenia",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Error
                    // );
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd MDS: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas łączenia z MDS:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsMdsConnecting = false;
            }
        }

        private void DisconnectMds()
        {
            try
            {
                _mdsService.Disconnect();
                IsMdsConnected = false;
                StatusMessage = "Rozłączono z serwerem MDS";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd podczas rozłączania MDS: {ex.Message}";
            }
        }

        private bool ValidateMdsInputs()
        {
            if (string.IsNullOrWhiteSpace(MdsIpAddress))
            {
                MessageBox.Show("Adres IP MDS nie może być pusty", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(MdsPort, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Port MDS musi być liczbą z zakresu 1-65535", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(MdsUser))
            {
                MessageBox.Show("Nazwa użytkownika MDS nie może być pusta", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(MdsPassword))
            {
                MessageBox.Show("Hasło MDS nie może być puste", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        #endregion

        #region SLE Methods

        private async Task ConnectSleAsync()
        {
            if (IsSleConnecting) return;

            if (!ValidateSleInputs())
            {
                StatusMessage = "Błąd: Sprawdź poprawność danych wejściowych SLE";
                return;
            }

            IsSleConnecting = true;
            StatusMessage = "Łączenie z serwerem SLE...";

            try
            {
                bool success = await _sleService.ConnectAndLoginAsync(
                    SleIpAddress,
                    int.Parse(SlePort),
                    SleUser,
                    SlePassword,
                    SleNode,
                    SleSubnode
                );

                if (success)
                {
                    IsSleConnected = true;
                    StatusMessage = $"Pomyślnie połączono z SLE ({SleIpAddress}:{SlePort})";
                    // MessageBox.Show(
                    //     "Połączenie z serwerem SLE (Order Entry) zostało nawiązane pomyślnie!\n\n" +
                    //     "Teraz możesz składać zlecenia z poziomu szczegółów instrumentu.",
                    //     "Sukces",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Information
                    // );
                }
                else
                {
                    StatusMessage = "Błąd połączenia SLE: Nieprawidłowe dane logowania lub serwer niedostępny";
                    // MessageBox.Show(
                    //     "Nie udało się połączyć z serwerem SLE.\nSprawdź dane połączenia i spróbuj ponownie.",
                    //     "Błąd połączenia",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Error
                    // );
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd SLE: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd podczas łączenia z SLE:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsSleConnecting = false;
            }
        }

        private void DisconnectSle()
        {
            try
            {
                _sleService.Disconnect();
                IsSleConnected = false;
                StatusMessage = "Rozłączono z serwerem SLE";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Błąd podczas rozłączania SLE: {ex.Message}";
            }
        }

        private bool ValidateSleInputs()
        {
            if (string.IsNullOrWhiteSpace(SleIpAddress))
            {
                MessageBox.Show("Adres IP SLE nie może być pusty", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(SlePort, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Port SLE musi być liczbą z zakresu 1-65535", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SleUser))
            {
                MessageBox.Show("Nazwa użytkownika SLE nie może być pusta", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SlePassword))
            {
                MessageBox.Show("Hasło SLE nie może być puste", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        #endregion

        public MdsConnectionService GetMdsService() => _mdsService;
        public SleConnectionService GetSleService() => _sleService;
    }
}
