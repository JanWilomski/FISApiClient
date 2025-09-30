# 🚀 Szybki Start - FIS API Client

## Dla użytkowników końcowych

### ⚡ 3 kroki do uruchomienia

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
- Gotowe! ✅

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

## ✅ Sukces połączenia

Gdy zobaczysz:
```
● Połączono (zielony wskaźnik)
"Pomyślnie połączono z 192.168.45.25:25503"
```

Gratulacje! Aplikacja działa poprawnie! 🎉

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

---

## 📚 Pełna dokumentacja

- **Użytkownicy**: Czytaj [USER_GUIDE.md](USER_GUIDE.md)
- **Deweloperzy**: Czytaj [README.md](README.md)
- **Architektura**: Czytaj [ARCHITECTURE.md](ARCHITECTURE.md)
- **Budowanie**: Czytaj [BUILD_DEPLOY.md](BUILD_DEPLOY.md)

---

## 🎯 Co dalej?

Po pomyślnym połączeniu:
1. **v1.1**: Dodamy listę instrumentów
2. **v1.2**: Real-time aktualizacje
3. **v2.0**: Składanie zleceń (SLE)

---

**Status projektu**: ✅ Produkcyjny (v1.0)  
**Ostatnia aktualizacja**: 2025

🎉 **Miłego korzystania z FIS API Client!**
