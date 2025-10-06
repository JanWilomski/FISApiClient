using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Cross_FIS_API_1._2.Models
{
    public class SleConnectionService
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private string _node = string.Empty;
        private string _subnode = string.Empty;
        private string _userNumber = string.Empty;
        private string _assignedCallingId = "00000"; // Numer przydzielony przez serwer podczas logowania

        private const byte Stx = 2;
        private const byte Etx = 3;
        private const int HeaderLength = 32;
        private const int FooterLength = 3;

        public bool IsConnected => _tcpClient?.Connected ?? false;
        
        // Eventy dla odpowiedzi z SLE
        public event Action<OrderReply>? OrderReplyReceived;
        public event Action<string>? OrderRejected;
        public event Action<string>? OrderAccepted;
        
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
        /// Format zgodny z FIS API Manual
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
                Debug.WriteLine($"[SLE] Subscription data length: {subscriptionRequest.Length} bytes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Failed to subscribe to real-time replies: {ex.Message}");
            }
        }

        /// <summary>
        /// Wysyła nowe zlecenie (request 2000)
        /// </summary>
        /// <param name="glidAndSymbol">GLID + Symbol instrumentu</param>
        /// <param name="side">0=Buy, 1=Sell</param>
        /// <param name="quantity">Ilość</param>
        /// <param name="modality">L=Limit, B=At best, M=Market</param>
        /// <param name="price">Cena (dla limit orders)</param>
        /// <param name="validity">J=Day, K=FOK, E=E&E</param>
        public async Task<bool> SendOrderAsync(
            string glidAndSymbol, 
            int side, 
            long quantity, 
            string modality, 
            decimal price = 0, 
            string validity = "J",
            string clientReference = "784",
            string internalReference = "",
            // Twoje dane z FIS Workstation:
            string clientCodeType = "C",        // Origin: Client (MANDATORY)
            string clearingAccount = "0100",    // Clearing Account
            string allocationCode = "0955",     // Allocation receptor (Clearing Firm)
            string memo = "7841",               // Memo
            string secondClientCodeType = "B",  // Originator Origin: External B (Broker)
            string floorTraderId = "0955",      // Own Broker D
            string clientFreeField1 = "100",    // Custom
            string currency = "PLN")            // Currency
        {
            if (!IsConnected || _stream == null)
            {
                Debug.WriteLine("[SLE] Cannot send order - not connected");
                return false;
            }

            if (!_realtimeSubscribed)
            {
                Debug.WriteLine("[SLE] Cannot send order - not subscribed to real-time replies");
                return false;
            }

            try
            {
                Debug.WriteLine($"[SLE] ========================================");
                Debug.WriteLine($"[SLE] === SENDING ORDER (Request 2000) ===");
                Debug.WriteLine($"[SLE] ========================================");
                Debug.WriteLine($"[SLE] Instrument: {glidAndSymbol}");
                Debug.WriteLine($"[SLE] Side: {(side == 0 ? "BUY" : "SELL")}");
                Debug.WriteLine($"[SLE] Quantity: {quantity}");
                Debug.WriteLine($"[SLE] Modality: {modality}");
                Debug.WriteLine($"[SLE] Price: {price}");
                Debug.WriteLine($"[SLE] Validity: {validity}");
                Debug.WriteLine($"[SLE] --- FIS Workstation Parameters ---");
                Debug.WriteLine($"[SLE] Client Code Type (Origin): {clientCodeType}");
                Debug.WriteLine($"[SLE] Clearing Account: {clearingAccount}");
                Debug.WriteLine($"[SLE] Allocation Code: {allocationCode}");
                Debug.WriteLine($"[SLE] Floor Trader ID: {floorTraderId}");
                Debug.WriteLine($"[SLE] Client Reference: {clientReference}");
                Debug.WriteLine($"[SLE] Memo: {memo}");
                Debug.WriteLine($"[SLE] Second Client Code Type: {secondClientCodeType}");
                Debug.WriteLine($"[SLE] Custom Field: {clientFreeField1}");
                Debug.WriteLine($"[SLE] Currency: {currency}");

                byte[] orderRequest = BuildOrderRequest(
                    glidAndSymbol, 
                    side, 
                    quantity, 
                    modality, 
                    price, 
                    validity,
                    clientReference,
                    internalReference,
                    clientCodeType,
                    clearingAccount,
                    allocationCode,
                    memo,
                    secondClientCodeType,
                    floorTraderId,
                    clientFreeField1,
                    currency);

                // Wyświetl hex dump dla debugowania
                Debug.WriteLine($"[SLE] --- HEX DUMP ---");
                Debug.WriteLine(BitConverter.ToString(orderRequest).Replace("-", " "));
                Debug.WriteLine($"[SLE] Total message length: {orderRequest.Length} bytes");

                await _stream.WriteAsync(orderRequest, 0, orderRequest.Length);
                await _stream.FlushAsync();

                Debug.WriteLine($"[SLE] ✓ Order sent successfully");
                Debug.WriteLine($"[SLE] ========================================");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] ✗ Failed to send order: {ex.Message}");
                Debug.WriteLine($"[SLE] Stack trace: {ex.StackTrace}");
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
                    return; // Czekaj na więcej danych
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
                
                // Reply type (D)
                byte replyType = response[pos++];
                Debug.WriteLine($"[SLE] Reply type: {(char)replyType}");
                
                // Dekoduj dalsze pola...
                // TO DO: Implementacja pełnego parsowania odpowiedzi 2019
                
                Debug.WriteLine("[SLE] === ORDER REPLY COMPLETE ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Failed to parse order reply: {ex.Message}");
            }
        }

        private void ProcessOrderBookReply(byte[] response, int length, int stxPos)
        {
            Debug.WriteLine("[SLE] Processing order book reply (2004)");
            // TO DO: Implementacja
        }

        private void ProcessRepliesBook(byte[] response, int length, int stxPos)
        {
            Debug.WriteLine("[SLE] Processing replies book (2008)");
            // TO DO: Implementacja
        }

        #region Message Builders

        private byte[] BuildLoginRequest(string user, string password)
        {
            var dataBuilder = new List<byte>();
            
            // User number (3 bajty, padded left z zerami)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(user.PadLeft(3, '0')));
            
            // Password (16 bajtów, padded right spacjami)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(password.PadRight(16, ' ')));
            
            // 7 bajtów filler
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 7)));
            
            // Key/Value pairs
            dataBuilder.AddRange(EncodeField("15")); // Key: Server version
            dataBuilder.AddRange(EncodeField("V3")); // Value: Version 3
            
            dataBuilder.AddRange(EncodeField("26")); // Key: Username
            dataBuilder.AddRange(EncodeField(user)); // Value: User
            
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 1100);
        }

        /// <summary>
        /// POPRAWIONY format requestu 2017 zgodny z FIS API Manual
        /// </summary>
        private byte[] BuildRealTimeSubscriptionRequest()
        {
            // Request 2017 - Real-time replies subscription
            // Format zgodny z FIS API Manual (przykład C++)
            var dataBuilder = new List<byte>();
            
            // Flagi subskrypcji - określają jakie typy komunikatów real-time chcemy otrzymywać
            // Ack flag (1 bajt) - '1' = chcemy otrzymywać potwierdzenia
            dataBuilder.Add((byte)'1');
            
            // Reject flag (1 bajt) - '1' = chcemy otrzymywać odrzucenia
            dataBuilder.Add((byte)'1');
            
            // Exchange reject flag (1 bajt) - '1' = chcemy otrzymywać odrzucenia z giełdy
            dataBuilder.Add((byte)'1');
            
            // Trade execution flag (1 bajt) - '0' = nie subskrybujemy executions (domyślnie)
            dataBuilder.Add((byte)'0');
            
            // Exchange message flag (1 bajt) - '0' = nie subskrybujemy wiadomości z giełdy
            dataBuilder.Add((byte)'0');
            
            // Default flag (1 bajt) - '0' = wartość domyślna
            dataBuilder.Add((byte)'0');
            
            // Inflected message flag (1 bajt) - '0' = nie subskrybujemy
            dataBuilder.Add((byte)'0');
            
            // 11 bajtów filler (spacje)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 11)));
            
            // Łącznie: 7 + 11 = 18 bajtów danych
            var dataPayload = dataBuilder.ToArray();
            
            Debug.WriteLine($"[SLE] Building 2017 subscription request:");
            Debug.WriteLine($"[SLE] Data payload length: {dataPayload.Length} bytes");
            Debug.WriteLine($"[SLE] Flags: ack=1, reject=1, exch_reject=1, trade=0, msg=0, def=0, infl=0");
            
            return BuildMessage(dataPayload, 2017);
        }

        /// <summary>
        /// Buduje request 2000 - POPRAWIONA WERSJA
        /// Wysyła TYLKO wypełnione pola, pomija puste
        /// </summary>
            /// <summary>
            /// POPRAWIONA wersja - pola w kolejności SEKWENCYJNEJ
            /// SLE przypisuje numery pozycji automatycznie
            /// </summary>
            private byte[] BuildOrderRequest(
                string glidAndSymbol, 
                int side, 
                long quantity, 
                string modality, 
                decimal price, 
                string validity,
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
                Debug.WriteLine($"[SLE] Building order request V2 (sequential)");
                
                // === HEADER ===
                string userNum = _userNumber.PadLeft(5, '0');
                dataBuilder.AddRange(Encoding.ASCII.GetBytes(userNum));
                dataBuilder.Add((byte)'O'); // Request category
                dataBuilder.Add((byte)'0'); // Command
                dataBuilder.AddRange(EncodeField(glidAndSymbol));
                dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 10))); // Filler
                
                Debug.WriteLine($"[SLE] Stock: {glidAndSymbol}");
                Debug.WriteLine($"[SLE] Building bitmap fields in SEQUENTIAL order:");
                
                // === BITMAP ===
                // KRYTYCZNE: Wysyłamy pola SEKWENCYJNIE według dokumentacji
                // SLE przypisuje im numery pozycji automatycznie (0,1,2,3...)
                
                List<byte[]> fields = new List<byte[]>();
                
                // 0: Side (MANDATORY)
                fields.Add(EncodeField(side.ToString()));
                Debug.WriteLine($"[SLE] Seq 0: Side = {side}");
                
                // 1: Quantity (MANDATORY)
                fields.Add(EncodeField(quantity.ToString()));
                Debug.WriteLine($"[SLE] Seq 1: Quantity = {quantity}");
                
                // 2: Modality (MANDATORY)
                fields.Add(EncodeField(modality));
                Debug.WriteLine($"[SLE] Seq 2: Modality = {modality}");
                
                // 3: Price (conditional)
                if (modality == "L" && price > 0)
                {
                    fields.Add(EncodeField(price.ToString("F2", CultureInfo.InvariantCulture)));
                    Debug.WriteLine($"[SLE] Seq 3: Price = {price:F2}");
                }
                else
                {
                    fields.Add(new byte[] { 32 }); // Spacja bez GL encoding
                    Debug.WriteLine($"[SLE] Seq 3: Price = (empty)");
                }
                
                // 4: Validity (MANDATORY)
                fields.Add(EncodeField(validity));
                Debug.WriteLine($"[SLE] Seq 4: Validity = {validity}");
                
                // 5: Expiry date (conditional)
                fields.Add(new byte[] { 32 }); // Spacja
                Debug.WriteLine($"[SLE] Seq 5: Expiry date = (empty)");
                
                // BRAK 6,7 w dokumentacji
                
                // 8: Minimum quantity
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 6: Min quantity = (empty)");
                
                // 9: Displayed quantity
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 7: Displayed qty = (empty)");
                
                // 10: Client reference
                if (!string.IsNullOrEmpty(clientReference))
                {
                    string clRef = clientReference.Substring(0, Math.Min(8, clientReference.Length));
                    fields.Add(EncodeField(clRef));
                    Debug.WriteLine($"[SLE] Seq 8: Client Ref = {clRef}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 8: Client Ref = (empty)");
                }
                
                // BRAK 11
                
                // 12: Internal reference (MANDATORY)
                string intRef = internalReference;
                if (string.IsNullOrEmpty(intRef))
                {
                    intRef = $"ORD{DateTime.Now:yyyyMMddHHmmss}";
                }
                intRef = intRef.Substring(0, Math.Min(16, intRef.Length));
                fields.Add(EncodeField(intRef));
                Debug.WriteLine($"[SLE] Seq 9: Internal Ref = {intRef}");
                
                // 13: Exchange number (conditional)
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 10: Exchange number = (empty)");
                
                // BRAK 14,15,16
                
                // 17: Client Code Type (MANDATORY)
                fields.Add(EncodeField(clientCodeType));
                Debug.WriteLine($"[SLE] Seq 11: Client Code Type = {clientCodeType} *** MANDATORY ***");
                
                // BRAK 18
                
                // 19: Allocation Code
                if (!string.IsNullOrEmpty(allocationCode))
                {
                    string allocCode = allocationCode.Substring(0, Math.Min(8, allocationCode.Length));
                    fields.Add(EncodeField(allocCode));
                    Debug.WriteLine($"[SLE] Seq 12: Allocation Code = {allocCode}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 12: Allocation Code = (empty)");
                }
                
                // BRAK 20
                
                // 21: Posting Mode
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 13: Posting Mode = (empty)");
                
                // BRAK 22,23
                
                // 24: Compensation Mode 1
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 14: Compensation Mode = (empty)");
                
                // BRAK 25
                
                // 26: Stop loss price
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 15: Stop loss price = (empty)");
                
                // 27: Routing reference
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 16: Routing ref = (empty)");
                
                // BRAK 28-80
                
                // 81: Memo
                if (!string.IsNullOrEmpty(memo))
                {
                    string memoStr = memo.Substring(0, Math.Min(18, memo.Length));
                    fields.Add(EncodeField(memoStr));
                    Debug.WriteLine($"[SLE] Seq 17: Memo = {memoStr}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 17: Memo = (empty)");
                }
                
                // 82: Trader Order Number
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 18: Trader Order Number = (empty)");
                
                // BRAK 83-90
                
                // 91: Application side
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 19: Application side = (empty)");
                
                // 92: Hour date station (TIMESTAMP)
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                fields.Add(EncodeField(timestamp));
                Debug.WriteLine($"[SLE] Seq 20: Hour date station = {timestamp}");
                
                // BRAK 93-105
                
                // 106: GLID (MANDATORY)
                string glid = glidAndSymbol.Length >= 12 
                    ? glidAndSymbol.Substring(0, 12) 
                    : glidAndSymbol.PadRight(12, ' ');
                fields.Add(EncodeField(glid));
                Debug.WriteLine($"[SLE] Seq 21: GLID = {glid} *** MANDATORY ***");
                
                // BRAK 107-116
                
                // 117: Exchange cancel quantity
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 22: Exchange cancel qty = (empty)");
                
                // BRAK 118-131
                
                // 132: Clearing Account 1
                if (!string.IsNullOrEmpty(clearingAccount))
                {
                    fields.Add(EncodeField(clearingAccount));
                    Debug.WriteLine($"[SLE] Seq 23: Clearing Account = {clearingAccount}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 23: Clearing Account = (empty)");
                }
                
                // BRAK 133-146
                
                // 147: Floor Trader ID
                if (!string.IsNullOrEmpty(floorTraderId))
                {
                    fields.Add(EncodeField(floorTraderId));
                    Debug.WriteLine($"[SLE] Seq 24: Floor Trader ID = {floorTraderId}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 24: Floor Trader ID = (empty)");
                }
                
                // BRAK 148-191
                
                // 192: Currency
                if (!string.IsNullOrEmpty(currency))
                {
                    fields.Add(EncodeField(currency));
                    Debug.WriteLine($"[SLE] Seq 25: Currency = {currency}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 25: Currency = (empty)");
                }
                
                // BRAK 193-305
                
                // 306: Second Client Code Type
                if (!string.IsNullOrEmpty(secondClientCodeType) && secondClientCodeType != " ")
                {
                    fields.Add(EncodeField(secondClientCodeType));
                    Debug.WriteLine($"[SLE] Seq 26: Second Client Code Type = {secondClientCodeType}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 26: Second Client Code Type = (empty)");
                }
                
                // BRAK 307-316
                
                // 317: Client Free Field 1
                if (!string.IsNullOrEmpty(clientFreeField1))
                {
                    string customField = clientFreeField1.Substring(0, Math.Min(16, clientFreeField1.Length));
                    fields.Add(EncodeField(customField));
                    Debug.WriteLine($"[SLE] Seq 27: Client Free Field 1 = {customField}");
                }
                else
                {
                    fields.Add(new byte[] { 32 });
                    Debug.WriteLine($"[SLE] Seq 27: Client Free Field 1 = (empty)");
                }
                
                // 318: Client Free Field 2
                fields.Add(new byte[] { 32 });
                Debug.WriteLine($"[SLE] Seq 28: Client Free Field 2 = (empty)");
                
                // Połącz wszystkie pola
                foreach (var field in fields)
                {
                    dataBuilder.AddRange(field);
                }
                
                Debug.WriteLine($"[SLE] Total fields: {fields.Count}");
                Debug.WriteLine($"[SLE] Data payload size: {dataBuilder.Count} bytes");
                
                var dataPayload = dataBuilder.ToArray();
                
                // Hex dump
                Debug.WriteLine($"[SLE] --- DATA PAYLOAD HEX ---");
                for (int i = 0; i < Math.Min(200, dataPayload.Length); i += 32)
                {
                    int len = Math.Min(32, dataPayload.Length - i);
                    string hex = BitConverter.ToString(dataPayload, i, len).Replace("-", " ");
                    Debug.WriteLine($"[SLE] {i:D4}: {hex}");
                }
                Debug.WriteLine($"[SLE] ========================================");
                
                return BuildMessage(dataPayload, 2000);
            }

        /// <summary>
        /// Buduje wiadomość FIS API z nagłówkiem, danymi i stopką
        /// POPRAWIONY: API version dla SLE V5 to '0'
        /// POPRAWIONY: Używa przydzielonego Calling ID zamiast zer
        /// </summary>
        /// <summary>
        /// Buduje wiadomość FIS API z nagłówkiem, danymi i stopką
        /// POPRAWIONY: API version dla różnych requestów
        /// </summary>
        private byte[] BuildMessage(byte[] dataPayload, int requestNumber)
        {
            int dataLength = dataPayload.Length;
            int totalLength = 2 + HeaderLength + dataLength + FooterLength;
            var message = new byte[totalLength];

            using (var ms = new System.IO.MemoryStream(message))
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // LG (2 bajty) - little endian!
                writer.Write((byte)(totalLength % 256));
                writer.Write((byte)(totalLength / 256));
                
                Debug.WriteLine($"[SLE] Total message length: {totalLength}");
                
                // HEADER (32 bajty)
                writer.Write(Stx); // STX = 0x02
                
                // API version (1 bajt)
                // KRYTYCZNE: Dla request 2000 używamy API V3 = '0'
                if (requestNumber == 2000 || requestNumber == 2019)
                {
                    writer.Write((byte)'0'); // API V3
                    Debug.WriteLine($"[SLE] API Version: 0 (V3) for request {requestNumber}");
                }
                else if (requestNumber == 2017)
                {
                    writer.Write((byte)' '); // API V4 dla subscriptions
                    Debug.WriteLine($"[SLE] API Version: space (V4) for request {requestNumber}");
                }
                else
                {
                    writer.Write((byte)'0'); // Default V3
                    Debug.WriteLine($"[SLE] API Version: 0 (V3 default)");
                }
                
                // Length (5 bajtów ASCII)
                int contentLength = HeaderLength + dataLength + FooterLength;
                writer.Write(Encoding.ASCII.GetBytes(contentLength.ToString().PadLeft(5, '0')));
                Debug.WriteLine($"[SLE] Content length: {contentLength}");
                
                // Called logical ID (5 bajtów) - server subnode
                writer.Write(Encoding.ASCII.GetBytes(_subnode.PadLeft(5, '0')));
                Debug.WriteLine($"[SLE] Called ID (server): {_subnode}");
                
                // Filler (5 bajtów spacji)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 5)));
                
                // Calling logical ID (5 bajtów) - nasz assigned ID
                writer.Write(Encoding.ASCII.GetBytes(_assignedCallingId.PadLeft(5, '0')));
                Debug.WriteLine($"[SLE] Calling ID (us): {_assignedCallingId}");
                
                // Filler (2 bajty spacji)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                
                // Request number (5 bajtów)
                writer.Write(Encoding.ASCII.GetBytes(requestNumber.ToString().PadLeft(5, '0')));
                Debug.WriteLine($"[SLE] Request number: {requestNumber}");
                
                // Filler (3 bajty spacji)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 3)));
                
                // DATA
                writer.Write(dataPayload);
                Debug.WriteLine($"[SLE] Data payload: {dataPayload.Length} bytes");
                
                // FOOTER (3 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                writer.Write(Etx); // ETX = 0x03
            }
            
            // Hex dump całej wiadomości
            Debug.WriteLine($"[SLE] === COMPLETE MESSAGE HEX DUMP ===");
            Debug.WriteLine($"[SLE] Total: {message.Length} bytes");
            
            for (int i = 0; i < message.Length; i += 16)
            {
                int len = Math.Min(16, message.Length - i);
                
                // Offset
                string offset = $"{i:X4}";
                
                // Hex
                string hex = BitConverter.ToString(message, i, len).Replace("-", " ");
                
                // ASCII
                string ascii = "";
                for (int j = 0; j < len; j++)
                {
                    byte b = message[i + j];
                    if (b >= 32 && b < 127)
                        ascii += (char)b;
                    else
                        ascii += '.';
                }
                
                Debug.WriteLine($"[SLE] {offset}  {hex,-48}  |{ascii}|");
            }
            Debug.WriteLine($"[SLE] === END MESSAGE DUMP ===");
            
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
                // Puste pole - zwróć pojedynczą spację
                return new byte[] { 33, 32 }; // LENGTH=1+32=33, DATA=' '
            }
    
            var valueBytes = Encoding.ASCII.GetBytes(value);
            var encoded = new byte[valueBytes.Length + 1];
            encoded[0] = (byte)(valueBytes.Length + 32);
            Array.Copy(valueBytes, 0, encoded, 1, valueBytes.Length);
    
            // Debug
            string hex = BitConverter.ToString(encoded).Replace("-", " ");
            Debug.WriteLine($"[SLE] EncodeField('{value}') -> [{hex}]");
    
            return encoded;
        }

        private bool VerifyLoginResponse(byte[] response, int length)
        {
            if (length < HeaderLength + 2) return false;
            
            try
            {
                // Sprawdź request number w odpowiedzi
                string requestNumberStr = Encoding.ASCII.GetString(response, 26, 5);
                
                if (requestNumberStr == "01100") // Success response
                {
                    // KRYTYCZNE: W odpowiedzi serwera pola są odwrócone!
                    // W requestach OD klienta: Called=serwer(14300), Calling=klient(my)
                    // W odpowiedziach OD serwera: Called=klient(my), Calling=serwer(14300)
                    // Musimy odczytać CALLED ID (pozycja 9-13), nie CALLING ID (19-23)!
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
    }

    // Model dla odpowiedzi na zlecenie
    public class OrderReply
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal ExecutedPrice { get; set; }
        public long ExecutedQuantity { get; set; }
    }
}
