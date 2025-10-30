using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FISApiClient.Models
{
    public class SleConnectionService
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private string _node = string.Empty;
        private string _subnode = string.Empty;
        private string _userNumber = string.Empty;
        private string _assignedCallingId = "00000"; // Numer przydzielony przez serwer podczas logowania
        
        private static long _orderIdCounter = 0;
        private static readonly object _orderIdLock = new object();

        private const byte Stx = 2;
        private const byte Etx = 3;
        private const int HeaderLength = 32;
        private const int FooterLength = 3;

        public bool IsConnected => _tcpClient?.Connected ?? false;
        
        // Eventy dla odpowiedzi z SLE
        public event Action<OrderReply>? OrderReplyReceived;
        public event Action<string>? OrderRejected;
        public event Action<string>? OrderAccepted;
        public event Action<List<Order>>? OrderBookReceived;
        public event Action<Order>? OrderUpdated;
        
        private readonly List<byte> _receiveBuffer = new List<byte>();
        private bool _realtimeSubscribed = false;

        public async Task<bool> ConnectAndLoginAsync(string ipAddress, int port, string user, string password, string node, string subnode)
        {
            if (IsConnected) Disconnect();

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ipAddress, port);
                if (!_tcpClient.Connected) return false;

                _stream = _tcpClient.GetStream();

                _node = node;
                _subnode = subnode;
                _userNumber = user;

                // Wysyłamy identyfikator klienta (16 bajtów)
                var clientId = Encoding.ASCII.GetBytes("FISAPICLIENT    ");
                await _stream.WriteAsync(clientId, 0, clientId.Length);

                // Wysyłamy request logowania (1100)
                byte[] loginRequest = BuildLoginRequest(user, password);
                await _stream.WriteAsync(loginRequest, 0, loginRequest.Length);

                var buffer = new byte[1024];
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    bool loginSuccess = VerifyLoginResponse(buffer, bytesRead);
                    if (loginSuccess)
                    {
                        Debug.WriteLine("[SLE] Login successful");
                        
                        // Zasubskrybuj real-time replies (wymagane przed składaniem zleceń!)
                        await SubscribeToRealTimeReplies();
                        
                        _ = Task.Run(ListenForMessages);
                        return true;
                    }
                }
                
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Connection failed: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Subskrypcja real-time replies (request 2017) - WYMAGANE przed wysłaniem zleceń
        /// </summary>
        private async Task SubscribeToRealTimeReplies()
        {
            if (!IsConnected || _stream == null) return;

            try
            {
                byte[] subscriptionRequest = BuildRealTimeSubscriptionRequest();
                await _stream.WriteAsync(subscriptionRequest, 0, subscriptionRequest.Length);
                await _stream.FlushAsync();
                
                _realtimeSubscribed = true;
                Debug.WriteLine("[SLE] Real-time replies subscription sent (request 2017)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Failed to subscribe to real-time replies: {ex.Message}");
            }
        }

        /// <summary>
        /// Wysyła nowe zlecenie (request 2000)
        /// </summary>
        public async Task<bool> SendOrderAsync(
            string localCode,      
            string glid,           
            OrderSide side,
            long quantity,
            OrderModality modality,
            decimal price,
            OrderValidity validity,
            string clientReference,
            string internalReference,
            string clientCodeType,
            string clearingAccount,
            string allocationCode,
            string memo,
            string secondClientCodeType,
            string floorTraderId,
            string clientFreeField1,
            string currency)
        {
            try
            {
                Debug.WriteLine($"[SLE] ========================================");
                Debug.WriteLine($"[SLE] Sending order with LOCAL CODE");
                Debug.WriteLine($"[SLE] LocalCode: {localCode}");
                Debug.WriteLine($"[SLE] GLID: {glid}");
                Debug.WriteLine($"[SLE] Side: {side}");
                Debug.WriteLine($"[SLE] Quantity: {quantity}");
                Debug.WriteLine($"[SLE] Modality: {modality}");
                Debug.WriteLine($"[SLE] Price: {price}");
                Debug.WriteLine($"[SLE] Validity: {validity}");

                byte[] orderRequest = BuildOrderRequest(
                    localCode, glid, side, quantity, modality, price, validity,
                    clientReference, internalReference, clientCodeType, clearingAccount,
                    allocationCode, memo, secondClientCodeType, floorTraderId,
                    clientFreeField1, currency);

                await _stream.WriteAsync(orderRequest, 0, orderRequest.Length);
                await _stream.FlushAsync();

                Debug.WriteLine($"[SLE] ✓ Order sent successfully");
                Debug.WriteLine($"[SLE] ========================================");
        
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] ✗ Failed to send order: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (_tcpClient == null) return;
            
            _realtimeSubscribed = false;
            _stream?.Close();
            _tcpClient?.Close();
            _tcpClient = null;
            _stream = null;
            
            Debug.WriteLine("[SLE] Disconnected");
        }

        private async Task ListenForMessages()
        {
            if (_stream == null) return;
            var buffer = new byte[8192];
    
            Debug.WriteLine("[SLE] ListenForMessages started");
    
            while (IsConnected)
            {
                try
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
            
                    if (bytesRead > 0)
                    {
                        Debug.WriteLine($"[SLE] Received {bytesRead} bytes from server");
                
                        for (int i = 0; i < bytesRead; i++)
                        {
                            _receiveBuffer.Add(buffer[i]);
                        }
                
                        ProcessBufferedMessages();
                    }
                    else
                    {
                        Debug.WriteLine("[SLE] Connection closed by server");
                        Disconnect();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SLE] Error in ListenForMessages: {ex.Message}");
                    Disconnect();
                    break;
                }
            }
    
            Debug.WriteLine("[SLE] ListenForMessages stopped");
        }

        private void ProcessBufferedMessages()
        {
            while (_receiveBuffer.Count >= 3)
            {
                bool foundValidMessage = false;
                int messageStart = -1;
                int totalMessageLength = 0;
                
                for (int i = 0; i <= _receiveBuffer.Count - 3; i++)
                {
                    if (_receiveBuffer[i + 2] == Stx)
                    {
                        int length = _receiveBuffer[i] + 256 * _receiveBuffer[i + 1];
                        
                        if (length >= 35 && length <= 30000)
                        {
                            messageStart = i;
                            totalMessageLength = length;
                            foundValidMessage = true;
                            break;
                        }
                    }
                }
                
                if (!foundValidMessage)
                {
                    if (_receiveBuffer.Count > 0)
                    {
                        _receiveBuffer.RemoveAt(0);
                    }
                    
                    if (_receiveBuffer.Count > 100)
                    {
                        _receiveBuffer.Clear();
                    }
                    
                    return;
                }
                
                if (messageStart + totalMessageLength > _receiveBuffer.Count)
                {
                    return;
                }
                
                byte[] messageBytes = _receiveBuffer.GetRange(messageStart, totalMessageLength).ToArray();
                ProcessSingleMessage(messageBytes, totalMessageLength, 2);
                _receiveBuffer.RemoveRange(0, messageStart + totalMessageLength);
            }
        }

        private void ProcessSingleMessage(byte[] response, int length, int stxPos)
        {
            try
            {
                string requestNumberStr = Encoding.ASCII.GetString(response, stxPos + 24, 5);
                if (int.TryParse(requestNumberStr, out int requestNumber))
                {
                    Debug.WriteLine($"[SLE] Received response with request number: {requestNumber}");
            
                    switch(requestNumber)
                    {
                        case 2019: // Real-time order update
                            ProcessOrderReply(response, length, stxPos);
                            break;
                        case 2004: // Order book consultation reply
                            ProcessOrderBookReply(response, length, stxPos);
                            break;
                        case 2008: // Replies book consultation
                            ProcessRepliesBook(response, length, stxPos);
                            break;
                        default:
                            Debug.WriteLine($"[SLE] Unhandled request number: {requestNumber}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Error processing message: {ex.Message}");
            }
        }

        private void ProcessOrderReply(byte[] response, int length, int stxPos)
        {
            try
            {
                Debug.WriteLine("[SLE] === PROCESSING ORDER REPLY (2019) ===");
                
                int pos = stxPos + HeaderLength;
                
                // A: Chaining
                char chaining = (char)response[pos++];
                Debug.WriteLine($"[SLE] Chaining: {chaining}");
                
                // B: User number (5 bytes)
                string userNum = Encoding.ASCII.GetString(response, pos, 5);
                pos += 5;
                Debug.WriteLine($"[SLE] User number: {userNum}");
                
                // C: Request category (1 byte)
                char requestCategory = (char)response[pos++];
                Debug.WriteLine($"[SLE] Request category: {requestCategory}");
                
                // D: Reply type (1 byte)
                char replyType = (char)response[pos++];
                Debug.WriteLine($"[SLE] Reply type: {replyType}");
                
                string replyTypeDesc = replyType switch
                {
                    'A' => "Exchange acknowledgement",
                    'C' => "Exchange reject",
                    'G' => "GL reject",
                    'R' => "Trade execution",
                    'J' => "Exchange message",
                    'L' => "Inflected message",
                    _ => "Unknown"
                };
                Debug.WriteLine($"[SLE] Reply type description: {replyTypeDesc}");
                
                // E: Index (6 bytes)
                string index = Encoding.ASCII.GetString(response, pos, 6);
                pos += 6;
                Debug.WriteLine($"[SLE] Index: {index}");
                
                // F: Number of replies (5 bytes)
                string numReplies = Encoding.ASCII.GetString(response, pos, 5);
                pos += 5;
                Debug.WriteLine($"[SLE] Number of replies: {numReplies}");
                
                // G: Stockcode (GL encoded)
                var (stockcode, bytesRead) = DecodeGLField(response, pos);
                pos += bytesRead;
                Debug.WriteLine($"[SLE] Stockcode: {stockcode}");
                
                // Filler (10 bytes)
                pos += 10;
                
                // BITMAP - dekodowanie pól SEKWENCYJNIE (bez numerów pól!)
                Debug.WriteLine("[SLE] Decoding bitmap fields SEQUENTIALLY...");
                var bitmapFields = new Dictionary<int, string>();
                int fieldNumber = 0;
                
                while (pos < length - FooterLength)
                {
                    // Sprawdź czy nie dotarliśmy do footera
                    if (pos + FooterLength <= length && response[pos + 2] == Etx)
                    {
                        Debug.WriteLine($"[SLE] Reached footer at position {pos}");
                        break;
                    }
                    
                    // Dekoduj wartość pola (GL encoded) - SEKWENCYJNIE!
                    var (fieldValue, fieldBytesRead) = DecodeGLField(response, pos);
                    pos += fieldBytesRead;
                    
                    // Jeśli pole nie jest puste, zapisz je
                    if (!string.IsNullOrEmpty(fieldValue))
                    {
                        bitmapFields[fieldNumber] = fieldValue;
                        
                        // Loguj tylko kluczowe pola
                        if (fieldNumber <= 10 || fieldNumber == 30 || fieldNumber == 42 || 
                            fieldNumber == 62 || fieldNumber == 65 || fieldNumber == 261)
                        {
                            Debug.WriteLine($"[SLE] Field #{fieldNumber} = [{fieldValue}]");
                        }
                    }
                    
                    fieldNumber++;
                    
                    // Zabezpieczenie przed nieskończoną pętlą
                    if (fieldNumber > 2000)
                    {
                        Debug.WriteLine($"[SLE] Warning: Too many fields (>{fieldNumber}), breaking");
                        break;
                    }
                }
                
                Debug.WriteLine($"[SLE] Total fields decoded: {fieldNumber}");
                Debug.WriteLine($"[SLE] Non-empty fields: {bitmapFields.Count}");
                
                // Wyświetl najważniejsze pola
                if (bitmapFields.ContainsKey(30)) 
                    Debug.WriteLine($"[SLE] Order Status: {bitmapFields[30]}");
                if (bitmapFields.ContainsKey(42)) 
                    Debug.WriteLine($"[SLE] SLE Reference: {bitmapFields[42]}");
                if (bitmapFields.ContainsKey(261)) 
                    Debug.WriteLine($"[SLE] Order ID: {bitmapFields[261]}");
                if (bitmapFields.ContainsKey(65)) 
                    Debug.WriteLine($"[SLE] Reject Code: {bitmapFields[65]}");
                
                Debug.WriteLine("[SLE] === ORDER REPLY COMPLETE ===");
                
                // Wywołaj eventy
                if (replyType == 'A')
                {
                    _ = Task.Run(() => OrderAccepted?.Invoke($"Order accepted - Reference: {bitmapFields.GetValueOrDefault(42, "N/A")}"));
                }
                else if (replyType == 'C' || replyType == 'G')
                {
                    _ = Task.Run(() => OrderRejected?.Invoke($"Order rejected - Code: {bitmapFields.GetValueOrDefault(65, "Unknown")}"));
                }
                
                // Dodatkowo - obsłuż real-time update dla Order Book
                if (stockcode != null && !string.IsNullOrEmpty(stockcode))
                {
                    HandleOrderUpdate(bitmapFields, stockcode, replyType);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Failed to parse order reply: {ex.Message}");
                Debug.WriteLine($"[SLE] Stack trace: {ex.StackTrace}");
            }
        }

        private void ProcessRepliesBook(byte[] response, int length, int stxPos)
        {
            Debug.WriteLine("[SLE] Processing replies book (2008)");
        }
        
        private string GenerateOrderId()
        {
            lock (_orderIdLock)
            {
                _orderIdCounter++;
        
                // Format: ORDyMMddHHmmss### gdzie ### to licznik 0-999
                string timestamp = DateTime.Now.ToString("yyMMddHHmmss");
                string counter = (_orderIdCounter % 1000).ToString("D3");
                string orderId = $"ORD{timestamp}{counter}";
        
                // Maksymalna długość 16 znaków (zgodnie z FIS API)
                if (orderId.Length > 16)
                {
                    orderId = orderId.Substring(0, 16);
                }
        
                return orderId;
            }
        }

        #region Message Builders

        private byte[] BuildLoginRequest(string user, string password)
        {
            var dataBuilder = new List<byte>();
            
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(user.PadLeft(3, '0')));
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(password.PadRight(16, ' ')));
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 7)));
            
            dataBuilder.AddRange(EncodeField("15"));
            dataBuilder.AddRange(EncodeField("V3"));
            dataBuilder.AddRange(EncodeField("26"));
            dataBuilder.AddRange(EncodeField(user));
            
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 1100);
        }

        private byte[] BuildRealTimeSubscriptionRequest()
        {
            var dataBuilder = new List<byte>();
            
            dataBuilder.Add((byte)'1'); // Ack
            dataBuilder.Add((byte)'1'); // Reject
            dataBuilder.Add((byte)'1'); // Exchange reject
            dataBuilder.Add((byte)'1'); // Trade execution
            dataBuilder.Add((byte)'1'); // Exchange message
            dataBuilder.Add((byte)'0'); // Default
            dataBuilder.Add((byte)'0'); // Inflected
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 11)));
            
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 2017);
        }

        private string GetOrderSideString(OrderSide side)
        {
            return side switch
            {
                OrderSide.Buy => "0",
                OrderSide.Sell => "1",
                _ => ""
            };
        }

        private string GetOrderModalityString(OrderModality modality)
        {
            return modality switch
            {
                OrderModality.Limit => "L",
                OrderModality.Market => "M",
                OrderModality.Stop => "S",
                OrderModality.Pegged => "P",
                _ => ""
            };
        }

        private string GetOrderValidityString(OrderValidity validity)
        {
            return validity switch
            {
                OrderValidity.Day => "J",
                OrderValidity.FOK => "K",
                OrderValidity.IOC => "E",
                OrderValidity.GTC => "G",
                _ => ""
            };
        }

        private byte[] BuildOrderRequest(
            string localCode,
            string glid,
            OrderSide side,
            long quantity,
            OrderModality modality,
            decimal price,
            OrderValidity validity,
            string clientReference,
            string internalReference,
            string clientCodeType,
            string clearingAccount,
            string allocationCode,
            string memo,
            string secondClientCodeType,
            string floorTraderId,
            string clientFreeField1,
            string currency)
        {
            var dataBuilder = new List<byte>();
            
            Debug.WriteLine($"[SLE] ========================================");
            Debug.WriteLine($"[SLE] Building order request with SEQUENTIAL bitmap filling");
            
            // === HEADER ===
            string userNum = _userNumber.PadLeft(5, '0');
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(userNum));
            Debug.WriteLine($"[SLE] User number: {userNum}");
            
            dataBuilder.Add((byte)'O'); // Request category = Simple order
            Debug.WriteLine($"[SLE] Request category: O");
            
            dataBuilder.Add((byte)'0'); // Command = New order
            Debug.WriteLine($"[SLE] Command: 0 (New)");
            
            // G: Stockcode - LOCAL CODE
            dataBuilder.AddRange(EncodeField(localCode));
            Debug.WriteLine($"[SLE] Stockcode (LOCAL CODE): {localCode}");
            
            // Filler (10 bajtów)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 10)));
            
            // === BITMAP - budujemy słownik pól ===
            Debug.WriteLine($"[SLE] Building bitmap dictionary:");
            var fields = new Dictionary<int, string>();
            
            // Field 0: Side (MANDATORY)
            fields[0] = GetOrderSideString(side);
            Debug.WriteLine($"[SLE] Field #0: Side = {fields[0]}");
            
            // Field 1: Quantity (MANDATORY)
            fields[1] = quantity.ToString(CultureInfo.InvariantCulture);
            Debug.WriteLine($"[SLE] Field #1: Quantity = {quantity}");
            
            // Field 2: Modality (MANDATORY)
            fields[2] = GetOrderModalityString(modality);
            Debug.WriteLine($"[SLE] Field #2: Modality = {fields[2]}");
            
            // Field 3: Price (conditional - tylko dla Limit orders)
            if (modality == OrderModality.Limit && price > 0)
            {
                fields[3] = price.ToString(CultureInfo.InvariantCulture);
                Debug.WriteLine($"[SLE] Field #3: Price = {price}");
            }
            
            // Field 4: Validity (MANDATORY)
            fields[4] = GetOrderValidityString(validity);
            Debug.WriteLine($"[SLE] Field #4: Validity = {fields[4]}");
            
            // Field 10: Client reference (optional)
            if (!string.IsNullOrEmpty(clientReference))
            {
                string clRef = clientReference.Substring(0, Math.Min(8, clientReference.Length));
                fields[10] = clRef;
                Debug.WriteLine($"[SLE] Field #10: Client Reference = {clRef}");
            }
            
            // Field 12: Internal reference (MANDATORY)
            string intRef = internalReference;
            if (string.IsNullOrEmpty(intRef))
            {
                intRef = $"ORD{DateTime.Now:yyyyMMddHHmmss}";
            }
            intRef = intRef.Substring(0, Math.Min(16, intRef.Length));
            //fields[12] = intRef;
            Debug.WriteLine($"[SLE] Field #12: Internal Reference = {intRef}");
            
            // Field 17: Client Code Type (MANDATORY dla WSE)
            fields[17] = clientCodeType;
            Debug.WriteLine($"[SLE] Field #17: Client Code Type = {clientCodeType} *** MANDATORY ***");
            
            // Field 19: Allocation Code
            if (!string.IsNullOrEmpty(allocationCode))
            {
                string allocCode = allocationCode.Substring(0, Math.Min(8, allocationCode.Length));
                fields[19] = allocCode;
                Debug.WriteLine($"[SLE] Field #19: Allocation Code = {allocCode}");
            }
            
            // Field 81: Memo (optional)7841 dla akcji i 7849 dla kontraktow
            if (!string.IsNullOrEmpty(memo))
            {
                string memoStr = memo.Substring(0, Math.Min(18, memo.Length));
                fields[81] = memoStr;
                Debug.WriteLine($"[SLE] Field #81: Memo = {memoStr}");
            }
            
            
            
            // Field 91: Application side not needed
            //fields[91] = "C"; // C = Client
            Debug.WriteLine($"[SLE] Field #91: Application side = C");
            
            // Field 92: Hour date station (timestamp)
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            fields[92] = timestamp;
            Debug.WriteLine($"[SLE] Field #92: Timestamp = {timestamp}");
            
            // Field 106: GLID (MANDATORY)
            string glidFormatted = glid.Length >= 12 
                ? glid.Substring(0, 12) 
                : glid.PadRight(12, ' ');
            fields[106] = glidFormatted;
            Debug.WriteLine($"[SLE] Field #106: GLID = {glidFormatted} *** MANDATORY ***");

            fields[112] = "0959";
            
            // Field 132: Clearing Account 1
            if (!string.IsNullOrEmpty(clearingAccount))
            {
                fields[132] = clearingAccount;
                Debug.WriteLine($"[SLE] Field #132: Clearing Account = {clearingAccount}");
            }
            
            // Field 147: Floor Trader ID (optional)
            if (!string.IsNullOrEmpty(floorTraderId))
            {
                fields[147] = floorTraderId;
                Debug.WriteLine($"[SLE] Field #147: Floor Trader ID = {floorTraderId}");
            }
            
            // Field 192: Currency (optional)
            if (!string.IsNullOrEmpty(currency))
            {
                fields[192] = currency;
                Debug.WriteLine($"[SLE] Field #192: Currency = {currency}");
            }
            
            string orderId = GenerateOrderId();
            string orderIdTrimmed = orderId.Substring(0, Math.Min(16, orderId.Length));
            fields[261] = orderIdTrimmed;
            Debug.WriteLine($"[SLE] Field #261: Order ID = {orderIdTrimmed} *** CLIENT IDENTIFIER ***");
            
            // // Field 306: Second Client Code Type (optional)
            // if (!string.IsNullOrEmpty(secondClientCodeType) && secondClientCodeType != " ")
            // {
            //     fields[306] = secondClientCodeType;
            //     Debug.WriteLine($"[SLE] Field #306: Second Client Code Type = {secondClientCodeType}");
            // }
            //
            // Field 317: Client Free Field 1 (optional)
            if (!string.IsNullOrEmpty(clientFreeField1))
            {
                string customField = clientFreeField1.Substring(0, Math.Min(16, clientFreeField1.Length));
                fields[317] = customField;
                Debug.WriteLine($"[SLE] Field #317: Client Free Field 1 = {customField}");
            }
            
            // Field 342: Capacity
            int capacity = 1;
            fields[342] = capacity.ToString(CultureInfo.InvariantCulture);
            Debug.WriteLine($"[SLE] Field #342: Capacity = {capacity}");

            
            
            int mifidInternIndic = 4;
            fields[574]=mifidInternIndic.ToString(CultureInfo.InvariantCulture);
            

            // Field 1449 Direct Electronic Access
            int dea = 2;
            fields[1449]=dea.ToString(CultureInfo.InvariantCulture);


            fields[1470] = "222386";

            String edmi = "222674";
            edmi="123321";
            fields[1482] = edmi;
            
            //3 i -1
            String eidmid = "4";
            fields[1483] = eidmid;
            
            
            // Field 1488 Execution Decision Maker Type 
            int edmt = 1;
            fields[1488]=edmt.ToString(CultureInfo.InvariantCulture);

            int idmt = 3;
            //fields[1489] = idmt.ToString(CultureInfo.InvariantCulture);
          
            
            // === SEKWENCYJNE WYPEŁNIANIE BITMAPY ===
            int maxFieldNumber = fields.Keys.Max();
            Debug.WriteLine($"[SLE] Max field number: {maxFieldNumber}");
            Debug.WriteLine($"[SLE] Building sequential bitmap from 0 to {maxFieldNumber}:");
            
            var bitmapFields = new List<byte>();
            int fillerCount = 0;
            
            for (int i = 0; i <= maxFieldNumber; i++)
            {
                if (fields.ContainsKey(i))
                {
                    // Pole ma wartość - koduj w GL
                    byte[] encoded = EncodeField(fields[i]);
                    bitmapFields.AddRange(encoded);
                    
                    if (i <= 20 || (i >= 90 && i <= 110) || i >= 300) // Loguj tylko istotne pola
                    {
                        Debug.WriteLine($"[SLE] Field #{i}: [{fields[i]}] → GL encoded ({encoded.Length} bytes)");
                    }
                }
                else
                {
                    // Pole puste - dodaj GL 0 (bajt 32)
                    bitmapFields.Add(32);
                    fillerCount++;
                }
            }
            
            Debug.WriteLine($"[SLE] Total fields: {maxFieldNumber + 1}, Filled: {fields.Count}, Fillers (GL 0): {fillerCount}");
            
            // Dodaj bitmap do dataBuilder
            dataBuilder.AddRange(bitmapFields);
            
            Debug.WriteLine($"[SLE] Bitmap size: {bitmapFields.Count} bytes");
            Debug.WriteLine($"[SLE] Total payload size: {dataBuilder.Count} bytes");
            
            var dataPayload = dataBuilder.ToArray();
            
            // Hex dump pierwszych 200 bajtów
            Debug.WriteLine($"[SLE] --- DATA PAYLOAD HEX (first 200 bytes) ---");
            for (int i = 0; i < Math.Min(1400, dataPayload.Length); i += 32)
            {
                int len = Math.Min(32, dataPayload.Length - i);
                string hex = BitConverter.ToString(dataPayload, i, len).Replace("-", " ");
                Debug.WriteLine($"[SLE] {i:D4}: {hex}");
            }
            
            // Hex dump końcówki (jeśli payload > 200 bajtów)
            if (dataPayload.Length > 200)
            {
                Debug.WriteLine($"[SLE] --- DATA PAYLOAD HEX (last 100 bytes) ---");
                int startPos = Math.Max(200, dataPayload.Length - 100);
                for (int i = startPos; i < dataPayload.Length; i += 32)
                {
                    int len = Math.Min(32, dataPayload.Length - i);
                    string hex = BitConverter.ToString(dataPayload, i, len).Replace("-", " ");
                    Debug.WriteLine($"[SLE] {i:D4}: {hex}");
                }
            }
            
            Debug.WriteLine($"[SLE] ========================================");
            
            return BuildMessage(dataPayload, 2000);
        }

        /// <summary>
        /// Buduje wiadomość FIS API z nagłówkiem, danymi i stopką
        /// </summary>
        private byte[] BuildMessage(byte[] dataPayload, int requestNumber)
        {
            int dataLength = dataPayload.Length;
            int totalLength = 2 + HeaderLength + dataLength + FooterLength;
            var message = new byte[totalLength];

            using (var ms = new System.IO.MemoryStream(message))
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // LG (2 bajty) - little endian
                writer.Write((byte)(totalLength % 256));
                writer.Write((byte)(totalLength / 256));
                
                // HEADER (32 bajty)
                writer.Write(Stx); // STX = 0x02
                

                
                
                writer.Write((byte)' ');
                
                
                // Length (5 bajtów ASCII)
                int contentLength = HeaderLength + dataLength + FooterLength;
                writer.Write(Encoding.ASCII.GetBytes(contentLength.ToString().PadLeft(5, '0')));
                
                // Called logical ID (5 bajtów) - server subnode
                writer.Write(Encoding.ASCII.GetBytes(_subnode.PadLeft(5, '0')));
                
                // Filler (5 bajtów)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 5)));
                
                // Calling logical ID (5 bajtów) - nasz assigned ID
                writer.Write(Encoding.ASCII.GetBytes(_assignedCallingId.PadLeft(5, '0')));
                
                // Filler (2 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                
                // Request number (5 bajtów)
                writer.Write(Encoding.ASCII.GetBytes(requestNumber.ToString().PadLeft(5, '0')));
                
                // Filler (3 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 3)));
                
                // DATA
                writer.Write(dataPayload);
                
                // FOOTER (3 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                writer.Write(Etx); // ETX = 0x03
            }
            
            // Hex dump dla weryfikacji nagłówka
            Debug.WriteLine($"[SLE] === MESSAGE HEADER HEX ===");
            string headerHex = BitConverter.ToString(message, 0, Math.Min(HeaderLength + 2, message.Length)).Replace("-", " ");
            Debug.WriteLine($"[SLE] {headerHex}");
            Debug.WriteLine($"[SLE] Position 3 (API Ver): 0x{message[3]:X2} = '{(char)message[3]}'");
            Debug.WriteLine($"[SLE] Position 26-30 (Request): {Encoding.ASCII.GetString(message, 26, 5)}");
            Debug.WriteLine($"[SLE] ==============================");
            
            return message;
        }

        /// <summary>
        /// Koduje pole w formacie GL
        /// Format: [LENGTH+32][DATA]
        /// </summary>
        private byte[] EncodeField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                // Dla pustego pola zwróć pojedynczą spację
                return new byte[] { 33, 32 }; // LENGTH=1+32=33, DATA=' '
            }
    
            var valueBytes = Encoding.ASCII.GetBytes(value);
            var encoded = new byte[valueBytes.Length + 1];
            encoded[0] = (byte)(valueBytes.Length + 32);
            Array.Copy(valueBytes, 0, encoded, 1, valueBytes.Length);
    
            return encoded;
        }
        
        /// <summary>
        /// Dekoduje pole w formacie GL
        /// Zwraca (wartość, ilość odczytanych bajtów)
        /// </summary>
        private (string value, int bytesRead) DecodeGLField(byte[] data, int startPos)
        {
            // Sprawdź czy nie wykraczamy poza bufor
            if (startPos >= data.Length)
                return (string.Empty, 0);
    
            // Odczytaj długość pola (bajt - 32)
            int length = data[startPos] - 32;
    
            // KRYTYCZNA POPRAWKA: Puste pole (GL 0) zajmuje 1 bajt!
            if (length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SLE] DecodeGLField at {startPos}: EMPTY (GL 0)");
                return (string.Empty, 1);
            }
    
            // Niepoprawna długość lub przekroczenie bufora
            if (length < 0 || startPos + 1 + length > data.Length)
            {
                System.Diagnostics.Debug.WriteLine($"[SLE] DecodeGLField at {startPos}: INVALID length={length}");
                return (string.Empty, 0);
            }
    
            // Odczytaj wartość pola
            string value = Encoding.ASCII.GetString(data, startPos + 1, length);
            return (value, 1 + length);
        }

        private bool VerifyLoginResponse(byte[] response, int length)
        {
            if (length < HeaderLength + 2) return false;
            
            try
            {
                string requestNumberStr = Encoding.ASCII.GetString(response, 26, 5);
                
                if (requestNumberStr == "01100")
                {
                    // W odpowiedziach OD serwera: Called=klient(my), Calling=serwer
                    string assignedCallingId = Encoding.ASCII.GetString(response, 9, 5);
                    _assignedCallingId = assignedCallingId.Trim();
                    
                    Debug.WriteLine($"[SLE] Login successful - assigned calling ID: {_assignedCallingId}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Error verifying login response: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region orderBook

        public async Task<bool> RequestOrderBookAsync()
        {
            if (!IsConnected || _stream == null)
            {
                System.Diagnostics.Debug.WriteLine("[SLE] Cannot request order book - not connected");
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[SLE] === SENDING REQUEST 2004 (ORDER BOOK) ===");
                
                byte[] request = BuildOrderBookRequest();
                
                await _stream.WriteAsync(request, 0, request.Length);
                await _stream.FlushAsync();
                
                System.Diagnostics.Debug.WriteLine("[SLE] Request 2004 sent - waiting for order book data");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SLE] Failed to request order book: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Buduje request 2004 - Order Book Consultation
        /// </summary>
        private byte[] BuildOrderBookRequest()
        {
            var dataBuilder = new List<byte>();
            
            // B: User number (5 bytes)
            string userNum = _userNumber.PadLeft(5, '0');
            dataBuilder.AddRange(System.Text.Encoding.ASCII.GetBytes(userNum));
            System.Diagnostics.Debug.WriteLine($"[SLE] User number: {userNum}");
            
            // C: Request category (1 byte) - 'O' for Simple order
            dataBuilder.Add((byte)'O');
            System.Diagnostics.Debug.WriteLine($"[SLE] Request category: O");
            
            // D2: Question (1 byte) - '2' = N responses starting from the beginning
            dataBuilder.Add((byte)'2');
            System.Diagnostics.Debug.WriteLine($"[SLE] Question type: 2 (all from beginning)");
            
            // Filler (7 bytes)
            dataBuilder.AddRange(System.Text.Encoding.ASCII.GetBytes(new string(' ', 7)));
            
            // G: Stockcode (GL encoded) - puste dla wszystkich instrumentów
            dataBuilder.Add(32); // GL 0 (empty)
            System.Diagnostics.Debug.WriteLine($"[SLE] Stockcode: empty (all instruments)");
            
            // E: Index (6 bytes) - "000000" dla początku
            dataBuilder.AddRange(System.Text.Encoding.ASCII.GetBytes("000000"));
            System.Diagnostics.Debug.WriteLine($"[SLE] Index: 000000");
            
            // F: Number of replies (5 bytes) - "00100" (100 zleceń)
            dataBuilder.AddRange(System.Text.Encoding.ASCII.GetBytes("00100"));
            System.Diagnostics.Debug.WriteLine($"[SLE] Number of replies: 100");
            
            // Filler (20 bytes)
            dataBuilder.AddRange(System.Text.Encoding.ASCII.GetBytes(new string(' ', 20)));
            
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 2004);
        }

        /// <summary>
        /// Parsuje odpowiedź 2004 - Order Book Reply
        /// </summary>
        private void ProcessOrderBookReply(byte[] response, int length, int stxPos)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[SLE] === PROCESSING ORDER BOOK REPLY (2004) ===");
                System.Diagnostics.Debug.WriteLine($"[SLE] Total message length: {length}");
                System.Diagnostics.Debug.WriteLine($"[SLE] STX position: {stxPos}");
                
                // HEX DUMP całej wiadomości
                System.Diagnostics.Debug.WriteLine("[SLE] === FULL MESSAGE HEX DUMP ===");
                for (int i = 0; i < Math.Min(length, 500); i++)
                {
                    if (i % 32 == 0)
                        System.Diagnostics.Debug.Write($"\n[{i:D4}] ");
                    System.Diagnostics.Debug.Write($"{response[i]:X2} ");
                }
                System.Diagnostics.Debug.WriteLine("\n[SLE] === END HEX DUMP ===");
                
                int pos = stxPos + HeaderLength;
                
                // A: Chaining
                char chaining = (char)response[pos++];
                System.Diagnostics.Debug.WriteLine($"[SLE] Chaining: '{chaining}' (0x{((byte)chaining):X2})");
                
                // B: User number (5 bytes)
                string userNum = System.Text.Encoding.ASCII.GetString(response, pos, 5);
                pos += 5;
                System.Diagnostics.Debug.WriteLine($"[SLE] User number: [{userNum}]");
                
                // C: Request category (1 byte)
                char requestCategory = (char)response[pos++];
                System.Diagnostics.Debug.WriteLine($"[SLE] Request category: '{requestCategory}' (0x{((byte)requestCategory):X2})");
                
                // Filler (1 byte) - WAŻNE: To NIE jest Reply Type!
                byte fillerByte = response[pos++];
                System.Diagnostics.Debug.WriteLine($"[SLE] Filler byte: 0x{fillerByte:X2}");
                
                // E: Index (6 bytes)
                string index = System.Text.Encoding.ASCII.GetString(response, pos, 6);
                pos += 6;
                System.Diagnostics.Debug.WriteLine($"[SLE] Index: [{index}]");
                
                // F: Number of replies (5 bytes)
                string numRepliesStr = System.Text.Encoding.ASCII.GetString(response, pos, 5);
                pos += 5;
                int numReplies = int.TryParse(numRepliesStr.Trim(), out int nr) ? nr : 0;
                System.Diagnostics.Debug.WriteLine($"[SLE] Number of replies: {numReplies}");
                
                // G: Stockcode (GL encoded)
                System.Diagnostics.Debug.WriteLine($"[SLE] About to decode Stockcode at position {pos}");
                var (stockcode, bytesRead) = DecodeGLField(response, pos);
                pos += bytesRead;
                System.Diagnostics.Debug.WriteLine($"[SLE] Stockcode: [{stockcode}] (read {bytesRead} bytes)");
                
                // Filler (10 bytes)
                System.Diagnostics.Debug.WriteLine($"[SLE] Filler 10 bytes at position {pos}:");
                System.Diagnostics.Debug.Write("[SLE] ");
                for (int i = 0; i < 10 && pos + i < response.Length; i++)
                {
                    System.Diagnostics.Debug.Write($"{response[pos + i]:X2} ");
                }
                System.Diagnostics.Debug.WriteLine("");
                pos += 10;
                
                // Data for order rozpoczyna się tutaj
                System.Diagnostics.Debug.WriteLine($"[SLE] === DATA FOR ORDER starts at position {pos} ===");
                System.Diagnostics.Debug.WriteLine($"[SLE] Remaining bytes: {length - FooterLength - pos}");
                
                // HEX DUMP Data for order (pierwsze 200 bajtów)
                System.Diagnostics.Debug.WriteLine("[SLE] === DATA FOR ORDER HEX DUMP (first 200 bytes) ===");
                int dataStart = pos;
                int dataEnd = Math.Min(dataStart + 200, length - FooterLength);
                for (int i = dataStart; i < dataEnd; i++)
                {
                    if ((i - dataStart) % 32 == 0)
                        System.Diagnostics.Debug.Write($"\n[{i - dataStart:D4}] ");
                    System.Diagnostics.Debug.Write($"{response[i]:X2} ");
                }
                System.Diagnostics.Debug.WriteLine("\n[SLE] === END DATA HEX DUMP ===");
                
                // Parsuj Order Data
                var order = ParseOrderData(response, pos, length - FooterLength, stockcode);
                
                if (order != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SLE] === SUCCESSFULLY PARSED ORDER ===");
                    System.Diagnostics.Debug.WriteLine($"  OrderID: [{order.OrderId}]");
                    System.Diagnostics.Debug.WriteLine($"  SleReference: [{order.SleReference}]");
                    System.Diagnostics.Debug.WriteLine($"  ExchangeNumber: [{order.ExchangeNumber}]");
                    System.Diagnostics.Debug.WriteLine($"  Side: {order.Side}");
                    System.Diagnostics.Debug.WriteLine($"  Quantity: {order.Quantity}");
                    System.Diagnostics.Debug.WriteLine($"  ExecutedQty: {order.ExecutedQuantity}");
                    System.Diagnostics.Debug.WriteLine($"  RemainingQty: {order.RemainingQuantity}");
                    System.Diagnostics.Debug.WriteLine($"  Status: {order.Status}");
                    System.Diagnostics.Debug.WriteLine($"  OrderTime: {order.OrderTime}");
                    System.Diagnostics.Debug.WriteLine($"  Price: {order.Price}");
                    System.Diagnostics.Debug.WriteLine($"  AvgPrice: {order.AveragePrice}");
                    
                    // Wyślij do Order Book
                    if (chaining == '0')
                    {
                        var orders = new List<Order> { order };
                        _ = Task.Run(() => OrderBookReceived?.Invoke(orders));
                    }
                    else
                    {
                        var orders = new List<Order> { order };
                        _ = Task.Run(() => OrderBookReceived?.Invoke(orders));
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SLE] ❌ ORDER PARSING RETURNED NULL");
                }
                
                System.Diagnostics.Debug.WriteLine("[SLE] === ORDER BOOK REPLY COMPLETE ===\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SLE] ❌ Failed to parse order book reply: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SLE] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Parsuje dane zlecenia z bitmapy i tworzy obiekt Order
        /// </summary>
        private Order? ParseOrderData(byte[] response, int startPos, int endPos, string stockcode)
        {
            try
            {
                var order = new Order();
                order.LocalCode = stockcode;
                order.Instrument = stockcode;
                
                int pos = startPos;
                var bitmapFields = new Dictionary<int, string>();
                int fieldNumber = 0;
                
                System.Diagnostics.Debug.WriteLine($"[SLE] === PARSING ORDER DATA SEQUENTIALLY ===");
                System.Diagnostics.Debug.WriteLine($"[SLE] Start pos: {startPos}, End pos: {endPos}");
                System.Diagnostics.Debug.WriteLine($"[SLE] Total bytes to parse: {endPos - startPos}");
                
                while (pos < endPos)
                {
                    // Sprawdź czy nie dotarliśmy do footera
                    if (pos + FooterLength <= endPos && response[pos + 2] == Etx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SLE] Reached footer at position {pos}");
                        break;
                    }
                    
                    // Sprawdź czy nie przekroczyliśmy rozmiaru
                    if (pos >= endPos)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SLE] Reached end of data at position {pos}");
                        break;
                    }
                    
                    // Decode GL field
                    int posBeforeDecode = pos;
                    var (fieldValue, bytesRead) = DecodeGLField(response, pos);
                    pos += bytesRead;
                    
                    // Debug dla każdego pola
                    if (fieldNumber < 50 || !string.IsNullOrEmpty(fieldValue))
                    {
                        if (!string.IsNullOrEmpty(fieldValue))
                        {
                            System.Diagnostics.Debug.WriteLine($"[SLE] Field #{fieldNumber}: POS={posBeforeDecode}, LENGTH={bytesRead}, VALUE=[{fieldValue}]");
                            bitmapFields[fieldNumber] = fieldValue;
                        }
                        else
                        {
                            // Pole puste (GL 0)
                            if (fieldNumber < 20)
                                System.Diagnostics.Debug.WriteLine($"[SLE] Field #{fieldNumber}: EMPTY (GL 0)");
                        }
                    }
                    
                    fieldNumber++;
                    
                    // Zabezpieczenie
                    if (fieldNumber > 2000)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SLE] ⚠️ Warning: Too many fields (>{fieldNumber}), breaking");
                        break;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[SLE] === PARSING COMPLETE ===");
                System.Diagnostics.Debug.WriteLine($"[SLE] Total fields decoded: {fieldNumber}");
                System.Diagnostics.Debug.WriteLine($"[SLE] Non-empty fields: {bitmapFields.Count}");
                
                // Wyświetl wszystkie niepuste pola
                System.Diagnostics.Debug.WriteLine($"[SLE] === ALL NON-EMPTY FIELDS ===");
                foreach (var kvp in bitmapFields.OrderBy(x => x.Key))
                {
                    System.Diagnostics.Debug.WriteLine($"  Field #{kvp.Key}: [{kvp.Value}]");
                }
                System.Diagnostics.Debug.WriteLine($"[SLE] ==============================");
                
                // Mapuj do Order
                MapBitmapToOrder(order, bitmapFields);
                
                return order;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SLE] ❌ Failed to parse order data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SLE] Stack trace: {ex.StackTrace}");
                return null;
            }
        }



        /// <summary>
        /// Mapuje pola bitmapy do właściwości obiektu Order
        /// UWAGA: Numery pól według dokumentacji FIS Trading API WSE
        /// </summary>
        private void MapBitmapToOrder(Order order, Dictionary<int, string> fields)
        {
            // === PODSTAWOWE POLA ZLECENIA ===
            
            // Field 0: Side (MANDATORY)
            if (fields.ContainsKey(0))
                order.SetSide(fields[0]);
            
            // Field 1: Quantity (MANDATORY)
            if (fields.ContainsKey(1) && long.TryParse(fields[1], out long qty))
                order.Quantity = qty;
            
            // Field 2: Modality (MANDATORY)
            if (fields.ContainsKey(2))
                order.SetModality(fields[2]);
            
            // Field 3: Price (conditional - dla Limit orders)
            if (fields.ContainsKey(3) && decimal.TryParse(fields[3], 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal price))
                order.Price = price;
            
            // Field 4: Validity (MANDATORY)
            if (fields.ContainsKey(4))
                order.SetValidity(fields[4]);
            
            // Field 5: Expiry date
            // TODO: Implement if needed
            
            // Field 8: Minimum quantity
            if (fields.ContainsKey(8) && long.TryParse(fields[8], out long minQty))
                order.MinimumQuantity = minQty;
            
            // Field 9: Displayed quantity (Iceberg orders)
            if (fields.ContainsKey(9) && long.TryParse(fields[9], out long dispQty))
                order.DisplayedQuantity = dispQty;
            
            // === POLA KLIENTA ===
            
            // Field 10: Client reference (max 8 chars)
            if (fields.ContainsKey(10))
                order.ClientReference = fields[10];
            
            // Field 12: Internal reference (max 16 chars)
            if (fields.ContainsKey(12))
                order.InternalReference = fields[12];
            
            // Field 13: Exchange number - NUMER ZLECENIA NA GIEŁDZIE (KRYTYCZNE!)
            if (fields.ContainsKey(13))
                order.ExchangeNumber = fields[13];
            
            // Field 17: Client Code Type
            if (fields.ContainsKey(17))
                order.ClientCodeType = fields[17];
            
            // Field 19: Allocation Code
            if (fields.ContainsKey(19))
                order.AllocationCode = fields[19];
            
            // === POLA STATUSU I CZASU ===
            
            // Field 30: Order status (from field 30, not reply type!)
            if (fields.ContainsKey(30))
                order.SetStatus(fields[30]);
            
            // Field 36: Order time - FORMAT: YYYYMMDDHHMMSS
            if (fields.ContainsKey(36))
            {
                var orderTime = Order.ParseOrderDateTime(fields[36]);
                if (orderTime.HasValue)
                    order.OrderTime = orderTime.Value;
            }
            
            // Field 37: Remain quantity (z serwera, bardziej dokładne niż kalkulacja lokalna)
            if (fields.ContainsKey(37) && long.TryParse(fields[37], out long remainQty))
                order.RemainingQuantity = remainQty;
            
            // Field 38: Number of executions
            if (fields.ContainsKey(38) && int.TryParse(fields[38], out int numExec))
                order.NumberOfExecutions = numExec;
            
            // Field 40: Average price - POPRAWNY NUMER (nie 102!)
            if (fields.ContainsKey(40) && decimal.TryParse(fields[40], 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal avgPrice))
                order.AveragePrice = avgPrice;
            
            // Field 41: Order status (alternatywna reprezentacja)
            // Jeśli field 30 nie jest dostępne, użyj field 41
            if (!fields.ContainsKey(30) && fields.ContainsKey(41))
                order.SetStatus(fields[41]);
            
            // Field 42: SLE reference
            if (fields.ContainsKey(42))
                order.SleReference = fields[42];
            
            // Field 44: Total executed quantity - POPRAWNY NUMER (nie 101!)
            if (fields.ContainsKey(44) && long.TryParse(fields[44], out long totalExecQty))
                order.ExecutedQuantity = totalExecQty;
            
            // === POLA WYKONANIA (EXECUTION) ===
            
            // Field 48: Execution price (cena pojedynczej egzekucji)
            if (fields.ContainsKey(48) && decimal.TryParse(fields[48], 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out decimal execPrice))
                order.ExecutionPrice = execPrice;
            
            // Field 49: Execution quantity (ilość pojedynczej egzekucji)
            // Uwaga: To jest quantity dla POJEDYNCZEJ egzekucji, nie całkowita!
            // Całkowita wykonana ilość to field 44
            
            // Field 51: Exchange trade number
            if (fields.ContainsKey(51))
                order.TradeNumber = fields[51];
            
            // Field 52: Trade time - FORMAT: YYYYMMDDHHMMSS
            if (fields.ContainsKey(52))
            {
                var tradeTime = Order.ParseOrderDateTime(fields[52]);
                if (tradeTime.HasValue)
                    order.TradeTime = tradeTime;
            }
            
            // Field 53: Trade type
            if (fields.ContainsKey(53))
                order.TradeType = fields[53];
            
            // Field 57: Acknowledgement type
            if (fields.ContainsKey(57))
                order.AcknowledgementType = fields[57];
            
            // === POLA ODRZUCENIA ===
            
            // Field 62: Index (SLE database index - NIE CZAS!)
            if (fields.ContainsKey(62))
                order.SleIndex = fields[62];
            
            // Field 64: Reject type
            if (fields.ContainsKey(64))
                order.RejectType = fields[64];
            
            // Field 65: Rejects code (reject reason)
            if (fields.ContainsKey(65))
                order.RejectReason = fields[65];
            
            // Field 66: Rejects time - FORMAT: YYYYMMDDHHMMSS
            if (fields.ContainsKey(66))
            {
                var rejectTime = Order.ParseOrderDateTime(fields[66]);
                if (rejectTime.HasValue)
                    order.RejectTime = rejectTime;
            }
            
            // Field 67: Rejected command type
            if (fields.ContainsKey(67))
                order.RejectedCommandType = fields[67];
            
            // === POLA DODATKOWE ===
            
            // Field 81: Memo
            if (fields.ContainsKey(81))
                order.Memo = fields[81];
            
            // Field 106: GLID
            if (fields.ContainsKey(106))
                order.GLID = fields[106];
            
            // Field 132: Clearing Account
            if (fields.ContainsKey(132))
                order.ClearingAccount = fields[132];
            
            // Field 147: Floor Trader ID
            if (fields.ContainsKey(147))
                order.FloorTraderId = fields[147];
            
            // Field 192: Currency
            if (fields.ContainsKey(192))
                order.Currency = fields[192];
            
            // Field 261: Order ID (Client identifier, max 16 chars)
            if (fields.ContainsKey(261))
                order.OrderId = fields[261];
            
            // Ustaw czas ostatniej aktualizacji
            order.LastUpdateTime = DateTime.Now;
            
            // Debug log
            System.Diagnostics.Debug.WriteLine($"[SLE] Mapped order:");
            System.Diagnostics.Debug.WriteLine($"  OrderID: {order.OrderId}");
            System.Diagnostics.Debug.WriteLine($"  ExchangeNumber: {order.ExchangeNumber}");
            System.Diagnostics.Debug.WriteLine($"  SleReference: {order.SleReference}");
            System.Diagnostics.Debug.WriteLine($"  Side: {order.Side}");
            System.Diagnostics.Debug.WriteLine($"  Quantity: {order.Quantity}");
            System.Diagnostics.Debug.WriteLine($"  ExecutedQty: {order.ExecutedQuantity}");
            System.Diagnostics.Debug.WriteLine($"  Status: {order.Status}");
            System.Diagnostics.Debug.WriteLine($"  OrderTime: {order.OrderTime}");
            System.Diagnostics.Debug.WriteLine($"  Price: {order.Price}");
            System.Diagnostics.Debug.WriteLine($"  AvgPrice: {order.AveragePrice}");
        }

        /// <summary>
        /// Obsługuje real-time update zlecenia (2019) i przekazuje do Order Book
        /// </summary>
        private void HandleOrderUpdate(Dictionary<int, string> bitmapFields, string stockcode, char replyType)
        {
            try
            {
                var order = new Order();
                order.LocalCode = stockcode;
                order.Instrument = stockcode;
                
                MapBitmapToOrder(order, bitmapFields);
                
                string statusFromReply = replyType switch
                {
                    'A' => "A", // Accepted
                    'C' => "C", // Rejected
                    'G' => "C", // GL Reject
                    'R' => "E", // Executed
                    _ => null
                };

                if (statusFromReply != null)
                {
                    order.SetStatus(statusFromReply);
                }
                
                _ = Task.Run(() => OrderUpdated?.Invoke(order));
                
                System.Diagnostics.Debug.WriteLine($"[SLE] Order update broadcasted - ID: {order.OrderId}, Status: {order.Status}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SLE] Failed to handle order update: {ex.Message}");
            }
        }
        
        public async Task<bool> ModifyOrder(
            string exchangeNumber,
            string localCode,
            string glid,
            long? newQuantity = null,
            decimal? newPrice = null,
            OrderValidity? newValidity = null,
            string clientReference = "",
            string internalReference = "",
            string clientCodeType = "",
            string clearingAccount = "",
            string allocationCode = "",
            string memo = "")
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[SLE] Cannot modify order - not connected");
                return false;
            }

            if (string.IsNullOrEmpty(exchangeNumber))
            {
                Debug.WriteLine("[SLE] ✗ Cannot modify order - ExchangeNumber is required!");
                return false;
            }

            try
            {
                Debug.WriteLine($"[SLE] ========================================");
                Debug.WriteLine($"[SLE] MODIFYING ORDER");
                Debug.WriteLine($"[SLE] ExchangeNumber: {exchangeNumber}");
                Debug.WriteLine($"[SLE] LocalCode: {localCode}");
                Debug.WriteLine($"[SLE] GLID: {glid}");
                if (newQuantity.HasValue)
                    Debug.WriteLine($"[SLE] New Quantity: {newQuantity.Value}");
                if (newPrice.HasValue)
                    Debug.WriteLine($"[SLE] New Price: {newPrice.Value}");
                if (newValidity.HasValue)
                    Debug.WriteLine($"[SLE] New Validity: {newValidity.Value}");

                byte[] modifyRequest = BuildModifyOrderRequest(
                    exchangeNumber, localCode, glid, newQuantity, newPrice, newValidity,
                    clientReference, internalReference, clientCodeType, clearingAccount,
                    allocationCode, memo);

                await _stream.WriteAsync(modifyRequest, 0, modifyRequest.Length);
                await _stream.FlushAsync();

                Debug.WriteLine($"[SLE] ✓ Order modification sent successfully");
                Debug.WriteLine($"[SLE] ========================================");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] ✗ Failed to modify order: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Anuluje istniejące zlecenie (Command = 1)
        /// WYMAGA: ExchangeNumber (field 13) - MANDATORY
        /// </summary>
        public async Task<bool> CancelOrder(
            string exchangeNumber,
            string localCode,
            string glid,
            string clientReference = "",
            string internalReference = "")
        {
            if (!IsConnected)
            {
                Debug.WriteLine("[SLE] Cannot cancel order - not connected");
                return false;
            }

            if (string.IsNullOrEmpty(exchangeNumber))
            {
                Debug.WriteLine("[SLE] ✗ Cannot cancel order - ExchangeNumber is required!");
                return false;
            }

            try
            {
                Debug.WriteLine($"[SLE] ========================================");
                Debug.WriteLine($"[SLE] CANCELLING ORDER");
                Debug.WriteLine($"[SLE] ExchangeNumber: {exchangeNumber}");
                Debug.WriteLine($"[SLE] LocalCode: {localCode}");
                Debug.WriteLine($"[SLE] GLID: {glid}");

                byte[] cancelRequest = BuildCancelOrderRequest(
                    exchangeNumber, localCode, glid, clientReference, internalReference);

                await _stream.WriteAsync(cancelRequest, 0, cancelRequest.Length);
                await _stream.FlushAsync();

                Debug.WriteLine($"[SLE] ✓ Order cancellation sent successfully");
                Debug.WriteLine($"[SLE] ========================================");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] ✗ Failed to cancel order: {ex.Message}");
                return false;
            }
        }

        private byte[] BuildModifyOrderRequest(
            string exchangeNumber,
            string localCode,
            string glid,
            long? newQuantity,
            decimal? newPrice,
            OrderValidity? newValidity,
            string clientReference,
            string internalReference,
            string clientCodeType,
            string clearingAccount,
            string allocationCode,
            string memo)
        {
            var dataBuilder = new List<byte>();

            // User Number (B) - 5 ASCII digits
            string userStr = _userNumber.PadLeft(5, '0');
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(userStr));

            // Request Category (C) - 1 ASCII char - 'O' = Simple order
            dataBuilder.Add((byte)'O');

            // Command (D1) - 1 ASCII digit - '2' = Update/Modify order
            dataBuilder.Add((byte)'2');

            // Stockcode (G) - GL encoded (LocalCode/ISIN)
            byte[] stockcodeEncoded = EncodeField(localCode);
            dataBuilder.AddRange(stockcodeEncoded);

            // Filler - 10 bytes (FILLER type = wszystkie 32)
            for (int i = 0; i < 10; i++)
                dataBuilder.Add(32);

            // === BITMAP ===
            var fields = new Dictionary<int, string>();

            // Field 13: Exchange number - MANDATORY dla modyfikacji!
            fields[13] = exchangeNumber;

            // Field 1: Quantity - tylko jeśli nowa wartość
            if (newQuantity.HasValue)
                fields[1] = newQuantity.Value.ToString(CultureInfo.InvariantCulture);

            // Field 3: Price - tylko jeśli nowa wartość
            if (newPrice.HasValue)
                fields[3] = newPrice.Value.ToString("F2", CultureInfo.InvariantCulture);

            // Field 4: Validity - tylko jeśli nowa wartość
            if (newValidity.HasValue)
                fields[4] = GetOrderValidityString(newValidity.Value);

            // Field 10: Client reference
            if (!string.IsNullOrEmpty(clientReference))
                fields[10] = clientReference;

            // Field 12: Internal reference
            if (!string.IsNullOrEmpty(internalReference))
                fields[12] = internalReference;
            else
                fields[12] = GenerateOrderId(); // Fallback

            // Field 17: Client Code Type
            if (!string.IsNullOrEmpty(clientCodeType))
                fields[17] = clientCodeType;

            // Field 19: Allocation Code
            if (!string.IsNullOrEmpty(allocationCode))
                fields[19] = allocationCode;

            // Field 81: Memo
            if (!string.IsNullOrEmpty(memo))
                fields[81] = memo;

            // Field 106: GLID - MANDATORY
            fields[106] = glid;

            // Field 132: Clearing Account
            if (!string.IsNullOrEmpty(clearingAccount))
                fields[132] = clearingAccount;

            // === SEKWENCYJNE WYPEŁNIANIE BITMAPY ===
            int maxFieldNumber = fields.Keys.Max();
            Debug.WriteLine($"[SLE] MODIFY - Max field number: {maxFieldNumber}");

            var bitmapFields = new List<byte>();

            for (int i = 0; i <= maxFieldNumber; i++)
            {
                if (fields.ContainsKey(i))
                {
                    byte[] encoded = EncodeField(fields[i]);
                    bitmapFields.AddRange(encoded);

                    if (i == 13 || i == 1 || i == 3 || i == 4)
                        Debug.WriteLine($"[SLE] MODIFY Field #{i}: [{fields[i]}]");
                }
                else
                {
                    bitmapFields.Add(32); // GL 0
                }
            }

            dataBuilder.AddRange(bitmapFields);

            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 2000);
        }

        private byte[] BuildCancelOrderRequest(
            string exchangeNumber,
            string localCode,
            string glid,
            string clientReference,
            string internalReference)
        {
            var dataBuilder = new List<byte>();

            // User Number (B) - 5 ASCII digits
            string userStr = _userNumber.PadLeft(5, '0');
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(userStr));

            // Request Category (C) - 1 ASCII char - 'O' = Simple order
            dataBuilder.Add((byte)'O');

            // Command (D1) - 1 ASCII digit - '1' = Cancel order
            dataBuilder.Add((byte)'1');

            // Stockcode (G) - GL encoded (LocalCode/ISIN)
            byte[] stockcodeEncoded = EncodeField(localCode);
            dataBuilder.AddRange(stockcodeEncoded);

            // Filler - 10 bytes
            for (int i = 0; i < 10; i++)
                dataBuilder.Add(32);

            // === BITMAP ===
            var fields = new Dictionary<int, string>();

            // Field 13: Exchange number - MANDATORY dla cancelowania!
            fields[13] = exchangeNumber;

            // Field 10: Client reference
            if (!string.IsNullOrEmpty(clientReference))
                fields[10] = clientReference;

            // Field 12: Internal reference
            if (!string.IsNullOrEmpty(internalReference))
                fields[12] = internalReference;
            else
                fields[12] = GenerateOrderId();

            // Field 106: GLID - MANDATORY
            fields[106] = glid;

            // === SEKWENCYJNE WYPEŁNIANIE BITMAPY ===
            int maxFieldNumber = fields.Keys.Max();
            Debug.WriteLine($"[SLE] CANCEL - Max field number: {maxFieldNumber}");

            var bitmapFields = new List<byte>();

            for (int i = 0; i <= maxFieldNumber; i++)
            {
                if (fields.ContainsKey(i))
                {
                    byte[] encoded = EncodeField(fields[i]);
                    bitmapFields.AddRange(encoded);

                    if (i == 13 || i == 106)
                        Debug.WriteLine($"[SLE] CANCEL Field #{i}: [{fields[i]}]");
                }
                else
                {
                    bitmapFields.Add(32); // GL 0
                }
            }

            dataBuilder.AddRange(bitmapFields);

            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 2000);
        }

        #endregion
    }

    public class OrderReply
    {
        public string OrderId { get; set; } = string.Empty;
        public OrderStatus Status { get; set; } = OrderStatus.Unknown;
        public string Message { get; set; } = string.Empty;
        public decimal ExecutedPrice { get; set; }
        public long ExecutedQuantity { get; set; }
    }
}