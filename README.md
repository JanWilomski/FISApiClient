# FIS API Client - Aplikacja WPF do połączenia z MDS/SLC

## Opis projektu

Aplikacja WPF w architekturze MVVM do łączenia się z serwerem Market Data (MDS/SLC) na giełdzie WSE (Warsaw Stock Exchange). Umożliwia pobieranie danych o instrumentach finansowych i ich szczegółów poprzez API FIS.

## Architektura

Projekt wykorzystuje wzorzec **MVVM (Model-View-ViewModel)**:

### 1. **Models** (Modele danych i logika biznesowa)
- `Instrument.cs` - model instrumentu finansowego
- `InstrumentDetails.cs` - szczegółowe dane instrumentu
- `MdsConnectionService.cs` - serwis połączenia z MDS/SLC

### 2. **ViewModels** (Logika prezentacji)
- `ConnectionViewModel.cs` - zarządzanie logiką połączenia

### 3. **Views** (Widoki UI)
- `MainWindow.xaml` - główne okno aplikacji z formularzem połączenia

### 4. **Helpers** (Klasy pomocnicze)
- `ViewModelBase.cs` - bazowa klasa ViewModel z INotifyPropertyChanged
- `RelayCommand.cs` - implementacja ICommand dla MVVM

## Szczegóły implementacji protokołu FIS API

### Struktura wiadomości GL API

Zgodnie z dokumentacją API SLC V5, każda wiadomość ma następującą strukturę:

```
[LG(2)][HEADER(32)][DATA(variable)][FOOTER(3)]
```

#### LG (Length) - 2 bajty
- `LG[0] = długość % 256`
- `LG[1] = długość / 256`
- Całkowita długość = `LG[0] + 256 * LG[1]`

#### HEADER - 32 bajty
```
[STX(1)][API_VER(1)][REQ_SIZE(5)][CALLED_ID(5)][FILLER(5)][CALLING_ID(5)][FILLER(2)][REQ_NUM(5)][FILLER(3)]
```
- **STX**: 0x02 (Start of Text)
- **API_VER**: '0' dla SLC V5
- **REQ_SIZE**: rozmiar żądania (ASCII, 5 znaków)
- **CALLED_ID**: wywoływany identyfikator logiczny (subnode)
- **CALLING_ID**: wywołujący identyfikator logiczny (zazwyczaj "00000")
- **REQ_NUM**: numer żądania (ASCII, 5 znaków, np. "01100" dla logowania)

#### FOOTER - 3 bajty
```
[FILLER(2)][ETX(1)]
```
- **ETX**: 0x03 (End of Text)

### Format GL (Variable Length Encoding)

Pola danych używają specjalnego kodowania GL:
- Pierwszy bajt = długość + 32
- Następne bajty = wartość w ASCII

**Przykład**: Dla wartości "FTE" (3 znaki):
- Pierwszy bajt = 3 + 32 = 35 = '#'
- Wynik: "#FTE"

### Proces połączenia (Request 1100)

1. **Wysłanie Client ID** (16 bajtów ASCII):
   ```
   "FISAPICLIENT    "
   ```

2. **Żądanie logowania (Request 1100)**:
   - Pozycja 0: User Number (3 bajty, padLeft '0')
   - Pozycja 1: Password (16 bajtów, padRight ' ')
   - Pozycja 2: Filler (7 bajtów spacji)
   - Pozycja 10-11: Pary klucz-wartość w formacie GL:
     - "15" → "V5" (wersja serwera)
     - "26" → user (username dla połączenia)

3. **Odpowiedź**:
   - Request Number "01100" = sukces
   - Request Number "01102" = błąd (pole "Reason" zawiera kod błędu)

### Kody błędów połączenia

- 0: Nieprawidłowe hasło
- 1: Brak miejsca w bazie połączeń
- 2: Nieprawidłowy format żądania
- 3: Zabroniony numer użytkownika
- 4: Nieznany użytkownik
- 52: Złe hasło
- 59: Już połączony

### Request 5108 - Dictionary (Słownik instrumentów)

Służy do pobierania listy instrumentów dla danego GLID (Exchange-Market identifier).

**Format GLID**:
```
[EEEE][SS][MMM][SSS]
EEEE: Exchange (4 cyfry)
SS:   Source (2 cyfry)
MMM:  Market (3 cyfry)
SSS:  Sub-market (3 cyfry)
```

**Przykład dla WSE**:
- Exchange 40: `004000001000` - GPW Główny Rynek
- Exchange 330: `033000001000` - NewConnect
- Exchange 331: `033100001000` - Catalyst
- Exchange 332: `033200001000` - Structured Products

### Request 1000-1003 - Stock Watch

Pobiera dane o konkretnym instrumencie:

- **1000**: Snapshot (jednorazowy)
- **1001**: Refreshed + Real-time (subskrypcja)
- **1002**: Stop refresh (zatrzymanie subskrypcji)
- **1003**: Real-time update (aktualizacja)

**Pola danych (Stock Watch)**:
- 0: Bid quantity (ilość ofert kupna)
- 1: Bid price (cena kupna)
- 2: Ask price (cena sprzedaży)
- 3: Ask quantity (ilość ofert sprzedaży)
- 4: Last traded price (ostatnia cena)
- 5: Last traded quantity (ostatnia ilość)
- 6: Last trade time (czas ostatniej transakcji)
- 8: Percentage variation (zmiana procentowa)
- 9: Total quantity exchanged (łączny wolumen)
- 10: Opening price (cena otwarcia)
- 11: High (najwyższa cena)
- 12: Low (najniższa cena)
- 13: Suspension indicator (wskaźnik zawieszenia)
- 14: Variation sign (znak zmiany)
- 16: Closing price (cena zamknięcia)
- 88: ISIN
- 140: Trading phase (faza handlu)

## Domyślne parametry połączenia

Aplikacja zawiera następujące domyślne wartości:

- **IP Address**: 192.168.45.25
- **Port**: 25503
- **User**: 103
- **Password**: glglgl
- **Node**: 5500
- **Subnode**: 4500
- **API Version**: V5 (SLC V5)

## Wymagania

- .NET 8.0 (Windows)
- WPF (Windows Presentation Foundation)

## Budowanie projektu

```bash
cd /home/claude/FISApiClient
dotnet build
```

## Uruchomienie

```bash
dotnet run
```

## Funkcjonalność

### Obecna funkcjonalność (v1.0)

1. **Łączenie z serwerem MDS/SLC**
   - Formularz z parametrami połączenia
   - Walidacja danych wejściowych
   - Wysyłanie Client ID i żądania logowania (Request 1100)
   - Weryfikacja odpowiedzi serwera
   - Wskaźnik statusu połączenia (wizualny + tekstowy)

2. **Obsługa błędów**
   - Wyświetlanie komunikatów błędów
   - Timeout połączenia
   - Nieprawidłowe dane logowania

3. **Asynchroniczna komunikacja**
   - Async/await dla operacji I/O
   - Nasłuchiwanie wiadomości w tle
   - Non-blocking UI

### Planowana funkcjonalność (przyszłe wersje)

1. **Pobieranie listy instrumentów** (Request 5108)
2. **Wyświetlanie szczegółów instrumentu** (Request 1000-1003)
3. **Subskrypcja real-time** (Request 1001)
4. **Połączenie z SLE** (składanie zleceń)

## Struktura kodu

```
FISApiClient/
├── Models/
│   ├── Instrument.cs              # Model instrumentu
│   ├── InstrumentDetails.cs       # Szczegóły instrumentu
│   └── MdsConnectionService.cs    # Serwis połączenia MDS
├── ViewModels/
│   └── ConnectionViewModel.cs     # ViewModel połączenia
├── Views/
│   ├── MainWindow.xaml           # Główne okno
│   └── MainWindow.xaml.cs        # Code-behind
├── Helpers/
│   ├── ViewModelBase.cs          # Bazowa klasa ViewModel
│   └── RelayCommand.cs           # Implementacja ICommand
├── App.xaml                       # Konfiguracja aplikacji
├── App.xaml.cs                    # App code-behind
└── FISApiClient.csproj           # Plik projektu
```

## Wzorzec MVVM w projekcie

### Separacja odpowiedzialności

1. **Model** - dane i logika biznesowa
   - `MdsConnectionService` zarządza połączeniem TCP/IP
   - Enkapsuluje protokół komunikacji FIS API
   - Dekoduje i enkoduje wiadomości GL

2. **ViewModel** - logika prezentacji
   - `ConnectionViewModel` zarządza stanem UI
   - Implementuje komendy (Connect, Disconnect)
   - Waliduje dane wejściowe
   - Komunikuje się z Model poprzez `MdsConnectionService`

3. **View** - interfejs użytkownika
   - `MainWindow.xaml` definiuje wygląd
   - Data binding do ViewModelu
   - Brak logiki biznesowej w code-behind

### Data Binding

Aplikacja wykorzystuje dwukierunkowe bindowanie danych:
```xml
<TextBox Text="{Binding IpAddress, UpdateSourceTrigger=PropertyChanged}"/>
```

### Commands

Akcje użytkownika obsługiwane przez ICommand:
```xml
<Button Command="{Binding ConnectCommand}"/>
```

## Testowanie połączenia

### Sprawdzanie logów

Aplikacja używa `Debug.WriteLine()` do logowania. W Visual Studio:
1. Uruchom w trybie Debug
2. Otwórz Output Window (View → Output)
3. Wybierz "Debug" z dropdown

### Problemy z połączeniem

Jeśli połączenie nie działa:
1. Sprawdź dostępność serwera: `ping 192.168.45.25`
2. Sprawdź czy port jest otwarty: `telnet 192.168.45.25 25503`
3. Zweryfikuj dane logowania (user/password)
4. Sprawdź logi w Output Window

## Uwagi techniczne

### PasswordBox w MVVM

Ze względu na bezpieczeństwo, `PasswordBox.Password` nie jest DependencyProperty i nie obsługuje bezpośredniego bindowania. Rozwiązanie:
- Event handler `PasswordBox_PasswordChanged` w code-behind
- Przekazywanie wartości do ViewModelu przez event

### Async w WPF

- Operacje sieciowe wykonywane asynchronicznie (async/await)
- UI pozostaje responsywny podczas połączenia
- Background task (`Task.Run`) dla nasłuchiwania wiadomości

### Thread Safety

- `MdsConnectionService` używa `NetworkStream` który nie jest thread-safe
- Nasłuchiwanie w osobnym wątku (`Task.Run(ListenForMessages)`)
- Events wywołują kod na UI thread dzięki WPF Dispatcher

## Autor

Cross FIS API Client v1.2

## Licencja

Użycie wewnętrzne - FIS API Protocol
