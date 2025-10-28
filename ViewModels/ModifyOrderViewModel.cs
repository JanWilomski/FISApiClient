using System;
using System.Linq;
using System.Windows;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class ModifyOrderViewModel : ViewModelBase
    {
        private readonly SleConnectionService _sleService;
        private readonly Order _order;

        #region Properties

        // Original order info (read-only)
        public string OrderId => _order.OrderId;
        public string Instrument => _order.Instrument;
        public string ExchangeNumber => _order.ExchangeNumber;
        public OrderSide Side => _order.Side;
        public long OriginalQuantity => _order.Quantity;
        public decimal OriginalPrice => _order.Price;
        public OrderValidity OriginalValidity => _order.Validity;

        // New values (editable)
        private string _newQuantity;
        public string NewQuantity
        {
            get => _newQuantity;
            set
            {
                if (SetProperty(ref _newQuantity, value))
                {
                    ModifyCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _newPrice;
        public string NewPrice
        {
            get => _newPrice;
            set
            {
                if (SetProperty(ref _newPrice, value))
                {
                    ModifyCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private OrderValidity _newValidity;
        public OrderValidity NewValidity
        {
            get => _newValidity;
            set
            {
                if (SetProperty(ref _newValidity, value))
                {
                    ModifyCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public System.Collections.Generic.IEnumerable<OrderValidity> Validities =>
            Enum.GetValues(typeof(OrderValidity)).Cast<OrderValidity>().Where(v => v != OrderValidity.Unknown);

        private bool _isModifying;
        public bool IsModifying
        {
            get => _isModifying;
            set
            {
                if (SetProperty(ref _isModifying, value))
                {
                    ModifyCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
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

        public RelayCommand ModifyCommand { get; }
        public RelayCommand CancelCommand { get; }

        #endregion

        public event Action? RequestClose;

        public ModifyOrderViewModel(Order order, SleConnectionService sleService)
        {
            _order = order ?? throw new ArgumentNullException(nameof(order));
            _sleService = sleService ?? throw new ArgumentNullException(nameof(sleService));

            // Initialize with current values
            _newQuantity = order.Quantity.ToString();
            _newPrice = order.Price.ToString("F2");
            _newValidity = order.Validity;

            ModifyCommand = new RelayCommand(
                async _ => await ModifyOrder(),
                _ => !IsModifying && IsValid()
            );

            CancelCommand = new RelayCommand(
                _ => RequestClose?.Invoke(),
                _ => !IsModifying
            );
        }

        private bool IsValid()
        {
            // Check if at least one field changed
            bool quantityChanged = false;
            bool priceChanged = false;

            if (long.TryParse(NewQuantity, out long qty))
            {
                quantityChanged = qty != OriginalQuantity && qty > 0;
            }

            if (decimal.TryParse(NewPrice, out decimal price))
            {
                priceChanged = price != OriginalPrice && price > 0;
            }

            bool validityChanged = NewValidity != OriginalValidity;

            return quantityChanged || priceChanged || validityChanged;
        }

        private async System.Threading.Tasks.Task ModifyOrder()
        {
            if (string.IsNullOrEmpty(_order.ExchangeNumber))
            {
                MessageBox.Show(
                    "Nie można zmodyfikować zlecenia - brak Exchange Number.\n" +
                    "Zlecenie musi być zaakceptowane przez giełdę przed modyfikacją.",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            IsModifying = true;
            StatusMessage = "Wysyłanie modyfikacji...";

            try
            {
                // Parse new values
                long? newQty = null;
                if (long.TryParse(NewQuantity, out long qty) && qty != OriginalQuantity)
                {
                    newQty = qty;
                }

                decimal? newPrice = null;
                if (decimal.TryParse(NewPrice, out decimal price) && price != OriginalPrice)
                {
                    newPrice = price;
                }

                OrderValidity? newValidity = null;
                if (NewValidity != OriginalValidity)
                {
                    newValidity = NewValidity;
                }

                // Send modify request
                bool success = await _sleService.ModifyOrder(
                    _order.ExchangeNumber,
                    _order.LocalCode, // LocalCode
                    _order.GLID,
                    newQty,
                    newPrice,
                    newValidity,
                    _order.ClientReference,
                    _order.InternalReference,
                    _order.ClientCodeType,
                    _order.ClearingAccount,
                    _order.AllocationCode,
                    _order.Memo
                );

                if (success)
                {
                    StatusMessage = "✓ Modyfikacja wysłana";
                    
                    MessageBox.Show(
                        "Modyfikacja zlecenia została wysłana pomyślnie.",
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
                        "Nie udało się wysłać modyfikacji zlecenia.",
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
                    $"Wystąpił błąd podczas modyfikacji zlecenia:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsModifying = false;
            }
        }
    }
}