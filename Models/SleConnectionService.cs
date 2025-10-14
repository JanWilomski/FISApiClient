using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            string localCode,      // ← ZMIANA: zamiast glidAndSymbol
            string glid,           // ← NOWY parametr
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
            try
            {
                Debug.WriteLine($"[SLE] ========================================");
                Debug.WriteLine($"[SLE] Sending order with LOCAL CODE");
                Debug.WriteLine($"[SLE] LocalCode: {localCode}");
                Debug.WriteLine($"[SLE] GLID: {glid}");
                Debug.WriteLine($"[SLE] Side: {(side == 0 ? "BUY" : "SELL")}");
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
                
                // BITMAP - dekodowanie pól
                Debug.WriteLine("[SLE] Decoding bitmap fields...");
                var bitmapFields = new Dictionary<int, string>();
                
                while (pos < length - FooterLength)
                {
                    // Dekoduj Field ID
                    var (fieldIdStr, idBytesRead) = DecodeGLField(response, pos);
                    if (string.IsNullOrEmpty(fieldIdStr)) break;
                    
                    pos += idBytesRead;
                    
                    if (!int.TryParse(fieldIdStr, out int fieldId))
                    {
                        Debug.WriteLine($"[SLE] Invalid field ID: {fieldIdStr}");
                        break;
                    }
                    
                    // Dekoduj wartość pola
                    var (fieldValue, valueBytesRead) = DecodeGLField(response, pos);
                    pos += valueBytesRead;
                    
                    bitmapFields[fieldId] = fieldValue;
                    Debug.WriteLine($"[SLE] Field #{fieldId} = {fieldValue}");
                }
                
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
                    OrderAccepted?.Invoke($"Order accepted - Reference: {bitmapFields.GetValueOrDefault(42, "N/A")}");
                }
                else if (replyType == 'C' || replyType == 'G')
                {
                    OrderRejected?.Invoke($"Order rejected - Code: {bitmapFields.GetValueOrDefault(65, "Unknown")}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Failed to parse order reply: {ex.Message}");
            }
        }

        private void ProcessOrderBookReply(byte[] response, int length, int stxPos)
        {
            Debug.WriteLine("[SLE] Processing order book reply (2004)");
        }

        private void ProcessRepliesBook(byte[] response, int length, int stxPos)
        {
            Debug.WriteLine("[SLE] Processing replies book (2008)");
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

        /// <summary>
        /// POPRAWIONY BuildOrderRequest
        /// Format zgodny z dokumentacją FIS API:
        /// - Header: User(5) + Category(1) + Command(1) + Stockcode(GL) + Filler(10)
        /// - Bitmap: Dla każdego pola [Field_ID jako GL][Value jako GL]
        /// - PUSTE pola NIE są wysyłane
        /// </summary>
        /// <summary>
        /// POPRAWIONY BuildOrderRequest - używa LocalCode w stockcode i GLID w Field 106
        /// </summary>
        private byte[] BuildOrderRequest(
            string localCode,      // ← ZMIANA: LocalCode dla pola G
            string glid,           // ← NOWY: GLID dla Field 106
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
            Debug.WriteLine($"[SLE] Building order request with LOCAL CODE");
            
            // === HEADER ===
            string userNum = _userNumber.PadLeft(5, '0');
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(userNum));
            Debug.WriteLine($"[SLE] User number: {userNum}");
            
            dataBuilder.Add((byte)'O'); // Request category = Simple order
            Debug.WriteLine($"[SLE] Request category: O");
            
            dataBuilder.Add((byte)'0'); // Command = New order
            Debug.WriteLine($"[SLE] Command: 0 (New)");
            
            //LOCAL CODE
            dataBuilder.AddRange(EncodeField(localCode));
            Debug.WriteLine($"[SLE] Stockcode (LOCAL CODE): {localCode}");
            
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 10)));
            
            Debug.WriteLine($"[SLE] Building bitmap with field IDs:");
            
            // === BITMAP ===
            var bitmapFields = new List<byte>();
            
            // Field 0: Side (MANDATORY)
            bitmapFields.AddRange(EncodeField("0"));
            bitmapFields.AddRange(EncodeField(side.ToString()));
            Debug.WriteLine($"[SLE] Field #0: Side = {side}");
            
            // Field 1: Quantity (MANDATORY)
            bitmapFields.AddRange(EncodeField("1"));
            bitmapFields.AddRange(EncodeField(quantity.ToString(CultureInfo.InvariantCulture)));
            Debug.WriteLine($"[SLE] Field #1: Quantity = {quantity}");
            
            // Field 2: Modality (MANDATORY)
            bitmapFields.AddRange(EncodeField("2"));
            bitmapFields.AddRange(EncodeField(modality));
            Debug.WriteLine($"[SLE] Field #2: Modality = {modality}");
            
            // Field 3: Price (conditional - tylko dla Limit orders)
            if (modality == "L" && price > 0)
            {
                bitmapFields.AddRange(EncodeField("3"));
                // ⭐ USUWAMY KROPKĘ - wysyłaj jako integer (cena × 100)
                long priceInteger = (long)Math.Round(price * 100);
                bitmapFields.AddRange(EncodeField(priceInteger.ToString(CultureInfo.InvariantCulture)));
                Debug.WriteLine($"[SLE] Field #3: Price = {price:F2} → integer = {priceInteger}");
            }
            
            // Field 4: Validity (MANDATORY)
            bitmapFields.AddRange(EncodeField("4"));
            bitmapFields.AddRange(EncodeField(validity));
            Debug.WriteLine($"[SLE] Field #4: Validity = {validity}");
            
            // Field 10: Client reference (opcjonalne)
            if (!string.IsNullOrEmpty(clientReference))
            {
                string clRef = clientReference.Substring(0, Math.Min(8, clientReference.Length));
                bitmapFields.AddRange(EncodeField("10"));
                bitmapFields.AddRange(EncodeField(clRef));
                Debug.WriteLine($"[SLE] Field #10: Client Reference = {clRef}");
            }
            
            // Field 12: Internal reference (MANDATORY)
            string intRef = internalReference;
            if (string.IsNullOrEmpty(intRef))
            {
                intRef = $"ORD{DateTime.Now:yyyyMMddHHmmss}";
            }
            intRef = intRef.Substring(0, Math.Min(16, intRef.Length));
            bitmapFields.AddRange(EncodeField("12"));
            bitmapFields.AddRange(EncodeField(intRef));
            Debug.WriteLine($"[SLE] Field #12: Internal Reference = {intRef}");
            
            // Field 17: Client Code Type (MANDATORY dla WSE)
            bitmapFields.AddRange(EncodeField("17"));
            bitmapFields.AddRange(EncodeField(clientCodeType));
            Debug.WriteLine($"[SLE] Field #17: Client Code Type = {clientCodeType} *** MANDATORY ***");
            
            // Field 19: Allocation Code
            if (!string.IsNullOrEmpty(allocationCode))
            {
                string allocCode = allocationCode.Substring(0, Math.Min(8, allocationCode.Length));
                bitmapFields.AddRange(EncodeField("19"));
                bitmapFields.AddRange(EncodeField(allocCode));
                Debug.WriteLine($"[SLE] Field #19: Allocation Code = {allocCode}");
            }
            
            // Field 81: Memo
            if (!string.IsNullOrEmpty(memo))
            {
                string memoStr = memo.Substring(0, Math.Min(18, memo.Length));
                bitmapFields.AddRange(EncodeField("81"));
                bitmapFields.AddRange(EncodeField(memoStr));
                Debug.WriteLine($"[SLE] Field #81: Memo = {memoStr}");
            }
            // Field 91: Application side (MANDATORY dla WSE)
            bitmapFields.AddRange(EncodeField("91"));
            bitmapFields.AddRange(EncodeField("C")); // C = Client
            Debug.WriteLine($"[SLE] Field #91: Application side = C");

            
            
            // Field 92: Hour date station (timestamp)
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            bitmapFields.AddRange(EncodeField("92"));
            bitmapFields.AddRange(EncodeField(timestamp));
            Debug.WriteLine($"[SLE] Field #92: Timestamp = {timestamp}");
            
            // ⭐ Field 106: GLID (MANDATORY) - ZMIANA: używaj przekazanego GLID
            string glidFormatted = glid.Length >= 12 
                ? glid.Substring(0, 12) 
                : glid.PadRight(12, ' ');
            bitmapFields.AddRange(EncodeField("106"));
            bitmapFields.AddRange(EncodeField(glidFormatted));
            Debug.WriteLine($"[SLE] Field #106: GLID = {glidFormatted} *** MANDATORY ***");
            
            // Field 132: Clearing Account 1
            if (!string.IsNullOrEmpty(clearingAccount))
            {
                bitmapFields.AddRange(EncodeField("132"));
                bitmapFields.AddRange(EncodeField(clearingAccount));
                Debug.WriteLine($"[SLE] Field #132: Clearing Account = {clearingAccount}");
            }
            
            // Field 147: Floor Trader ID
            if (!string.IsNullOrEmpty(floorTraderId))
            {
                bitmapFields.AddRange(EncodeField("147"));
                bitmapFields.AddRange(EncodeField(floorTraderId));
                Debug.WriteLine($"[SLE] Field #147: Floor Trader ID = {floorTraderId}");
            }
            
            // Field 192: Currency
            if (!string.IsNullOrEmpty(currency))
            {
                bitmapFields.AddRange(EncodeField("192"));
                bitmapFields.AddRange(EncodeField(currency));
                Debug.WriteLine($"[SLE] Field #192: Currency = {currency}");
            }
            
            // Field 306: Second Client Code Type
            if (!string.IsNullOrEmpty(secondClientCodeType) && secondClientCodeType != " ")
            {
                bitmapFields.AddRange(EncodeField("306"));
                bitmapFields.AddRange(EncodeField(secondClientCodeType));
                Debug.WriteLine($"[SLE] Field #306: Second Client Code Type = {secondClientCodeType}");
            }
            
            // Field 317: Client Free Field 1
            if (!string.IsNullOrEmpty(clientFreeField1))
            {
                string customField = clientFreeField1.Substring(0, Math.Min(16, clientFreeField1.Length));
                bitmapFields.AddRange(EncodeField("317"));
                bitmapFields.AddRange(EncodeField(customField));
                Debug.WriteLine($"[SLE] Field #317: Client Free Field 1 = {customField}");
            }
            
            // Dodaj bitmap do dataBuilder
            dataBuilder.AddRange(bitmapFields);
            
            Debug.WriteLine($"[SLE] Bitmap size: {bitmapFields.Count} bytes");
            Debug.WriteLine($"[SLE] Total payload size: {dataBuilder.Count} bytes");
            
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
            if (startPos >= data.Length)
                return (string.Empty, 0);
            
            int length = data[startPos] - 32;
            
            if (length <= 0 || startPos + 1 + length > data.Length)
                return (string.Empty, 0);
            
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
    }

    public class OrderReply
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal ExecutedPrice { get; set; }
        public long ExecutedQuantity { get; set; }
    }
}