# Przewodnik Użytkownika - FIS API Client

## Spis treści
1. [Wprowadzenie](#wprowadzenie)
2. [Instalacja i uruchomienie](#instalacja-i-uruchomienie)
3. [Łączenie z serwerem](#łączenie-z-serwerem)
4. [Rozwiązywanie problemów](#rozwiązywanie-problemów)
5. [FAQ](#faq)

---

## Wprowadzenie

FIS API Client to aplikacja Windows (WPF) służąca do połączenia z serwerem Market Data Server (MDS/SLC) na giełdzie WSE (Warsaw Stock Exchange). Aplikacja umożliwia:

- ✅ Połączenie z serwerem MDS/SLC
- ✅ Pobieranie listy instrumentów finansowych
- ✅ Otrzymywanie szczegółowych danych o instrumentach
- ✅ Subskrypcję aktualizacji w czasie rzeczywistym

---

## Instalacja i uruchomienie

### Wymagania systemowe

- **System operacyjny**: Windows 10 lub nowszy (64-bit)
- **.NET**: .NET 8.0 Runtime lub SDK
- **Pamięć RAM**: Minimum 4 GB
- **Połączenie sieciowe**: Dostęp do serwera MDS (192.168.45.25:25503)

### Instalacja

1. **Pobierz .NET 8.0 Runtime**
   - Odwiedź: https://dotnet.microsoft.com/download/dotnet/8.0
   - Pobierz "Desktop Runtime" dla Windows

2. **Rozpakuj aplikację**
   - Rozpakuj archiwum ZIP do wybranego folderu
   - Upewnij się, że wszystkie pliki są rozpakowane

3. **Uruchom aplikację**
   - Kliknij dwukrotnie na plik `FISApiClient.exe`
   - Lub uruchom z wiersza poleceń:
     ```cmd
     cd C:\ścieżka\do\FISApiClient
     FISApiClient.exe
     ```

### Uruchomienie z Visual Studio (dla deweloperów)

1. Otwórz `FISApiClient.sln` w Visual Studio 2022
2. Przywróć pakiety NuGet (automatycznie przy pierwszym otwarciu)
3. Naciśnij F5 lub kliknij "Start" (zielona strzałka)

---

## Łączenie z serwerem

### Krok 1: Uruchom aplikację

Po uruchomieniu zobaczysz główne okno z formularzem połączenia:

```
┌─────────────────────────────────────────┐
│  FIS API Client                          │
│  Połączenie z serwerem Market Data      │
├─────────────────────────────────────────┤
│                                          │
│  Status: ● Rozłączono                   │
│                                          │
│  Adres IP:     [192.168.45.25]          │
│  Port:         [25503]                   │
│  Użytkownik:   [103]                     │
│  Hasło:        [******]                  │
│  Node:         [5500]                    │
│  Subnode:      [4500]                    │
│                                          │
│     [Połącz]        [Rozłącz]           │
│                                          │
├─────────────────────────────────────────┤
│  Gotowy do połączenia                    │
└─────────────────────────────────────────┘
```

### Krok 2: Sprawdź parametry połączenia

**Domyślne wartości** (już wypełnione):

| Parametr    | Wartość domyślna | Opis                                    |
|-------------|------------------|-----------------------------------------|
| Adres IP    | 192.168.45.25    | Adres IP serwera MDS                    |
| Port        | 25503            | Port TCP dla MDS/SLC                    |
| Użytkownik  | 103              | Numer użytkownika (User ID)             |
| Hasło       | glglgl           | Hasło użytkownika                       |
| Node        | 5500             | Identyfikator węzła (Node ID)           |
| Subnode     | 4500             | Identyfikator podwęzła (Subnode ID)     |

**Uwaga**: Te wartości można modyfikować tylko gdy aplikacja jest rozłączona.

### Krok 3: Kliknij "Połącz"

1. Kliknij przycisk **"Połącz"**
2. Aplikacja spróbuje nawiązać połączenie z serwerem
3. Zobaczysz komunikat "Łączenie z serwerem MDS..."

### Krok 4: Weryfikacja połączenia

**Połączenie pomyślne:**
```
┌─────────────────────────────────────────┐
│  Status: ● Połączono                    │
│         (zielony wskaźnik)              │
└─────────────────────────────────────────┘
```
- Wyświetli się okno z komunikatem sukcesu
- Wskaźnik statusu zmieni kolor na zielony
- Przycisk "Połącz" zostanie wyłączony
- Przycisk "Rozłącz" zostanie aktywowany
- Pasek statusu: "Pomyślnie połączono z 192.168.45.25:25503"

**Połączenie nieudane:**
```
┌─────────────────────────────────────────┐
│  Status: ● Rozłączono                   │
│         (czerwony wskaźnik)             │
└─────────────────────────────────────────┘
```
- Wyświetli się okno z komunikatem błędu
- Wskaźnik pozostanie czerwony
- Pasek statusu wyświetli szczegóły błędu

### Krok 5: Rozłączenie

Aby rozłączyć się z serwerem:
1. Kliknij przycisk **"Rozłącz"**
2. Połączenie zostanie zamknięte
3. Wszystkie pola formularza będą ponownie edytowalne

---

## Rozwiązywanie problemów

### Problem 1: "Nie można połączyć z serwerem"

**Możliwe przyczyny:**
- ❌ Serwer jest wyłączony lub niedostępny
- ❌ Błędny adres IP lub port
- ❌ Firewall blokuje połączenie
- ❌ Brak połączenia sieciowego

**Rozwiązania:**
1. **Sprawdź dostępność serwera:**
   ```cmd
   ping 192.168.45.25
   ```
   Powinny pojawić się odpowiedzi. Jeśli "Request timed out" - serwer jest niedostępny.

2. **Sprawdź port:**
   ```cmd
   telnet 192.168.45.25 25503
   ```
   Jeśli połączenie się powiedzie - port jest otwarty.

3. **Sprawdź firewall:**
   - Otwórz "Windows Defender Firewall"
   - Dodaj wyjątek dla FISApiClient.exe
   - Upewnij się, że port 25503 jest otwarty

4. **Zweryfikuj dane:**
   - Sprawdź czy IP, port są poprawne
   - Zapytaj administratora sieci o aktualny adres serwera

### Problem 2: "Nieprawidłowe dane logowania"

**Możliwe przyczyny:**
- ❌ Błędny user ID (103)
- ❌ Błędne hasło (glglgl)
- ❌ Konto jest zablokowane
- ❌ Nieprawidłowy node lub subnode

**Rozwiązania:**
1. Sprawdź dane logowania z administratorem
2. Upewnij się, że konto nie jest używane gdzie indziej (błąd 59: "Already connected")
3. Zweryfikuj wartości Node (5500) i Subnode (4500)

### Problem 3: Aplikacja się zawiesza

**Rozwiązania:**
1. Zamknij aplikację (Alt+F4 lub Task Manager)
2. Uruchom ponownie
3. Sprawdź logi w Output Window (jeśli uruchamiasz z Visual Studio)
4. Sprawdź czy nie ma konfliktów z innymi aplikacjami

### Problem 4: Aplikacja nie uruchamia się

**Możliwe przyczyny:**
- ❌ Brak .NET 8.0 Runtime
- ❌ Brakujące pliki aplikacji
- ❌ Niekompatybilna wersja Windows

**Rozwiązania:**
1. **Zainstaluj .NET 8.0 Runtime:**
   - Pobierz z: https://dotnet.microsoft.com/download/dotnet/8.0
   - Zainstaluj "Desktop Runtime"
   - Uruchom ponownie komputer

2. **Sprawdź integralność plików:**
   ```cmd
   dir FISApiClient.exe
   dir FISApiClient.dll
   ```
   Jeśli pliki nie istnieją - rozpakuj ponownie archiwum.

3. **Sprawdź wersję Windows:**
   - Minimum: Windows 10 (wersja 1607 lub nowsza)
   - Zalecane: Windows 11

### Problem 5: Brak aktualizacji w czasie rzeczywistym

**Uwaga**: Obecna wersja (v1.0) skupia się na połączeniu. Funkcjonalność real-time będzie dodana w przyszłych wersjach.

---

## FAQ

### Q1: Czy mogę używać aplikacji na więcej niż jednym komputerze jednocześnie?

**A:** Tak, ale każde połączenie wymaga osobnego user ID. Jeśli spróbujesz zalogować się tym samym kontem z dwóch miejsc, otrzymasz błąd "Already connected" (kod 59).

### Q2: Jakie giełdy są obsługiwane?

**A:** Aplikacja jest skonfigurowana dla WSE (Warsaw Stock Exchange):
- Exchange 40: GPW Główny Rynek
- Exchange 330: NewConnect
- Exchange 331: Catalyst
- Exchange 332: Structured Products

### Q3: Czy mogę zmienić parametry połączenia?

**A:** Tak, wszystkie parametry można edytować gdy aplikacja jest rozłączona. Po wprowadzeniu zmian kliknij "Połącz" aby nawiązać nowe połączenie.

### Q4: Co oznaczają poszczególne pola?

- **Adres IP**: Adres serwera MDS
- **Port**: Port TCP (zazwyczaj 25503 dla MDS)
- **Użytkownik**: Unikalny numer identyfikacyjny użytkownika (3 cyfry)
- **Hasło**: Hasło do konta (maksymalnie 16 znaków)
- **Node**: Identyfikator węzła logicznego (Node ID)
- **Subnode**: Identyfikator podwęzła (Subnode ID) - używany jako "Called Logical ID" w nagłówku wiadomości

### Q5: Jak długo trwa nawiązanie połączenia?

**A:** Zazwyczaj 1-3 sekundy. Jeśli trwa dłużej niż 10 sekund, sprawdź połączenie sieciowe.

### Q6: Czy aplikacja zapisuje hasło?

**A:** Nie, aplikacja nie zapisuje żadnych danych logowania na dysku. Po zamknięciu aplikacji wszystkie dane są usuwane z pamięci.

### Q7: Co zrobić jeśli serwer nie odpowiada?

**A:**
1. Sprawdź czy serwer jest włączony (ping)
2. Skontaktuj się z administratorem serwera
3. Sprawdź czy masz odpowiednie uprawnienia sieciowe
4. Zweryfikuj konfigurację firewall

### Q8: Czy mogę zobaczyć surowe wiadomości protokołu?

**A:** Jeśli uruchamiasz aplikację z Visual Studio w trybie Debug:
1. Otwórz "Output Window" (View → Output)
2. Wybierz "Debug" z dropdown
3. Wszystkie wiadomości są logowane przez `Debug.WriteLine()`

### Q9: Jakie kody błędów mogę otrzymać?

| Kod | Znaczenie |
|-----|-----------|
| 0   | Nieprawidłowe hasło |
| 1   | Brak miejsca w bazie połączeń |
| 2   | Nieprawidłowy format żądania |
| 3   | Zabroniony numer użytkownika |
| 4   | Nieznany użytkownik |
| 52  | Złe hasło |
| 59  | Już połączony (użytkownik jest już zalogowany) |

### Q10: Czy aplikacja wymaga uprawnień administratora?

**A:** Nie, aplikacja działa z prawami standardowego użytkownika. Jednak firewall może wymagać potwierdzenia przy pierwszym uruchomieniu.

---

## Wsparcie techniczne

### Kontakt

Jeśli masz pytania lub problemy:
1. Sprawdź ten przewodnik
2. Sprawdź sekcję "Rozwiązywanie problemów"
3. Skontaktuj się z administratorem systemu

### Logi debugowania

Jeśli zgłaszasz problem, dołącz:
1. Wersję aplikacji (v1.0)
2. Wersję Windows
3. Komunikat błędu (zrzut ekranu)
4. Logi z Output Window (jeśli dostępne)

---

## Historia wersji

### v1.0 (Obecna wersja)
- ✅ Połączenie z serwerem MDS/SLC
- ✅ Logowanie (Request 1100)
- ✅ Rozłączanie
- ✅ Walidacja danych wejściowych
- ✅ Obsługa błędów połączenia

### Planowane funkcje (przyszłe wersje)
- 📋 Pobieranie listy instrumentów (Request 5108)
- 📊 Wyświetlanie szczegółów instrumentu (Request 1000-1003)
- 🔄 Subskrypcja real-time (Request 1001)
- 💼 Składanie zleceń (połączenie z SLE)
- 📈 Wykresy cenowe
- 💾 Eksport danych do CSV/Excel

---

## Skróty klawiszowe

Obecnie aplikacja nie posiada zdefiniowanych skrótów klawiszowych. Nawigacja odbywa się za pomocą myszy i klawisza Tab.

---

**Autor**: FIS API Client Team  
**Wersja dokumentu**: 1.0  
**Data ostatniej aktualizacji**: 2025
