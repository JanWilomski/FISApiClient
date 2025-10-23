using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FISApiClient.Models
{
    public class MdsConnectionService
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private string _node = string.Empty;
        private string _subnode = string.Empty;

        private const byte Stx = 2;
        private const byte Etx = 3;
        private const int HeaderLength = 32;
        private const int FooterLength = 3;

        public bool IsConnected => _tcpClient?.Connected ?? false;
        public event Action<List<Instrument>>? InstrumentsReceived;
        public event Action<InstrumentDetails>? InstrumentDetailsReceived;
        
        private readonly List<byte> _receiveBuffer = new List<byte>();
        
        // Śledzenie aktywnych subskrypcji real-time
        private readonly HashSet<string> _activeSubscriptions = new HashSet<string>();
        private readonly object _subscriptionLock = new object();
        
        // Cache dla szczegółów instrumentów (dla mergowania real-time updates)
        private readonly Dictionary<string, InstrumentDetails> _instrumentDetailsCache = new Dictionary<string, InstrumentDetails>();

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

                var clientId = Encoding.ASCII.GetBytes("FISAPICLIENT    ");
                await _stream.WriteAsync(clientId, 0, clientId.Length);

                byte[] loginRequest = BuildLoginRequest(user, password);
                await _stream.WriteAsync(loginRequest, 0, loginRequest.Length);

                var buffer = new byte[1024];
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    bool loginSuccess = VerifyLoginResponse(buffer, bytesRead);
                    if (loginSuccess)
                    {
                        _ = Task.Run(ListenForMessages);
                        return true;
                    }
                }
                
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MDS Connection failed: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        private async Task ListenForMessages()
        {
            if (_stream == null) return;
            var buffer = new byte[8192];
    
            Debug.WriteLine("[MDS] ListenForMessages started");
    
            while (IsConnected)
            {
                try
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
            
                    if (bytesRead > 0)
                    {
                        Debug.WriteLine($"[MDS] Received {bytesRead} bytes from server");
                
                        // Dodaj odebrane dane do bufora
                        for (int i = 0; i < bytesRead; i++)
                        {
                            _receiveBuffer.Add(buffer[i]);
                        }
                
                        Debug.WriteLine($"[MDS] Total buffer size: {_receiveBuffer.Count} bytes");
                
                        // Przetwarzaj wszystkie kompletne wiadomości w buforze
                        ProcessBufferedMessages();
                    }
                    else
                    {
                        Debug.WriteLine("[MDS] Connection closed by server (0 bytes read)");
                        Disconnect();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MDS] Error in ListenForMessages: {ex.Message}");
                    Disconnect();
                    break;
                }
            }
    
            Debug.WriteLine("[MDS] ListenForMessages stopped");
        }
        
        private void ProcessBufferedMessages()
        {
            while (_receiveBuffer.Count >= 3)
            {
                int bytesToShow = Math.Min(30, _receiveBuffer.Count);
                string hexDump = string.Join(" ", _receiveBuffer.Take(bytesToShow).Select(b => b.ToString("X2")));
                Debug.WriteLine($"[MDS] Buffer first {bytesToShow} bytes: {hexDump}");
                
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
                            Debug.WriteLine($"[MDS] Found valid message pattern at position {i}:");
                            Debug.WriteLine($"[MDS]   Length bytes: {_receiveBuffer[i]:X2} {_receiveBuffer[i + 1]:X2} = {length}");
                            Debug.WriteLine($"[MDS]   STX at position: {i + 2}");
                            
                            messageStart = i;
                            totalMessageLength = length;
                            foundValidMessage = true;
                            break;
                        }
                        else
                        {
                            Debug.WriteLine($"[MDS] Found STX at {i + 2} but invalid length {length}, continuing search...");
                        }
                    }
                }
                
                if (!foundValidMessage)
                {
                    Debug.WriteLine("[MDS] No valid message pattern found in buffer");
                    
                    if (_receiveBuffer.Count > 0)
                    {
                        Debug.WriteLine($"[MDS] Removing first byte: {_receiveBuffer[0]:X2}");
                        _receiveBuffer.RemoveAt(0);
                    }
                    
                    if (_receiveBuffer.Count > 100)
                    {
                        Debug.WriteLine("[MDS] Buffer too large without valid message, clearing");
                        _receiveBuffer.Clear();
                    }
                    
                    return;
                }
                
                Debug.WriteLine($"[MDS] Message starts at position {messageStart}, total length: {totalMessageLength}");
                Debug.WriteLine($"[MDS] Buffer has {_receiveBuffer.Count} bytes, need {messageStart + totalMessageLength}");
                
                if (messageStart + totalMessageLength > _receiveBuffer.Count)
                {
                    int needed = messageStart + totalMessageLength - _receiveBuffer.Count;
                    Debug.WriteLine($"[MDS] Incomplete message: need {needed} more bytes");
                    return;
                }
                
                Debug.WriteLine("[MDS] Complete message received, processing...");
                
                byte[] messageBytes = _receiveBuffer.GetRange(messageStart, totalMessageLength).ToArray();
                
                int msgHexLen = Math.Min(50, messageBytes.Length);
                string msgHex = string.Join(" ", messageBytes.Take(msgHexLen).Select(b => b.ToString("X2")));
                Debug.WriteLine($"[MDS] Message first {msgHexLen} bytes: {msgHex}");
                
                ProcessSingleMessage(messageBytes, totalMessageLength, 2);
                
                _receiveBuffer.RemoveRange(0, messageStart + totalMessageLength);
                
                Debug.WriteLine($"[MDS] Message processed, remaining buffer: {_receiveBuffer.Count} bytes");
            }
        }
        
        private void ProcessSingleMessage(byte[] response, int length, int stxPos)
        {
            Debug.WriteLine($"[MDS] ProcessSingleMessage: length={length}, stxPos={stxPos}");
    
            try
            {
                string requestNumberStr = Encoding.ASCII.GetString(response, stxPos + 24, 5);
                if (int.TryParse(requestNumberStr, out int requestNumber))
                {
                    Debug.WriteLine($"[MDS] Received response with request number: {requestNumber}");
            
                    switch(requestNumber)
                    {
                        case 5108:
                            ProcessDictionaryResponse(response, length, stxPos);
                            break;
                        case 1000: // Snapshot (stary)
                        case 1001: // Refreshed snapshot (nowy - z real-time)
                            Debug.WriteLine($"[MDS] Processing instrument details snapshot (request {requestNumber})");
                            ProcessInstrumentDetailsResponse(response, length, stxPos);
                            break;
                        case 1003: // Real-time update
                            Debug.WriteLine($"[MDS] Processing real-time update (request 1003)");
                            ProcessRealTimeUpdate(response, length, stxPos);
                            break;
                        default:
                            Debug.WriteLine($"[MDS] Unhandled request number: {requestNumber}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDS] Error processing message: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (_tcpClient == null) return;
            
            // Zatrzymaj wszystkie aktywne subskrypcje przed rozłączeniem
            lock (_subscriptionLock)
            {
                foreach (var glidAndSymbol in _activeSubscriptions.ToList())
                {
                    _ = StopInstrumentDetailsAsync(glidAndSymbol);
                }
                _activeSubscriptions.Clear();
            }
            
            _stream?.Close();
            _tcpClient?.Close();
            _tcpClient = null;
            _stream = null;
            _instrumentDetailsCache.Clear();
        }

        public async Task RequestAllInstrumentsAsync()
        {
            if (!IsConnected || _stream == null) return;

            int[] exchanges = { 40, 330, 331, 332 };
            int[] markets = { 1, 2, 3, 4, 5, 9, 16, 17, 20 };

            foreach (var exchange in exchanges)
            {
                foreach (var market in markets)
                {
                    if (!IsConnected || _stream == null)
                    {
                        Debug.WriteLine("MDS connection lost during instrument fetch. Aborting.");
                        return; 
                    }
                    string glid = $"{exchange:D4}00{market:D3}000";
                    byte[] dictionaryRequest = BuildDictionaryRequest(glid);
                    await _stream.WriteAsync(dictionaryRequest, 0, dictionaryRequest.Length);
                    await Task.Delay(50);
                }
            }
        }

        /// <summary>
        /// Wysyła request 1001 (Refreshed) dla instrumentu - zwraca snapshot i rozpoczyna real-time updates (1003)
        /// </summary>
        public async Task RequestInstrumentDetails(string glidAndStockcode)
        {
            if (!IsConnected || _stream == null)
            {
                Debug.WriteLine("[MDS] Cannot request details - not connected");
                return;
            }
    
            Debug.WriteLine($"[MDS] === SENDING REQUEST 1001 (REFRESHED) ===");
            Debug.WriteLine($"[MDS] Requesting real-time updates for: '{glidAndStockcode}'");
    
            // Użyj request 1001 zamiast 1000 dla real-time updates
            byte[] request = BuildStockWatchRequest(glidAndStockcode, useRealTime: true);
    
            Debug.WriteLine($"[MDS] Request built, size: {request.Length} bytes");
    
            await _stream.WriteAsync(request, 0, request.Length);
            await _stream.FlushAsync();
            
            // Dodaj do aktywnych subskrypcji
            lock (_subscriptionLock)
            {
                _activeSubscriptions.Add(glidAndStockcode);
            }
    
            Debug.WriteLine($"[MDS] Request 1001 sent - expecting snapshot + real-time updates");
            Debug.WriteLine($"[MDS] Active subscriptions: {_activeSubscriptions.Count}");
            Debug.WriteLine($"[MDS] === REQUEST COMPLETE ===");
        }
        
        /// <summary>
        /// Wysyła request 1002 (Stop Refresh) aby zatrzymać real-time updates dla instrumentu
        /// </summary>
        public async Task StopInstrumentDetailsAsync(string glidAndStockcode)
        {
            if (!IsConnected || _stream == null)
            {
                Debug.WriteLine("[MDS] Cannot stop subscription - not connected");
                return;
            }
            
            lock (_subscriptionLock)
            {
                if (!_activeSubscriptions.Contains(glidAndStockcode))
                {
                    Debug.WriteLine($"[MDS] No active subscription for '{glidAndStockcode}'");
                    return;
                }
            }
    
            Debug.WriteLine($"[MDS] === SENDING REQUEST 1002 (STOP REFRESH) ===");
            Debug.WriteLine($"[MDS] Stopping real-time updates for: '{glidAndStockcode}'");
    
            byte[] request = BuildStopRefreshRequest(glidAndStockcode);
    
            Debug.WriteLine($"[MDS] Request built, size: {request.Length} bytes");
    
            await _stream.WriteAsync(request, 0, request.Length);
            await _stream.FlushAsync();
            
            // Usuń z aktywnych subskrypcji
            lock (_subscriptionLock)
            {
                _activeSubscriptions.Remove(glidAndStockcode);
                _instrumentDetailsCache.Remove(glidAndStockcode);
            }
    
            Debug.WriteLine($"[MDS] Request 1002 sent - real-time updates stopped");
            Debug.WriteLine($"[MDS] Active subscriptions: {_activeSubscriptions.Count}");
            Debug.WriteLine($"[MDS] === STOP COMPLETE ===");
        }
        
        /// <summary>
        /// Zwraca kopię słownika z cache szczegółów instrumentów
        /// </summary>
        /// <returns>Słownik z kluczem GlidAndSymbol i wartością InstrumentDetails</returns>
        public Dictionary<string, InstrumentDetails> GetInstrumentDetailsCache()
        {
            lock (_subscriptionLock)
            {
                // Zwróć kopię słownika aby zapobiec modyfikacjom z zewnątrz
                return new Dictionary<string, InstrumentDetails>(_instrumentDetailsCache);
            }
        }

        /// <summary>
        /// Ładuje szczegóły instrumentów z cache (używane przy odczycie z pliku)
        /// </summary>
        /// <param name="detailsCache">Słownik szczegółów do załadowania</param>
        public void LoadInstrumentDetailsCache(Dictionary<string, InstrumentDetails> detailsCache)
        {
            lock (_subscriptionLock)
            {
                _instrumentDetailsCache.Clear();
                foreach (var kvp in detailsCache)
                {
                    _instrumentDetailsCache[kvp.Key] = kvp.Value;
                }
                Debug.WriteLine($"[MDS] Loaded {_instrumentDetailsCache.Count} instrument details from cache");
            }
        }

        private void ProcessDictionaryResponse(byte[] response, int length, int stxPos)
        {
            var instruments = new List<Instrument>();
            try
            {
                int position = stxPos + HeaderLength;
                byte chaining = response[position++];
                int numberOfGlid = int.Parse(Encoding.ASCII.GetString(response, position, 5));
                position += 5;

                for (int i = 0; i < numberOfGlid; i++)
                {
                    string glidAndSymbol = DecodeField(response, ref position);  // Pozycja 0: GLID + Stockcode
                    string name = DecodeField(response, ref position);           // Pozycja 1: Stock name
                    string localCode = DecodeField(response, ref position);      // Pozycja 2: LOCAL CODE
                    string isin = DecodeField(response, ref position);           // Pozycja 3: ISIN code
                    DecodeField(response, ref position);                         // Pozycja 4: Quotation group number
                    
                    if (!string.IsNullOrEmpty(glidAndSymbol) && glidAndSymbol.Length >= 12)
                    {
                        var instrument = new Instrument
                        {
                            Glid = glidAndSymbol.Substring(0, 12),
                            Symbol = glidAndSymbol.Length > 12 ? glidAndSymbol.Substring(12) : "",
                            LocalCode = localCode,
                            Name = name,
                            ISIN = isin
                        };
                        if (!string.IsNullOrEmpty(instrument.Symbol)) instruments.Add(instrument);
                    }
                }
                if (instruments.Any()) InstrumentsReceived?.Invoke(instruments);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ProcessDictionaryResponse: {ex.Message}");
            }
        }

        private void ProcessInstrumentDetailsResponse(byte[] response, int length, int stxPos)
        {
            try
            {
                int pos = stxPos + HeaderLength;
                var details = new InstrumentDetails();

                // H0: Chaining (1 bajt ASCII)
                byte chaining = response[pos++];
                Debug.WriteLine($"[MDS] === PARSING INSTRUMENT DETAILS SNAPSHOT ===");
                Debug.WriteLine($"[MDS] Chaining: {chaining}");

                // H1: GLID + Stockcode (pole GL-encoded)
                details.GlidAndSymbol = DecodeField(response, ref pos);
                Debug.WriteLine($"[MDS] GlidAndSymbol: '{details.GlidAndSymbol}'");
                
                if (string.IsNullOrEmpty(details.GlidAndSymbol))
                {
                    Debug.WriteLine("[MDS] GlidAndSymbol is empty, skipping response");
                    return;
                }

                // H2: Filler 7 bajtów (ASCII spacje) - według dokumentacji Stock Watch 1000-1001
                Debug.WriteLine($"[MDS] Position before filler: {pos}, skipping 7-byte filler");
                pos += 7;
                Debug.WriteLine($"[MDS] Position after filler: {pos}");

                // Teraz dekodujemy wszystkie pola GL-encoded
                var allFields = new List<string>();
                while (pos < length - FooterLength)
                {
                    // Sprawdź czy nie dotarliśmy do footera (2 spacje + ETX)
                    if (pos + FooterLength <= length && response[pos + 2] == Etx)
                    {
                        Debug.WriteLine($"[MDS] Reached footer at position {pos}");
                        break;
                    }
                    
                    string fieldValue = DecodeField(response, ref pos);
                    allFields.Add(fieldValue);
                    
                    // Zabezpieczenie przed nieskończoną pętlą
                    if (allFields.Count > 300)
                    {
                        Debug.WriteLine("[MDS] Warning: Too many fields (>300), breaking");
                        break;
                    }
                }

                Debug.WriteLine($"[MDS] Total fields decoded: {allFields.Count}");
                
                // Mapuj wszystkie pola do obiektu details
                MapFieldsToDetails(details, allFields);

                // Cache snapshot dla późniejszych real-time updates
                lock (_subscriptionLock)
                {
                    _instrumentDetailsCache[details.GlidAndSymbol] = details;
                }

                Debug.WriteLine($"[MDS] === PARSING COMPLETE ===");

                InstrumentDetailsReceived?.Invoke(details);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDS] Failed to parse instrument details: {ex.Message}");
                Debug.WriteLine($"[MDS] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Przetwarza real-time update (request 1003) - zawiera tylko zmienione pola
        /// Format: H0: GLID+Stockcode, potem pary (field_number, field_value)
        /// </summary>
        private void ProcessRealTimeUpdate(byte[] response, int length, int stxPos)
        {
            try
            {
                int pos = stxPos + HeaderLength;
                
                Debug.WriteLine($"[MDS] === PARSING REAL-TIME UPDATE (1003) ===");
                
                // H0: GLID + Stockcode (GL-encoded)
                string glidAndSymbol = DecodeField(response, ref pos);
                Debug.WriteLine($"[MDS] GLID+Symbol: '{glidAndSymbol}'");
                
                if (string.IsNullOrEmpty(glidAndSymbol))
                {
                    Debug.WriteLine("[MDS] Empty GLID+Symbol in real-time update, skipping");
                    return;
                }
                
                // Pobierz cached details dla tego instrumentu
                InstrumentDetails? details = null;
                lock (_subscriptionLock)
                {
                    if (_instrumentDetailsCache.ContainsKey(glidAndSymbol))
                    {
                        // Sklonuj obiekt żeby móc go modyfikować
                        details = CloneDetails(_instrumentDetailsCache[glidAndSymbol]);
                    }
                }
                
                if (details == null)
                {
                    Debug.WriteLine($"[MDS] No cached snapshot for '{glidAndSymbol}', cannot apply updates");
                    Debug.WriteLine($"[MDS] Creating new InstrumentDetails object for this update");
                    details = new InstrumentDetails { GlidAndSymbol = glidAndSymbol };
                }
                
                // Parsuj pary (field_number, field_value) aż do końca wiadomości
                int updateCount = 0;
                while (pos < length - FooterLength)
                {
                    // Sprawdź czy nie dotarliśmy do footera
                    if (pos + FooterLength <= length && response[pos + 2] == Etx)
                    {
                        Debug.WriteLine($"[MDS] Reached footer at position {pos}");
                        break;
                    }
                    
                    // H1: Refreshed field number (GL_C format: 1 byte = value + 32)
                    if (pos >= length)
                        break;
                        
                    byte fieldNumberByte = response[pos++];
                    int fieldNumber = fieldNumberByte - 32; // GL_C decoding
                    
                    // H2: Refreshed field (GL-encoded)
                    string fieldValue = DecodeField(response, ref pos);
                    
                    Debug.WriteLine($"[MDS] Update field #{fieldNumber} = '{fieldValue}'");
                    
                    // Aktualizuj odpowiednie pole w details
                    UpdateDetailField(details, fieldNumber, fieldValue);
                    updateCount++;
                    
                    // Zabezpieczenie
                    if (updateCount > 300)
                    {
                        Debug.WriteLine("[MDS] Too many field updates (>300), breaking");
                        break;
                    }
                }
                
                Debug.WriteLine($"[MDS] Applied {updateCount} field updates");
                
                // Aktualizuj cache
                lock (_subscriptionLock)
                {
                    _instrumentDetailsCache[glidAndSymbol] = details;
                }
                
                Debug.WriteLine($"[MDS] === REAL-TIME UPDATE COMPLETE ===");
                
                // Powiadom subskrybentów o aktualizacji
                InstrumentDetailsReceived?.Invoke(details);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MDS] Failed to parse real-time update: {ex.Message}");
                Debug.WriteLine($"[MDS] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Aktualizuje pojedyncze pole w InstrumentDetails na podstawie numeru pola z real-time update
        /// </summary>
        private void UpdateDetailField(InstrumentDetails details, int fieldNumber, string fieldValue)
        {
            // Mapowanie numerów pól według dokumentacji Stock Watch
            switch (fieldNumber)
            {
                case 0: // Bid quantity
                    details.BidQuantity = ParseLong(fieldValue);
                    break;
                case 1: // Bid price
                    details.BidPrice = ParseDecimal(fieldValue);
                    break;
                case 2: // Ask price
                    details.AskPrice = ParseDecimal(fieldValue);
                    break;
                case 3: // Ask quantity
                    details.AskQuantity = ParseLong(fieldValue);
                    break;
                case 4: // Last traded price
                    details.LastPrice = ParseDecimal(fieldValue);
                    break;
                case 5: // Last traded quantity
                    details.LastQuantity = ParseLong(fieldValue);
                    break;
                case 6: // Last trade time
                    details.LastTradeTime = FormatTime(fieldValue);
                    break;
                case 8: // Percentage variation
                    details.PercentageVariation = ParseDecimal(fieldValue);
                    break;
                case 9: // Total quantity exchanged (Volume)
                    details.Volume = ParseLong(fieldValue);
                    break;
                case 10: // Opening price
                    details.OpenPrice = ParseDecimal(fieldValue);
                    break;
                case 11: // High
                    details.HighPrice = ParseDecimal(fieldValue);
                    break;
                case 12: // Low
                    details.LowPrice = ParseDecimal(fieldValue);
                    break;
                case 13: // Suspension indicator
                    details.SuspensionIndicator = fieldValue;
                    break;
                case 14: // Variation sign
                    details.VariationSign = fieldValue;
                    break;
                case 16: // Closing price
                    details.ClosePrice = ParseDecimal(fieldValue);
                    break;
                case 42: // Local code
                    details.LocalCode = fieldValue;
                    break;
                case 88: // ISIN code
                    details.ISIN = fieldValue;
                    break;
                case 140: // Trading phase
                    details.TradingPhase = fieldValue;
                    break;
                // Dodaj więcej pól według potrzeb...
                default:
                    Debug.WriteLine($"[MDS] Unhandled field number {fieldNumber} in real-time update");
                    break;
            }
        }
        
        /// <summary>
        /// Mapuje wszystkie pola z listy do obiektu InstrumentDetails
        /// </summary>
        private void MapFieldsToDetails(InstrumentDetails details, List<string> allFields)
        {
            // Wypisz wszystkie niepuste pola
            Debug.WriteLine($"[MDS] === NON-EMPTY FIELDS ===");
            for (int i = 0; i < allFields.Count; i++)
            {
                if (!string.IsNullOrEmpty(allFields[i]))
                {
                    Debug.WriteLine($"[MDS] Field[{i}] = '{allFields[i]}'");
                }
            }
            
            // Position 0: Bid quantity
            if (allFields.Count > 0) 
                details.BidQuantity = ParseLong(allFields[0]);
            
            // Position 1: Bid price
            if (allFields.Count > 1) 
                details.BidPrice = ParseDecimal(allFields[1]);
            
            // Position 2: Ask price
            if (allFields.Count > 2) 
                details.AskPrice = ParseDecimal(allFields[2]);
            
            // Position 3: Ask quantity
            if (allFields.Count > 3) 
                details.AskQuantity = ParseLong(allFields[3]);
            
            // Position 4: Last traded price
            if (allFields.Count > 4) 
                details.LastPrice = ParseDecimal(allFields[4]);
            
            // Position 5: Last traded quantity
            if (allFields.Count > 5) 
                details.LastQuantity = ParseLong(allFields[5]);
            
            // Position 6: Last trade time
            if (allFields.Count > 6)
            {
                details.LastTradeTime = FormatTime(allFields[6]);
                Debug.WriteLine($"[MDS] LastTradeTime: raw='{allFields[6]}' formatted='{details.LastTradeTime}'");
            }
            
            // Position 8: Percentage variation
            if (allFields.Count > 8) 
                details.PercentageVariation = ParseDecimal(allFields[8]);
            
            // Position 9: Total quantity exchanged (Volume)
            if (allFields.Count > 9) 
                details.Volume = ParseLong(allFields[9]);
            
            // Position 10: Opening price
            if (allFields.Count > 10) 
                details.OpenPrice = ParseDecimal(allFields[10]);
            
            // Position 11: High
            if (allFields.Count > 11) 
                details.HighPrice = ParseDecimal(allFields[11]);
            
            // Position 12: Low
            if (allFields.Count > 12) 
                details.LowPrice = ParseDecimal(allFields[12]);
            
            // Position 13: Suspension indicator
            if (allFields.Count > 13) 
                details.SuspensionIndicator = allFields[13];
            
            // Position 14: Variation sign
            if (allFields.Count > 14) 
                details.VariationSign = allFields[14];
            
            // Position 16: Closing price
            if (allFields.Count > 16) 
                details.ClosePrice = ParseDecimal(allFields[16]);
            
            // Position 42: Local code
            if (allFields.Count > 42)
            {
                details.LocalCode = allFields[42];
                Debug.WriteLine($"[MDS] LocalCode (position 42): '{details.LocalCode}'");
            }
            
            // Position 88: ISIN code
            if (allFields.Count > 88)
            {
                details.ISIN = allFields[88];
                Debug.WriteLine($"[MDS] ISIN (position 88): '{details.ISIN}'");
            }
            
            // Position 140: Trading phase
            if (allFields.Count > 140)
            {
                details.TradingPhase = allFields[140];
                Debug.WriteLine($"[MDS] TradingPhase (position 140): '{details.TradingPhase}'");
            }

            // Podsumowanie
            Debug.WriteLine($"[MDS] === PARSED VALUES ===");
            Debug.WriteLine($"[MDS] Prices: Bid={details.BidPrice:F2}, Ask={details.AskPrice:F2}, Last={details.LastPrice:F2}");
            Debug.WriteLine($"[MDS] Quantities: BidQty={details.BidQuantity}, AskQty={details.AskQuantity}, LastQty={details.LastQuantity}");
            Debug.WriteLine($"[MDS] OHLC: Open={details.OpenPrice:F2}, High={details.HighPrice:F2}, Low={details.LowPrice:F2}, Close={details.ClosePrice:F2}");
            Debug.WriteLine($"[MDS] Volume={details.Volume}, PercentageVar={details.PercentageVariation:F2}%");
            Debug.WriteLine($"[MDS] LocalCode='{details.LocalCode}', ISIN='{details.ISIN}', TradingPhase='{details.TradingPhase}'");
        }
        
        /// <summary>
        /// Klonuje obiekt InstrumentDetails (shallow copy wystarczy dla naszych potrzeb)
        /// </summary>
        private InstrumentDetails CloneDetails(InstrumentDetails source)
        {
            return new InstrumentDetails
            {
                GlidAndSymbol = source.GlidAndSymbol,
                BidQuantity = source.BidQuantity,
                BidPrice = source.BidPrice,
                AskPrice = source.AskPrice,
                AskQuantity = source.AskQuantity,
                LastPrice = source.LastPrice,
                LastQuantity = source.LastQuantity,
                LastTradeTime = source.LastTradeTime,
                PercentageVariation = source.PercentageVariation,
                Volume = source.Volume,
                OpenPrice = source.OpenPrice,
                HighPrice = source.HighPrice,
                LowPrice = source.LowPrice,
                ClosePrice = source.ClosePrice,
                SuspensionIndicator = source.SuspensionIndicator,
                VariationSign = source.VariationSign,
                LocalCode = source.LocalCode,
                ISIN = source.ISIN,
                TradingPhase = source.TradingPhase
            };
        }
        
        private string FormatTime(string rawTime)
        {
            if (string.IsNullOrEmpty(rawTime)) return string.Empty;
    
            try
            {
                // Format: HHMMSS.microseconds lub HHMMSSmmm
                // Przykład: "95018.825883" = 09:50:18
        
                // Usuń część po kropce (mikrosekundy)
                string timePart = rawTime.Split('.')[0];
        
                // Pad do 6 cyfr jeśli krótsza
                timePart = timePart.PadLeft(6, '0');
        
                if (timePart.Length >= 6)
                {
                    string hours = timePart.Substring(0, 2);
                    string minutes = timePart.Substring(2, 2);
                    string seconds = timePart.Substring(4, 2);
            
                    return $"{hours}:{minutes}:{seconds}";
                }
        
                return rawTime;
            }
            catch
            {
                return rawTime;
            }
        }

        #region Message Builders
        
        /// <summary>
        /// Buduje request Stock Watch - snapshot (1000) lub refreshed (1001)
        /// </summary>
        private byte[] BuildStockWatchRequest(string glidAndStockcode, bool useRealTime = false)
        {
            var dataBuilder = new List<byte>();
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 7))); // H0: Filler (7 spaces)
            dataBuilder.AddRange(EncodeField(glidAndStockcode));                // H1: GLID + Stockcode
            
            // 1000 = snapshot tylko, 1001 = snapshot + real-time updates
            int requestNumber = useRealTime ? 1001 : 1000;
            
            return BuildMessage(dataBuilder.ToArray(), requestNumber);
        }
        
        /// <summary>
        /// Buduje request 1002 (Stop Refresh) aby zatrzymać real-time updates
        /// </summary>
        private byte[] BuildStopRefreshRequest(string glidAndStockcode)
        {
            var dataBuilder = new List<byte>();
            dataBuilder.AddRange(EncodeField(glidAndStockcode)); // H0: GLID + Stockcode
            return BuildMessage(dataBuilder.ToArray(), 1002);
        }

        private byte[] BuildDictionaryRequest(string glid)
        {
            var dataBuilder = new List<byte>();
            dataBuilder.AddRange(Encoding.ASCII.GetBytes("00001"));
            dataBuilder.AddRange(EncodeField(glid));
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 5108); 
        }

        private byte[] BuildLoginRequest(string user, string password)
        {
            var dataBuilder = new List<byte>();
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(user.PadLeft(3, '0')));
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(password.PadRight(16, ' ')));
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 7)));
            dataBuilder.AddRange(EncodeField("15"));
            dataBuilder.AddRange(EncodeField("V5"));
            dataBuilder.AddRange(EncodeField("26"));
            dataBuilder.AddRange(EncodeField(user));
            var dataPayload = dataBuilder.ToArray();
            return BuildMessage(dataPayload, 1100);
        }

        private byte[] BuildMessage(byte[] dataPayload, int requestNumber)
        {
            int dataLength = dataPayload.Length;
            int totalLength = 2 + HeaderLength + dataLength + FooterLength;
            var message = new byte[totalLength];

            using (var ms = new MemoryStream(message))
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)(totalLength % 256));
                writer.Write((byte)(totalLength / 256));
                writer.Write(Stx);
                writer.Write((byte)'0');
                writer.Write(Encoding.ASCII.GetBytes((HeaderLength + dataLength + FooterLength).ToString().PadLeft(5, '0')));
                writer.Write(Encoding.ASCII.GetBytes(_subnode.PadLeft(5, '0')));
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 5)));
                writer.Write(Encoding.ASCII.GetBytes("00000"));
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                writer.Write(Encoding.ASCII.GetBytes(requestNumber.ToString().PadLeft(5, '0')));
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 3)));
                writer.Write(dataPayload);
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

        private string DecodeField(byte[] data, ref int position)
        {
            try
            {
                if (position >= data.Length) return string.Empty;
                var fieldLength = data[position] - 32;
        
                if (fieldLength <= 0 || position + 1 + fieldLength > data.Length) 
                {
                    // Pole puste lub błędne - przeskocz tylko bajt długości
                    if (fieldLength == 0)
                    {
                        position++; // Przeskocz bajt długości dla pustego pola
                    }
                    return string.Empty;
                }
                
                var value = Encoding.ASCII.GetString(data, position + 1, fieldLength);
                position += 1 + fieldLength;
                return value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decoding field at position {position}: {ex.Message}");
                return string.Empty;
            }
        }

        private bool VerifyLoginResponse(byte[] response, int length)
        {
            if (length < HeaderLength + 2) return false;
            string requestNumberStr = Encoding.ASCII.GetString(response, 26, 5);
            return requestNumberStr == "01100";
        }

        private long ParseLong(string value) => long.TryParse(value, out var result) ? result : 0;
        private decimal ParseDecimal(string value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;

        #endregion
    }
}