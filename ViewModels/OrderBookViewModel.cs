using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FISApiClient.Helpers;
using FISApiClient.Models;

namespace FISApiClient.ViewModels
{
    public class OrderBookViewModel : ViewModelBase
    {
        private readonly SleConnectionService _sleService;

        #region Properties

        private ObservableCollection<Order> _orders = new ObservableCollection<Order>();
        public ObservableCollection<Order> Orders
        {
            get => _orders;
            set => SetProperty(ref _orders, value);
        }

        private Order? _selectedOrder;
        public Order? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                if (SetProperty(ref _selectedOrder, value))
                {
                    CancelOrderCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    RefreshCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = "Kliknij 'Odśwież' aby pobrać zlecenia";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _updateCount;
        public int UpdateCount
        {
            get => _updateCount;
            set => SetProperty(ref _updateCount, value);
        }

        private bool _isRealTimeActive;
        public bool IsRealTimeActive
        {
            get => _isRealTimeActive;
            set => SetProperty(ref _isRealTimeActive, value);
        }

        #endregion

        #region Commands

        public RelayCommand RefreshCommand { get; }
        public RelayCommand CancelOrderCommand { get; }
        public RelayCommand ClearAllCommand { get; }
        public RelayCommand ModifyOrderCommand { get; }

        #endregion

        #region Constructor

        public OrderBookViewModel(SleConnectionService sleService)
        {
            _sleService = sleService;

            RefreshCommand = new RelayCommand(
                async _ => await RefreshOrderBook(), 
                _ => !IsLoading && _sleService.IsConnected
            );

            // ZAKTUALIZUJ - zmień metodę na OpenModifyOrderWindow
            ModifyOrderCommand = new RelayCommand(
                _ => OpenModifyOrderWindow(),
                _ => SelectedOrder != null && !string.IsNullOrEmpty(SelectedOrder.ExchangeNumber)
            );

            // ZAKTUALIZUJ - zmień metodę na OpenCancelOrderWindow
            CancelOrderCommand = new RelayCommand(
                _ => OpenCancelOrderWindow(), 
                _ => SelectedOrder != null && !string.IsNullOrEmpty(SelectedOrder.ExchangeNumber)
            );

            ClearAllCommand = new RelayCommand(_ => ClearOrders());

            _sleService.OrderBookReceived += OnOrderBookReceived;
            _sleService.OrderUpdated += OnOrderUpdated;

            System.Diagnostics.Debug.WriteLine("[OrderBookVM] ViewModel initialized with Modify/Cancel support");

            IsRealTimeActive = _sleService.IsConnected;
            if (IsRealTimeActive)
            {
                StatusMessage = "✓ Real-Time aktywny | Kliknij 'Odśwież' aby pobrać zlecenia";
            }
        }

        #endregion

        #region Methods

        private async System.Threading.Tasks.Task RefreshOrderBook()
        {
            ClearOrders();
            IsLoading = true;
            StatusMessage = "Pobieranie zleceń...";

            try
            {
                System.Diagnostics.Debug.WriteLine("[OrderBookVM] Requesting order book refresh");
                
                bool success = await _sleService.RequestOrderBookAsync();
                
                if (!success)
                {
                    StatusMessage = "✗ Błąd podczas pobierania zleceń";
                    MessageBox.Show(
                        "Nie udało się pobrać Order Book.\nSprawdź połączenie z serwerem SLE.",
                        "Błąd",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                else
                {
                    StatusMessage = "Oczekiwanie na dane...";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Błąd: {ex.Message}";
                MessageBox.Show(
                    $"Wystąpił błąd:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsLoading = false;
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        private void CancelSelectedOrder()
        {
            if (SelectedOrder == null) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz anulować zlecenie?\n\n" +
                $"Order ID: {SelectedOrder.OrderId}\n" +
                $"Instrument: {SelectedOrder.Instrument}\n" +
                $"Side: {SelectedOrder.GetSideDescription()}\n" +
                $"Quantity: {SelectedOrder.Quantity}",
                "Potwierdzenie anulowania",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                StatusMessage = "Anulowanie zlecenia - funkcja do implementacji";
                MessageBox.Show(
                    "Funkcja anulowania zlecenia będzie wkrótce dostępna.",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        private void ClearOrders()
        {
            Orders.Clear();
            UpdateCount = 0;
            StatusMessage = "Lista zleceń wyczyszczona | Kliknij 'Odśwież' aby pobrać ponownie";
        }

        #endregion

        #region Event Handlers

        private void OnOrderBookReceived(System.Collections.Generic.List<Order> orders)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Received {orders.Count} orders");

                    foreach (var order in orders)
                    {
                        var existingOrder = Orders.FirstOrDefault(o =>
                            (!string.IsNullOrEmpty(order.OrderId) && o.OrderId == order.OrderId) ||
                            (!string.IsNullOrEmpty(order.SleReference) && o.SleReference == order.SleReference)
                        );

                        if (existingOrder != null)
                        {
                            UpdateOrderProperties(existingOrder, order);
                            System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Updated existing order: {order.OrderId}");
                        }
                        else
                        {
                            Orders.Add(order);
                            System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Added new order: {order.OrderId}");
                        }
                    }

                    UpdateCount++;
                    IsLoading = false;
                    StatusMessage = $"✓ Załadowano {Orders.Count} zleceń | Real-Time aktywny | Updates: {UpdateCount} | {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Error processing order book: {ex.Message}");
                    StatusMessage = $"✗ Błąd przetwarzania: {ex.Message}";
                }
            });
        }

        private void OnOrderUpdated(Order updatedOrder)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Real-time update for order: {updatedOrder.OrderId}");

                    var existingOrder = Orders.FirstOrDefault(o =>
                        (!string.IsNullOrEmpty(updatedOrder.OrderId) && o.OrderId == updatedOrder.OrderId) ||
                        (!string.IsNullOrEmpty(updatedOrder.SleReference) && o.SleReference == updatedOrder.SleReference)
                    );

                    if (existingOrder != null)
                    {
                        UpdateOrderProperties(existingOrder, updatedOrder);
                        System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Updated order via real-time: {updatedOrder.OrderId}");
                    }
                    else
                    {
                        Orders.Insert(0, updatedOrder);
                        System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Added new order via real-time: {updatedOrder.OrderId}");
                    }

                    UpdateCount++;
                    StatusMessage = $"✓ Real-Time | Orders: {Orders.Count} | Updates: {UpdateCount} | {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Error processing order update: {ex.Message}");
                }
            });
        }

        private void UpdateOrderProperties(Order target, Order source)
        {
            if (!string.IsNullOrEmpty(source.OrderId))
                target.OrderId = source.OrderId;
            if (!string.IsNullOrEmpty(source.SleReference))
                target.SleReference = source.SleReference;
            if (!string.IsNullOrEmpty(source.Instrument))
                target.Instrument = source.Instrument;
            if (source.Side != OrderSide.Unknown)
                target.Side = source.Side;
            if (source.Quantity > 0)
                target.Quantity = source.Quantity;
            if (source.ExecutedQuantity >= 0)
                target.ExecutedQuantity = source.ExecutedQuantity;
            if (source.Price > 0)
                target.Price = source.Price;
            if (source.Modality != OrderModality.Unknown)
                target.Modality = source.Modality;
            if (source.Validity != OrderValidity.Unknown)
                target.Validity = source.Validity;
            if (source.Status != OrderStatus.Unknown)
                target.Status = source.Status;
            if (source.OrderTime != default)
                target.OrderTime = source.OrderTime;
            target.LastUpdateTime = DateTime.Now;
            if (!string.IsNullOrEmpty(source.ClientReference))
                target.ClientReference = source.ClientReference;
            if (!string.IsNullOrEmpty(source.RejectReason))
                target.RejectReason = source.RejectReason;
            if (source.AveragePrice > 0)
                target.AveragePrice = source.AveragePrice;
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            System.Diagnostics.Debug.WriteLine("[OrderBookVM] Cleaning up");
            
            _sleService.OrderBookReceived -= OnOrderBookReceived;
            _sleService.OrderUpdated -= OnOrderUpdated;
        }

        #endregion
        
        #region Order Actions

        private void OpenModifyOrderWindow()
        {
            if (SelectedOrder == null)
                return;

            if (string.IsNullOrEmpty(SelectedOrder.ExchangeNumber))
            {
                MessageBox.Show(
                    "Nie można zmodyfikować zlecenia - brak Exchange Number.\n" +
                    "Zlecenie musi być najpierw zaakceptowane przez giełdę.",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Opening modify window for order: {SelectedOrder.OrderId}");

            var modifyWindow = new Views.ModifyOrderWindow(SelectedOrder, _sleService);
            modifyWindow.Owner = Application.Current.MainWindow;
            modifyWindow.ShowDialog();
        }

        private void OpenCancelOrderWindow()
        {
            if (SelectedOrder == null)
                return;

            if (string.IsNullOrEmpty(SelectedOrder.ExchangeNumber))
            {
                MessageBox.Show(
                    "Nie można anulować zlecenia - brak Exchange Number.\n" +
                    "Zlecenie musi być najpierw zaakceptowane przez giełdę.",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Opening cancel window for order: {SelectedOrder.OrderId}");

            var cancelWindow = new Views.CancelOrderWindow(SelectedOrder, _sleService);
            cancelWindow.Owner = Application.Current.MainWindow;
            cancelWindow.ShowDialog();
        }

        #endregion
    }
}