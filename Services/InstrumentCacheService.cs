using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FISApiClient.Models;

namespace FISApiClient.Services
{
    /// <summary>
    /// Serwis do cache'owania listy instrumentów i ich szczegółów
    /// </summary>
    public class InstrumentCacheService
    {
        private readonly string _cacheDirectory;
        private const string FILE_PREFIX = "instruments_";
        private const string FILE_EXTENSION = ".json";

        public InstrumentCacheService()
        {
            // Katalog cache w AppData użytkownika
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheDirectory = Path.Combine(appData, "FISApiClient", "cache");

            // Utwórz katalog jeśli nie istnieje
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
                Debug.WriteLine($"[Cache] Created cache directory: {_cacheDirectory}");
            }
        }

        /// <summary>
        /// Zwraca nazwę pliku cache dla dzisiejszej daty
        /// Format: instruments_2025-10-23.json
        /// </summary>
        private string GetTodayCacheFileName()
        {
            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            return $"{FILE_PREFIX}{dateString}{FILE_EXTENSION}";
        }

        /// <summary>
        /// Zwraca pełną ścieżkę do pliku cache dla dzisiejszej daty
        /// </summary>
        private string GetTodayCacheFilePath()
        {
            return Path.Combine(_cacheDirectory, GetTodayCacheFileName());
        }

        /// <summary>
        /// Sprawdza czy istnieje cache dla dzisiejszej daty
        /// </summary>
        public bool HasTodayCache()
        {
            string filePath = GetTodayCacheFilePath();
            bool exists = File.Exists(filePath);
            
            Debug.WriteLine($"[Cache] Checking for today's cache: {filePath}");
            Debug.WriteLine($"[Cache] Cache exists: {exists}");
            
            return exists;
        }

        /// <summary>
        /// Zapisuje instrumenty i ich szczegóły do cache
        /// </summary>
        public async Task SaveCacheAsync(
            List<Instrument> instruments, 
            Dictionary<string, InstrumentDetails> detailsCache)
        {
            try
            {
                string filePath = GetTodayCacheFilePath();
                
                Debug.WriteLine($"[Cache] Saving cache to: {filePath}");
                Debug.WriteLine($"[Cache] Instruments count: {instruments.Count}");
                Debug.WriteLine($"[Cache] Details count: {detailsCache.Count}");

                // Utwórz obiekt do serializacji
                var cacheData = new CachedInstrumentData
                {
                    CacheDate = DateTime.Now,
                    Instruments = instruments,
                    InstrumentDetails = detailsCache.Values.ToList()
                };

                // Serializuj do JSON z ładnym formatowaniem
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(cacheData, options);

                // Zapisz do pliku asynchronicznie
                await File.WriteAllTextAsync(filePath, json);

                Debug.WriteLine($"[Cache] Cache saved successfully");
                
                // Usuń stare pliki cache
                await CleanupOldCacheFilesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] Error saving cache: {ex.Message}");
                Debug.WriteLine($"[Cache] Stack trace: {ex.StackTrace}");
                // Nie rzucaj wyjątku - błąd cache nie powinien blokować aplikacji
            }
        }

        /// <summary>
        /// Ładuje instrumenty i ich szczegóły z cache
        /// </summary>
        public async Task<(List<Instrument> instruments, Dictionary<string, InstrumentDetails> details)?> LoadCacheAsync()
        {
            try
            {
                string filePath = GetTodayCacheFilePath();

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[Cache] Cache file not found: {filePath}");
                    return null;
                }

                Debug.WriteLine($"[Cache] Loading cache from: {filePath}");

                // Wczytaj plik
                string json = await File.ReadAllTextAsync(filePath);

                // Deserializuj
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var cacheData = JsonSerializer.Deserialize<CachedInstrumentData>(json, options);

                if (cacheData == null)
                {
                    Debug.WriteLine($"[Cache] Failed to deserialize cache data");
                    return null;
                }

                Debug.WriteLine($"[Cache] Cache loaded successfully");
                Debug.WriteLine($"[Cache] Cache date: {cacheData.CacheDate}");
                Debug.WriteLine($"[Cache] Instruments count: {cacheData.Instruments.Count}");
                Debug.WriteLine($"[Cache] Details count: {cacheData.InstrumentDetails.Count}");

                // Konwertuj listę szczegółów na słownik
                var detailsDict = cacheData.InstrumentDetails
                    .ToDictionary(d => d.GlidAndSymbol, d => d);

                return (cacheData.Instruments, detailsDict);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] Error loading cache: {ex.Message}");
                Debug.WriteLine($"[Cache] Stack trace: {ex.StackTrace}");
                
                // Jeśli plik jest uszkodzony, usuń go
                try
                {
                    string filePath = GetTodayCacheFilePath();
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Debug.WriteLine($"[Cache] Deleted corrupted cache file");
                    }
                }
                catch
                {
                    // Ignoruj błędy przy usuwaniu
                }

                return null;
            }
        }

        /// <summary>
        /// Usuwa pliki cache starsze niż dzisiejsza data
        /// </summary>
        private async Task CleanupOldCacheFilesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_cacheDirectory))
                        return;

                    string todayFileName = GetTodayCacheFileName();
                    var files = Directory.GetFiles(_cacheDirectory, $"{FILE_PREFIX}*{FILE_EXTENSION}");

                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        
                        // Nie usuwaj dzisiejszego pliku
                        if (fileName == todayFileName)
                            continue;

                        try
                        {
                            File.Delete(file);
                            Debug.WriteLine($"[Cache] Deleted old cache file: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Cache] Failed to delete old cache file {fileName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Cache] Error during cleanup: {ex.Message}");
                }
            });
        }
        
        

        /// <summary>
        /// Ręcznie usuwa dzisiejszy cache (do wymuszenia odświeżenia)
        /// </summary>
        public void ClearTodayCache()
        {
            try
            {
                string filePath = GetTodayCacheFilePath();
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.WriteLine($"[Cache] Today's cache cleared");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] Error clearing cache: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Zwraca słownik mapujący LocalCode -> Instrument dla szybkiego wyszukiwania
        /// </summary>
        public Dictionary<string, Instrument> GetLocalCodeToInstrumentMap(List<Instrument> instruments)
        {
            var map = new Dictionary<string, Instrument>(StringComparer.OrdinalIgnoreCase);
    
            foreach (var instrument in instruments)
            {
                if (!string.IsNullOrEmpty(instrument.LocalCode))
                {
                    // Jeśli jest duplikat, zachowaj pierwszy
                    if (!map.ContainsKey(instrument.LocalCode))
                    {
                        map[instrument.LocalCode] = instrument;
                    }
                }
            }
    
            Debug.WriteLine($"[Cache] Created LocalCode map with {map.Count} entries");
            return map;
        }

        /// <summary>
        /// Znajduje instrument po LocalCode
        /// </summary>
        public Instrument? FindInstrumentByLocalCode(List<Instrument> instruments, string localCode)
        {
            if (string.IsNullOrEmpty(localCode))
                return null;
        
            return instruments.FirstOrDefault(i => 
                string.Equals(i.LocalCode, localCode, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Klasa do serializacji danych cache do JSON
    /// </summary>
    internal class CachedInstrumentData
    {
        public DateTime CacheDate { get; set; }
        public List<Instrument> Instruments { get; set; } = new();
        public List<InstrumentDetails> InstrumentDetails { get; set; } = new();
    }
    
    
}
