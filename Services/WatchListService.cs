using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FISApiClient.Models;

namespace FISApiClient.Services
{
    public static class WatchListService
    {
        public static async Task<List<Instrument>> LoadWatchlistFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Plik listy obserwowanych nie został znaleziony.", filePath);
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<Instrument>();
                }
                
                var instruments = JsonSerializer.Deserialize<List<Instrument>>(json);
                return instruments ?? new List<Instrument>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading watchlist from {filePath}: {ex.Message}");
                // Rzuć wyjątek dalej, aby ViewModel mógł go obsłużyć
                throw new InvalidDataException($"Plik '{Path.GetFileName(filePath)}' jest uszkodzony lub ma nieprawidłowy format.", ex);
            }
        }

        public static async Task SaveWatchlistToFileAsync(string filePath, IEnumerable<Instrument> instruments)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(instruments.ToList(), options);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving watchlist to {filePath}: {ex.Message}");
                // Rzuć wyjątek dalej, aby ViewModel mógł go obsłużyć
                throw new IOException($"Nie udało się zapisać pliku '{Path.GetFileName(filePath)}'.", ex);
            }
        }
    }
}
