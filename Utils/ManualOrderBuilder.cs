using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FISApiClient.Utils
{
    /// <summary>
    /// Klasa do ręcznego budowania komunikatów FIS API SLE z kodowaniem GL
    /// </summary>
    public class ManualOrderBuilder
    {
        private const byte STX = 0x02;
        private const byte ETX = 0x03;
        private const int HeaderLength = 32;
        private const int FooterLength = 3;

        // Parametry połączenia - do ustawienia przez użytkownika
        public string UserNumber { get; set; } = "00001";  // 5 cyfr
        public string CalledLogicalId { get; set; } = "25503";  // Subnode serwera
        public string CallingLogicalId { get; set; } = "00001";  // Assigned ID

        /// <summary>
        /// Buduje kompletny komunikat zlecenia sprzedaży 1 akcji PEKAO za 160 zł
        /// </summary>
        public byte[] BuildSellOrderPEKAO()
        {
            // Parametry zlecenia
            string localCode = "PEKAO";      // LocalCode instrumentu
            string glid = "1063";             // GLID PEKAO
            int side = 1;                     // 1 = Sprzedaż (Sell)
            long quantity = 1;                // Ilość
            string modality = "L";            // L = Limit
            decimal price = 160.00m;          // Cena
            string validity = "D";            // D = Day order

            return BuildOrderRequest(localCode, glid, side, quantity, modality, price, validity);
        }

        /// <summary>
        /// Buduje komunikat request 2000 (Order Placement)
        /// </summary>
        private byte[] BuildOrderRequest(
            string localCode,
            string glid,
            int side,
            long quantity,
            string modality,
            decimal price,
            string validity)
        {
            Console.WriteLine("=== BUILDING ORDER REQUEST 2000 ===");
            Console.WriteLine($"LocalCode: {localCode}");
            Console.WriteLine($"GLID: {glid}");
            Console.WriteLine($"Side: {side} (1=Sell, 2=Buy)");
            Console.WriteLine($"Quantity: {quantity}");
            Console.WriteLine($"Modality: {modality}");
            Console.WriteLine($"Price: {price:F2}");
            Console.WriteLine($"Validity: {validity}");

            // === BUDOWA DATA PAYLOAD ===
            var dataBuilder = new List<byte>();

            // HEADER DATA (przed bitmap)
            // B: User Number (5 bajtów ASCII)
            string userNum = UserNumber.PadLeft(5, '0');
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(userNum));
            Console.WriteLine($"User Number: {userNum}");

            // C: Request Category (1 bajt) - 'O' = Simple order
            dataBuilder.Add((byte)'O');
            Console.WriteLine("Request Category: O (Simple order)");

            // D1: Command (1 bajt) - '0' = New order
            dataBuilder.Add((byte)'0');
            Console.WriteLine("Command: 0 (New order)");

            // G: Stockcode (GL encoded) - używamy LOCAL CODE
            byte[] stockcodeGL = EncodeFieldGL(localCode);
            dataBuilder.AddRange(stockcodeGL);
            Console.WriteLine($"Stockcode (GL): {localCode} -> {BitConverter.ToString(stockcodeGL)}");

            // Filler (10 bajtów)
            dataBuilder.AddRange(Encoding.ASCII.GetBytes(new string(' ', 10)));

            // === BITMAP ===
            Console.WriteLine("\n--- BITMAP FIELDS ---");

            // Field 0: Side (MANDATORY)
            dataBuilder.AddRange(EncodeFieldGL("0"));        // Field ID
            dataBuilder.AddRange(EncodeFieldGL(side.ToString()));  // Value
            Console.WriteLine($"Field #0 (Side): {side}");

            // Field 1: Quantity (MANDATORY)
            dataBuilder.AddRange(EncodeFieldGL("1"));
            dataBuilder.AddRange(EncodeFieldGL(quantity.ToString()));
            Console.WriteLine($"Field #1 (Quantity): {quantity}");

            // Field 2: Modality (MANDATORY)
            dataBuilder.AddRange(EncodeFieldGL("2"));
            dataBuilder.AddRange(EncodeFieldGL(modality));
            Console.WriteLine($"Field #2 (Modality): {modality}");

            // Field 3: Price (wymagane dla Limit orders)
            if (modality == "L" && price > 0)
            {
                dataBuilder.AddRange(EncodeFieldGL("3"));
                string priceStr = price.ToString("F2", CultureInfo.InvariantCulture);
                dataBuilder.AddRange(EncodeFieldGL(priceStr));
                Console.WriteLine($"Field #3 (Price): {priceStr}");
            }

            // Field 4: Validity (MANDATORY)
            dataBuilder.AddRange(EncodeFieldGL("4"));
            dataBuilder.AddRange(EncodeFieldGL(validity));
            Console.WriteLine($"Field #4 (Validity): {validity}");

            // Field 106: GLID (MANDATORY w dokumentacji WSE)
            dataBuilder.AddRange(EncodeFieldGL("106"));
            dataBuilder.AddRange(EncodeFieldGL(glid));
            Console.WriteLine($"Field #106 (GLID): {glid}");

            // Field 114: Currency (opcjonalne, ale zalecane)
            dataBuilder.AddRange(EncodeFieldGL("114"));
            dataBuilder.AddRange(EncodeFieldGL("PLN"));
            Console.WriteLine($"Field #114 (Currency): PLN");

            var dataPayload = dataBuilder.ToArray();
            Console.WriteLine($"\nData payload size: {dataPayload.Length} bytes");

            // === BUDOWA PEŁNEGO KOMUNIKATU ===
            return BuildFullMessage(dataPayload, 2000);
        }

        /// <summary>
        /// Buduje pełny komunikat FIS API z LG, Header, Data i Footer
        /// </summary>
        private byte[] BuildFullMessage(byte[] dataPayload, int requestNumber)
        {
            int contentLength = HeaderLength + dataPayload.Length + FooterLength;
            int totalLength = 2 + contentLength; // LG (2 bytes) + content

            var message = new byte[totalLength];

            using (var ms = new MemoryStream(message))
            using (var writer = new BinaryWriter(ms))
            {
                // === LG (2 bajty) - Little Endian ===
                writer.Write((byte)(totalLength % 256));
                writer.Write((byte)(totalLength / 256));

                // === HEADER (32 bajty) ===
                
                // STX (1 bajt) = 0x02
                writer.Write(STX);

                // API Version (1 bajt) = SPACJA dla SLE V4!
                writer.Write((byte)' ');  // ⚠️ KLUCZOWE: nie '5', tylko SPACJA!

                // Length (5 bajtów ASCII)
                writer.Write(Encoding.ASCII.GetBytes(contentLength.ToString().PadLeft(5, '0')));

                // Called logical ID (5 bajtów) - server subnode
                writer.Write(Encoding.ASCII.GetBytes(CalledLogicalId.PadLeft(5, '0')));

                // Filler (5 bajtów)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 5)));

                // Calling logical ID (5 bajtów) - nasz assigned ID
                writer.Write(Encoding.ASCII.GetBytes(CallingLogicalId.PadLeft(5, '0')));

                // Filler (2 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));

                // Request number (5 bajtów)
                writer.Write(Encoding.ASCII.GetBytes(requestNumber.ToString().PadLeft(5, '0')));

                // Filler (3 bajty)
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 3)));

                // === DATA ===
                writer.Write(dataPayload);

                // === FOOTER (3 bajty) ===
                writer.Write(Encoding.ASCII.GetBytes(new string(' ', 2)));
                writer.Write(ETX);  // 0x03
            }

            // === HEX DUMP ===
            Console.WriteLine("\n=== COMPLETE MESSAGE HEX DUMP ===");
            Console.WriteLine($"Total length: {totalLength} bytes (0x{totalLength:X})");
            
            // LG
            Console.WriteLine($"\nLG: {BitConverter.ToString(message, 0, 2)}");
            Console.WriteLine($"  Calculated: {totalLength} = {totalLength % 256} + 256*{totalLength / 256}");
            
            // Header
            Console.WriteLine($"\nHEADER (32 bytes):");
            Console.WriteLine($"  STX (pos 2): 0x{message[2]:X2}");
            Console.WriteLine($"  API Ver (pos 3): 0x{message[3]:X2} = '{(char)message[3]}'");
            Console.WriteLine($"  Length (pos 4-8): {Encoding.ASCII.GetString(message, 4, 5)}");
            Console.WriteLine($"  Called ID (pos 9-13): {Encoding.ASCII.GetString(message, 9, 5)}");
            Console.WriteLine($"  Calling ID (pos 19-23): {Encoding.ASCII.GetString(message, 19, 5)}");
            Console.WriteLine($"  Request # (pos 26-30): {Encoding.ASCII.GetString(message, 26, 5)}");

            // First 100 bytes hex
            Console.WriteLine($"\nFirst bytes (hex):");
            for (int i = 0; i < Math.Min(150, message.Length); i += 32)
            {
                int len = Math.Min(32, message.Length - i);
                string hex = BitConverter.ToString(message, i, len).Replace("-", " ");
                Console.WriteLine($"  {i:D4}: {hex}");
            }

            Console.WriteLine("\n=== END MESSAGE ===\n");

            return message;
        }

        /// <summary>
        /// Koduje pole w formacie GL
        /// Format: [LENGTH+32][DATA]
        /// </summary>
        private byte[] EncodeFieldGL(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                // Puste pole = długość 1, jedna spacja
                return new byte[] { 33, 32 };  // 33 = 1+32, 32 = ' '
            }

            byte[] valueBytes = Encoding.ASCII.GetBytes(value);
            byte[] encoded = new byte[valueBytes.Length + 1];

            // Pierwszy bajt = długość + 32
            encoded[0] = (byte)(valueBytes.Length + 32);

            // Następne bajty = dane
            Array.Copy(valueBytes, 0, encoded, 1, valueBytes.Length);

            return encoded;
        }

        /// <summary>
        /// Dekoduje pole GL (do testowania odpowiedzi)
        /// </summary>
        private (string value, int bytesRead) DecodeFieldGL(byte[] data, int startPos)
        {
            if (startPos >= data.Length)
                return (string.Empty, 0);

            int length = data[startPos] - 32;

            if (length <= 0 || startPos + 1 + length > data.Length)
                return (string.Empty, 0);

            string value = Encoding.ASCII.GetString(data, startPos + 1, length);
            return (value, 1 + length);
        }
    }
}