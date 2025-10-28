using System;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class CancelOrderViewModel : ViewModelBase
    {
        private readonly SleConnectionService _sleService;
        private readonly Order _order;

        #region Properties

        // Order details (read-only)
        public string OrderId => _order.OrderId;
        public string ExchangeNumber => _order.ExchangeNumber;
        public string Instrument => _order.Instrument;
        public OrderSide Side => _order.Side;
        public long Quantity => _order.Quantity;
        public long ExecutedQuantity => _order.ExecutedQuantity;
        public long RemainingQuantity => _order.RemainingQuantity;
        public decimal Price => _order.Price;
        public OrderModality Modality => _order.Modality;
        public OrderValidity Validity => _order.Validity;
        public OrderStatus Status => _order.Status;
        public DateTime? OrderTime => _order.OrderTime;

        private bool _isCancelling;
        public bool IsCancelling
        {
            get => _isCancelling;
            set
            {
                if (SetProperty(ref _isCancelling, value))
                {
                    CancelOrderCommand.RaiseCanExecuteChanged();
                    CloseCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region Commands

        public RelayCommand CancelOrderCommand { get; }
        public RelayCommand CloseCommand { get; }

        #endregion

        public event Action? RequestClose;

        public CancelOrderViewModel(Order order, SleConnectionService sleService)
        {
            _order = order ?? throw new ArgumentNullException(nameof(order));
            _sleService = sleService ?? throw new ArgumentNullException(nameof(sleService));

            CancelOrderCommand = new RelayCommand(
                async _ => await CancelOrder(),
                _ => !IsCancelling
            );

            CloseCommand = new RelayCommand(
                _ => RequestClose?.Invoke(),
                _ => !IsCancelling
            );
        }

        private async System.Threading.Tasks.Task CancelOrder()
        {
            if (string.IsNullOrEmpty(_order.ExchangeNumber))
            {
                MessageBox.Show(
                    "Nie można anulować zlecenia - brak Exchange Number.\n" +
                    "Zlecenie musi być zaakceptowane przez giełdę przed anulowaniem.",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // Confirm cancellation
            var result = MessageBox.Show(
                $"Czy na pewno chcesz anulować zlecenie?\n\n" +
                $"Order ID: {OrderId}\n" +
                $"Exchange #: {ExchangeNumber}\n" +
                $"Instrument: {Instrument}\n" +
                $"Side: {Side}\n" +
                $"Remaining: {RemainingQuantity}",
                "Potwierdzenie anulowania",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            IsCancelling = true;
            StatusMessage = "Wysyłanie anulowania...";

            try
            {
                // Send cancel request
                bool success = await _sleService.CancelOrder(
                    _order.ExchangeNumber,
                    _order.LocalCode, // LocalCode
                    _order.GLID,
                    _order.ClientReference,
                    _order.InternalReference
                );

                if (success)
                {
                    StatusMessage = "✓ Anulowanie wysłane";
                    
                    MessageBox.Show(
                        "Żądanie anulowania zlecenia zostało wysłane pomyślnie.",
                        "Sukces",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    RequestClose?.Invoke();
                }
                else
                {
                    StatusMessage = "✗ Błąd wysyłania";
                    
                    MessageBox.Show(
                        "Nie udało się wysłać anulowania zlecenia.",
                        "Błąd",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Błąd: {ex.Message}";
                
                MessageBox.Show(
                    $"Wystąpił błąd podczas anulowania zlecenia:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsCancelling = false;
            }
        }
    }
}