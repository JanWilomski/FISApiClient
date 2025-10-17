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

        #endregion

        #region Constructor

        public OrderBookViewModel(SleConnectionService sleService)
        {
            _sleService = sleService;

            // Inicjalizuj komendy - z parametrem object
            RefreshCommand = new RelayCommand(
                async (parameter) => await RefreshOrderBook(), 
                (parameter) => !IsLoading && _sleService.IsConnected
            );
    
            CancelOrderCommand = new RelayCommand(
                (parameter) => CancelSelectedOrder(), 
                (parameter) => SelectedOrder != null
            );
    
            ClearAllCommand = new RelayCommand((parameter) => ClearOrders());

            // Subskrybuj eventy z SLE Service
            _sleService.OrderBookReceived += OnOrderBookReceived;
            _sleService.OrderUpdated += OnOrderUpdated;

            System.Diagnostics.Debug.WriteLine("[OrderBookVM] ViewModel initialized");
    
            // Real-time jest już aktywny (request 2017 wysyłany przy logowaniu)
            IsRealTimeActive = _sleService.IsConnected;
            if (IsRealTimeActive)
            {
                StatusMessage = "✓ Real-Time aktywny | Kliknij 'Odśwież' aby pobrać zlecenia";
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Odświeża Order Book - wysyła request 2004
        /// </summary>
        private async System.Threading.Tasks.Task RefreshOrderBook()
        {
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

        /// <summary>
        /// Anuluje wybrane zlecenie
        /// </summary>
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
                // TODO: Implementacja anulowania zlecenia (request 2000 z Command=2)
                StatusMessage = "Anulowanie zlecenia - funkcja do implementacji";
                MessageBox.Show(
                    "Funkcja anulowania zlecenia będzie wkrótce dostępna.",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        /// <summary>
        /// Czyści listę zleceń
        /// </summary>
        private void ClearOrders()
        {
            Orders.Clear();
            UpdateCount = 0;
            StatusMessage = "Lista zleceń wyczyszczona | Kliknij 'Odśwież' aby pobrać ponownie";
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Obsługuje otrzymanie danych Order Book z serwera
        /// </summary>
        private void OnOrderBookReceived(System.Collections.Generic.List<Order> orders)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Received {orders.Count} orders");

                    foreach (var order in orders)
                    {
                        // Sprawdź czy zlecenie już istnieje (po OrderId lub SleReference)
                        var existingOrder = Orders.FirstOrDefault(o =>
                            (!string.IsNullOrEmpty(order.OrderId) && o.OrderId == order.OrderId) ||
                            (!string.IsNullOrEmpty(order.SleReference) && o.SleReference == order.SleReference)
                        );

                        if (existingOrder != null)
                        {
                            // Aktualizuj istniejące zlecenie
                            UpdateOrderProperties(existingOrder, order);
                            System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Updated existing order: {order.OrderId}");
                        }
                        else
                        {
                            // Dodaj nowe zlecenie
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

        /// <summary>
        /// Obsługuje real-time update pojedynczego zlecenia
        /// </summary>
        private void OnOrderUpdated(Order updatedOrder)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Real-time update for order: {updatedOrder.OrderId}");

                    // Znajdź istniejące zlecenie
                    var existingOrder = Orders.FirstOrDefault(o =>
                        (!string.IsNullOrEmpty(updatedOrder.OrderId) && o.OrderId == updatedOrder.OrderId) ||
                        (!string.IsNullOrEmpty(updatedOrder.SleReference) && o.SleReference == updatedOrder.SleReference)
                    );

                    if (existingOrder != null)
                    {
                        // Aktualizuj istniejące zlecenie
                        UpdateOrderProperties(existingOrder, updatedOrder);
                        System.Diagnostics.Debug.WriteLine($"[OrderBookVM] Updated order via real-time: {updatedOrder.OrderId}");
                    }
                    else
                    {
                        // Dodaj nowe zlecenie (nowo złożone)
                        Orders.Insert(0, updatedOrder); // Dodaj na początku listy
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

        /// <summary>
        /// Kopiuje właściwości z jednego zlecenia do drugiego
        /// </summary>
        private void UpdateOrderProperties(Order target, Order source)
        {
            if (!string.IsNullOrEmpty(source.OrderId))
                target.OrderId = source.OrderId;
            if (!string.IsNullOrEmpty(source.SleReference))
                target.SleReference = source.SleReference;
            if (!string.IsNullOrEmpty(source.Instrument))
                target.Instrument = source.Instrument;
            if (!string.IsNullOrEmpty(source.Side))
                target.Side = source.Side;
            if (source.Quantity > 0)
                target.Quantity = source.Quantity;
            if (source.ExecutedQuantity >= 0)
                target.ExecutedQuantity = source.ExecutedQuantity;
            if (source.Price > 0)
                target.Price = source.Price;
            if (!string.IsNullOrEmpty(source.Modality))
                target.Modality = source.Modality;
            if (!string.IsNullOrEmpty(source.Validity))
                target.Validity = source.Validity;
            if (!string.IsNullOrEmpty(source.Status))
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
    }
}