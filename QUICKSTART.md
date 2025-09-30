# 🚀 Szybki Start - FIS API Client

## Dla użytkowników końcowych

### ⚡ 5 kroków do listy instrumentów

#### 1️⃣ Zainstaluj .NET 8.0 Runtime
```
https://dotnet.microsoft.com/download/dotnet/8.0
Pobierz: "Desktop Runtime" dla Windows x64
```

#### 2️⃣ Rozpakuj i uruchom
- Rozpakuj archiwum `FISApiClient.zip`
- Uruchom `FISApiClient.exe`

#### 3️⃣ Połącz się z serwerem
- Domyślne dane są już wypełnione
- Kliknij przycisk **"Połącz"**
- Poczekaj na zielony wskaźnik ✅

#### 4️⃣ Otwórz listę instrumentów
- Kliknij przycisk **"📋 Lista instrumentów"**
- Otworzy się nowe okno

#### 5️⃣ Pobierz instrumenty
- Kliknij **"🔄 Pobierz instrumenty"**
- Poczekaj 2-5 sekund
- Gotowe! Zobacz ~500+ instrumentów! 🎉

---

## Dla deweloperów

### ⚡ 3 kroki do budowania

#### 1️⃣ Otwórz w Visual Studio
```cmd
FISApiClient.sln
```

#### 2️⃣ Zbuduj projekt
```
Ctrl+Shift+B
```

#### 3️⃣ Uruchom
```
F5
```

---

## 📋 Domyślne parametry

| Parametr | Wartość |
|----------|---------|
| IP | 192.168.45.25 |
| Port | 25503 |
| User | 103 |
| Password | glglgl |
| Node | 5500 |
| Subnode | 4500 |

---

## ✅ Co możesz zrobić

### 🔌 Połączenie
```
● Połączono (zielony wskaźnik)
"Pomyślnie połączono z 192.168.45.25:25503"
```

### 📊 Lista instrumentów
```
Łącznie: 500+
Rynki: GPW (40), NewConnect (330), Catalyst (331), Structured Products (332)
```

### 🔍 Wyszukiwanie
```
Szukaj: "KGHM" → Znajdzie KGHM Polska Miedź
Szukaj: "PKO" → Znajdzie PKO Bank Polski
```

### 📥 Eksport
```
Eksportuj CSV → Zapisz wszystkie instrumenty do pliku Excel
```

---

## ❌ Problemy?

### "Nie można połączyć z serwerem"
```cmd
ping 192.168.45.25
```
Jeśli brak odpowiedzi → Serwer jest niedostępny

### "Nieprawidłowe dane logowania"
Sprawdź user/password z administratorem

### "Aplikacja nie uruchamia się"
Sprawdź czy zainstalowano .NET 8.0 Desktop Runtime

### "Przycisk Lista instrumentów nieaktywny"
Najpierw połącz się z serwerem (przycisk "Połącz")

---

## 📚 Pełna dokumentacja

- **Użytkownicy**: Czytaj [USER_GUIDE.md](USER_GUIDE.md)
- **Deweloperzy**: Czytaj [README.md](README.md)
- **Nowa funkcjonalność**: Czytaj [RELEASE_v1.1.md](RELEASE_v1.1.md)
- **Architektura**: Czytaj [ARCHITECTURE.md](ARCHITECTURE.md)
- **Budowanie**: Czytaj [BUILD_DEPLOY.md](BUILD_DEPLOY.md)

---

## 🎯 Co nowego w v1.1?

Po pomyślnym połączeniu:
1. ✅ **Lista instrumentów** - Pobieraj z GPW, NewConnect, Catalyst
2. ✅ **Wyszukiwanie** - Szukaj po symbolu, nazwie, ISIN
3. ✅ **Eksport CSV** - Zapisz dane do Excel
4. 🔜 **v1.2**: Real-time aktualizacje
5. 🔜 **v2.0**: Składanie zleceń (SLE)

---

**Status projektu**: ✅ v1.1 Produkcyjny  
**Ostatnia aktualizacja**: 2025-09-30

🎉 **Miłego korzystania z FIS API Client!**
