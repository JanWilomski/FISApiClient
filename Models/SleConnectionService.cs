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
            string clientReference = "",
            string internalReference = "")
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
                Debug.WriteLine($"[SLE] === SENDING ORDER ===");
                Debug.WriteLine($"[SLE] Instrument: {glidAndSymbol}");
                Debug.WriteLine($"[SLE] Side: {(side == 0 ? "BUY" : "SELL")}");
                Debug.WriteLine($"[SLE] Quantity: {quantity}");
                Debug.WriteLine($"[SLE] Modality: {modality}");
                Debug.WriteLine($"[SLE] Price: {price}");
                Debug.WriteLine($"[SLE] Validity: {validity}");

                byte[] orderRequest = BuildOrderRequest(
                    glidAndSymbol, 
                    side, 
                    quantity, 
                    modality, 
                    price, 
                    validity,
                    clientReference,
                    internalReference);

                await _stream.WriteAsync(orderRequest, 0, orderRequest.Length);
                await _stream.FlushAsync();

                Debug.WriteLine($"[SLE] Order sent successfully");
                Debug.WriteLine($"[SLE] === ORDER COMPLETE ===");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SLE] Failed to send order: {ex.Message}");
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

        private byte[] BuildRealTimeSubscriptionRequest()
        {
            // Request 2017 - Real-time replies subscription
            var dataBuilder = new List<byte>();
            
            // User number (5 bajtów ASCII)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(_userNumber.PadLeft(5, '0')));
            
            // Request category (1 bajt) - ' ' dla subscription
            dataBuilder.Add((byte)' ');
            
            // Filler (10 bajtów)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 10)));
            
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 2017);
        }

        private byte[] BuildOrderRequest(
            string glidAndSymbol, 
            int side, 
            long quantity, 
            string modality, 
            decimal price, 
            string validity,
            string clientReference,
            string internalReference)
        {
            var dataBuilder = new List<byte>();
            
            // === HEADER ===
            // User number (5 bajtów ASCII)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(_userNumber.PadLeft(5, '0')));
            
            // Request category (1 bajt) - 'O' dla Simple Order
            dataBuilder.Add((byte)'O');
            
            // Command (1 bajt) - '0' = New Order
            dataBuilder.Add((byte)'0');
            
            // Stock code (GLID+Symbol w formacie GL)
            dataBuilder.AddRange(EncodeField(glidAndSymbol));
            
            // Filler (10 bajtów)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 10)));
            
            // === BITMAP (pola danych) ===
            // Pozycje 0-n, każde pole zakodowane w formacie GL
            
            // WAŻNE: Musimy zakodować pozycje jako spacer dla nieużywanych pól
            // i właściwe wartości dla używanych
            
            // Pole 0: Side (0=Buy, 1=Sell)
            dataBuilder.AddRange(EncodeField(side.ToString()));
            
            // Pole 1: Quantity
            dataBuilder.AddRange(EncodeField(quantity.ToString()));
            
            // Pole 2: Modality (L/B/M)
            dataBuilder.AddRange(EncodeField(modality));
            
            // Pole 3: Price (jeśli modality = L)
            if (modality == "L" && price > 0)
            {
                dataBuilder.AddRange(EncodeField(price.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                dataBuilder.Add((byte)' '); // Puste pole
            }
            
            // Pole 4: Validity (J/K/E)
            dataBuilder.AddRange(EncodeField(validity));
            
            // Pole 5: Expiry date (opcjonalne)
            dataBuilder.Add((byte)' ');
            
            // Pole 6: Expiry time (opcjonalne)
            dataBuilder.Add((byte)' ');
            
            // Pole 7: (nieużywane)
            dataBuilder.Add((byte)' ');
            
            // Pole 8: Minimum quantity (opcjonalne)
            dataBuilder.Add((byte)' ');
            
            // Pole 9: Displayed quantity (opcjonalne)
            dataBuilder.Add((byte)' ');
            
            // Pole 10: Client reference (opcjonalne, max 8 znaków)
            if (!string.IsNullOrEmpty(clientReference))
            {
                dataBuilder.AddRange(EncodeField(clientReference.Substring(0, Math.Min(8, clientReference.Length))));
            }
            else
            {
                dataBuilder.Add((byte)' ');
            }
            
            // Pole 11: (nieużywane)
            dataBuilder.Add((byte)' ');
            
            // Pole 12: Internal reference (opcjonalne, max 16 znaków)
            if (!string.IsNullOrEmpty(internalReference))
            {
                dataBuilder.AddRange(EncodeField(internalReference.Substring(0, Math.Min(16, internalReference.Length))));
            }
            else
            {
                dataBuilder.Add((byte)' ');
            }
            
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 2000);
        }

        private byte[] BuildMessage(byte[] dataPayload, int requestNumber)
        {
            int dataLength = dataPayload.Length;
            int totalLength = 2 + HeaderLength + dataLength + FooterLength;
            var message = new byte[totalLength];

            using (var ms = new System.IO.MemoryStream(message))
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // LG (2 bajty)
                writer.Write((byte)(totalLength % 256));
                writer.Write((byte)(totalLength / 256));
                
                // HEADER (32 bajty)
                writer.Write(Stx);
                writer.Write((byte)'0'); // API version dla v3
                writer.Write(Encoding.ASCII.GetBytes((HeaderLength + dataLength + FooterLength).ToString().PadLeft(5, '0')));
                writer.Write(Encoding.ASCII.GetBytes(_subnode.PadLeft(5, '0'))); // Called logical ID
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 5))); // Filler
                writer.Write(Encoding.ASCII.GetBytes("00000")); // Calling logical ID
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2))); // Filler
                writer.Write(Encoding.ASCII.GetBytes(requestNumber.ToString().PadLeft(5, '0'))); // Request number
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 3))); // Filler
                
                // DATA
                writer.Write(dataPayload);
                
                // FOOTER (3 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                writer.Write(Etx);
            }
            
            return message;
        }

        private byte[] EncodeField(string value)
        {
            var valueBytes = Encoding.ASCII.GetBytes(value);
            var encoded = new byte[valueBytes.Length + 1];
            encoded[0] = (byte)(valueBytes.Length + 32);
            Array.Copy(valueBytes, 0, encoded, 1, valueBytes.Length);
            return encoded;
        }

        private bool VerifyLoginResponse(byte[] response, int length)
        {
            if (length < HeaderLength + 2) return false;
            
            try
            {
                string requestNumberStr = Encoding.ASCII.GetString(response, 26, 5);
                return requestNumberStr == "01100"; // Success response
            }
            catch
            {
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
