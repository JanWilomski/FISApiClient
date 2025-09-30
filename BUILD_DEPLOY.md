# Instrukcje budowania i wdrażania - FIS API Client

## Spis treści
1. [Wymagania deweloperskie](#wymagania-deweloperskie)
2. [Konfiguracja środowiska](#konfiguracja-środowiska)
3. [Budowanie projektu](#budowanie-projektu)
4. [Testowanie](#testowanie)
5. [Pakowanie i dystrybucja](#pakowanie-i-dystrybucja)
6. [Wdrażanie](#wdrażanie)

---

## Wymagania deweloperskie

### Oprogramowanie

- **Visual Studio 2022** (Community, Professional lub Enterprise)
  - Workload: ".NET Desktop Development"
  - Workload: "WPF (Windows Presentation Foundation)"
- **.NET 8.0 SDK**
- **Git** (opcjonalnie, do kontroli wersji)

### Alternatywnie (bez Visual Studio)

- **.NET 8.0 SDK**
- **Visual Studio Code** z rozszerzeniem C#
- **MSBuild** (zainstalowany wraz z .NET SDK)

---

## Konfiguracja środowiska

### Instalacja Visual Studio 2022

1. Pobierz Visual Studio 2022:
   - https://visualstudio.microsoft.com/downloads/

2. Podczas instalacji wybierz workloads:
   - ✅ ".NET desktop development"
   - ✅ "Desktop development with C++"

3. Zweryfikuj instalację:
   ```cmd
   dotnet --version
   ```
   Powinno wyświetlić: `8.0.xxx`

### Instalacja .NET 8.0 SDK (bez Visual Studio)

1. Pobierz .NET 8.0 SDK:
   - https://dotnet.microsoft.com/download/dotnet/8.0

2. Zainstaluj SDK (wybierz wersję dla Windows x64)

3. Zweryfikuj instalację:
   ```cmd
   dotnet --version
   dotnet --list-sdks
   ```

---

## Budowanie projektu

### Metoda 1: Visual Studio 2022

#### Otwarcie projektu

1. Uruchom Visual Studio 2022
2. Wybierz "Open a project or solution"
3. Wskaż plik `FISApiClient.sln`

#### Przywracanie pakietów

Pakiety NuGet są automatycznie przywracane przy pierwszym otwarciu projektu.

Ręczne przywracanie:
```
Tools → NuGet Package Manager → Manage NuGet Packages for Solution
```
Kliknij "Restore" jeśli jest dostępne.

#### Budowanie

**Debug:**
- Naciśnij `Ctrl+Shift+B`
- Lub: `Build → Build Solution`

**Release:**
1. `Build → Configuration Manager`
2. Zmień "Active solution configuration" na "Release"
3. Naciśnij `Ctrl+Shift+B`

**Wynik:**
- Debug: `bin\Debug\net8.0-windows\`
- Release: `bin\Release\net8.0-windows\`

### Metoda 2: Wiersz poleceń (dotnet CLI)

#### Przejdź do katalogu projektu

```cmd
cd C:\ścieżka\do\FISApiClient
```

#### Przywróć pakiety

```cmd
dotnet restore
```

#### Budowanie Debug

```cmd
dotnet build
```

#### Budowanie Release

```cmd
dotnet build --configuration Release
```

#### Budowanie z pełnym wyjściem

```cmd
dotnet build --configuration Release --verbosity detailed
```

### Metoda 3: MSBuild bezpośrednio

```cmd
msbuild FISApiClient.sln /p:Configuration=Release /p:Platform="Any CPU"
```

---

## Testowanie

### Uruchomienie z Visual Studio

**Debug:**
- Naciśnij `F5` (uruchomienie z debuggerem)
- Lub `Ctrl+F5` (uruchomienie bez debuggera)

**Logi debugowania:**
1. Uruchom w trybie Debug (F5)
2. Otwórz Output Window: `View → Output`
3. Z dropdown wybierz "Debug"
4. Wszystkie `Debug.WriteLine()` będą widoczne

### Uruchomienie z wiersza poleceń

```cmd
cd bin\Release\net8.0-windows
FISApiClient.exe
```

### Testy manualne

#### Test 1: Połączenie poprawne

1. Uruchom aplikację
2. Sprawdź domyślne wartości:
   - IP: 192.168.45.25
   - Port: 25503
   - User: 103
   - Password: glglgl
   - Node: 5500
   - Subnode: 4500
3. Kliknij "Połącz"
4. **Oczekiwany rezultat**: 
   - Zielony wskaźnik "Połączono"
   - MessageBox: "Połączenie z serwerem MDS/SLC zostało nawiązane pomyślnie!"

#### Test 2: Błędny port

1. Zmień port na: 99999
2. Kliknij "Połącz"
3. **Oczekiwany rezultat**: 
   - Komunikat walidacji: "Port musi być liczbą z zakresu 1-65535"

#### Test 3: Błędne hasło

1. Ustaw hasło: "wrongpassword"
2. Kliknij "Połącz"
3. **Oczekiwany rezultat**: 
   - Czerwony wskaźnik "Rozłączono"
   - MessageBox z błędem połączenia

#### Test 4: Rozłączenie

1. Połącz się z serwerem (Test 1)
2. Kliknij "Rozłącz"
3. **Oczekiwany rezultat**:
   - Czerwony wskaźnik "Rozłączono"
   - MessageBox: "Rozłączono z serwerem MDS/SLC"
   - Wszystkie pola formularza są edytowalne

---

## Pakowanie i dystrybucja

### Publikacja Self-Contained (z runtime)

Aplikacja będzie działać na komputerach bez zainstalowanego .NET:

```cmd
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Parametry:**
- `-c Release`: Konfiguracja Release
- `-r win-x64`: Target Windows x64
- `--self-contained true`: Dołącz .NET runtime
- `-p:PublishSingleFile=true`: Pojedynczy plik exe

**Wynik:**
```
bin\Release\net8.0-windows\win-x64\publish\
└── FISApiClient.exe (większy rozmiar, ~60-80 MB)
```

### Publikacja Framework-Dependent (bez runtime)

Wymaga zainstalowanego .NET 8.0 Runtime na docelowym komputerze:

```cmd
dotnet publish -c Release -r win-x64 --self-contained false
```

**Wynik:**
```
bin\Release\net8.0-windows\win-x64\publish\
├── FISApiClient.exe
├── FISApiClient.dll
└── (inne zależności)
```

### Tworzenie paczki instalacyjnej

#### Metoda 1: ZIP Archive

```cmd
cd bin\Release\net8.0-windows\win-x64\publish
powershell Compress-Archive -Path * -DestinationPath FISApiClient-v1.0.zip
```

#### Metoda 2: Inno Setup (zalecane dla profesjonalnej dystrybucji)

1. Pobierz Inno Setup: https://jrsoftware.org/isdl.php

2. Utwórz skrypt `setup.iss`:

```iss
[Setup]
AppName=FIS API Client
AppVersion=1.0
DefaultDirName={pf}\FISApiClient
DefaultGroupName=FIS API Client
OutputBaseFilename=FISApiClient-Setup-v1.0
Compression=lzma2
SolidCompression=yes

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\FIS API Client"; Filename: "{app}\FISApiClient.exe"
Name: "{commondesktop}\FIS API Client"; Filename: "{app}\FISApiClient.exe"

[Run]
Filename: "{app}\FISApiClient.exe"; Description: "Launch FIS API Client"; Flags: postinstall nowait skipifsilent
```

3. Kompiluj:
```cmd
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

### Tworzenie ClickOnce Deployment

W Visual Studio:

1. Kliknij prawym na projekt → Publish
2. Wybierz target (Folder, Azure, itp.)
3. Skonfiguruj opcje publikacji
4. Kliknij "Publish"

---

## Wdrażanie

### Wdrażanie lokalne (pojedynczy komputer)

1. Skopiuj katalog publish na docelowy komputer
2. Uruchom `FISApiClient.exe`

### Wdrażanie sieciowe (shared folder)

1. Opublikuj aplikację (framework-dependent)
2. Skopiuj do folderu sieciowego:
   ```cmd
   xcopy /E /I bin\Release\net8.0-windows\win-x64\publish \\server\share\FISApiClient
   ```
3. Utwórz skrót na komputerach użytkowników:
   - Target: `\\server\share\FISApiClient\FISApiClient.exe`

### Wymagania na komputerze docelowym

#### Self-Contained (z runtime):
- ✅ Windows 10 (1607+) lub Windows 11
- ✅ Dostęp sieciowy do serwera MDS

#### Framework-Dependent (bez runtime):
- ✅ Windows 10 (1607+) lub Windows 11
- ✅ .NET 8.0 Runtime (Desktop)
- ✅ Dostęp sieciowy do serwera MDS

### Instalacja .NET Runtime na komputerze końcowym

Jeśli używasz framework-dependent:

1. Pobierz .NET 8.0 Desktop Runtime:
   - https://dotnet.microsoft.com/download/dotnet/8.0

2. Wybierz "Desktop Runtime" (nie SDK)

3. Zainstaluj (.NET Desktop Runtime 8.0.x - Windows x64)

4. Zweryfikuj:
   ```cmd
   dotnet --list-runtimes
   ```
   Powinna być widoczna linia:
   ```
   Microsoft.WindowsDesktop.App 8.0.x [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]
   ```

---

## Zarządzanie wersjami

### Ustawienie wersji aplikacji

Edytuj `FISApiClient.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <Copyright>Copyright © 2025</Copyright>
  <Company>Your Company</Company>
  <Product>FIS API Client</Product>
</PropertyGroup>
```

### Semantic Versioning

Stosuj standard: `MAJOR.MINOR.PATCH`

- **MAJOR**: Zmiany niekompatybilne wstecz
- **MINOR**: Nowe funkcjonalności (kompatybilne)
- **PATCH**: Poprawki błędów

Przykład:
- v1.0.0 - Pierwsza wersja
- v1.1.0 - Dodanie listy instrumentów
- v1.1.1 - Poprawka błędu połączenia
- v2.0.0 - Nowa architektura (breaking change)

---

## Optymalizacja wydajności

### Konfiguracja Release

Upewnij się, że w Release build:

```xml
<PropertyGroup Condition="'$(Configuration)'=='Release'">
  <Optimize>true</Optimize>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

### Trimming (zmniejszenie rozmiaru)

Dla self-contained, dodaj do .csproj:

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>link</TrimMode>
</PropertyGroup>
```

**Uwaga**: Testuj dokładnie po włączeniu trimming - może usunąć potrzebny kod.

---

## Troubleshooting budowania

### Problem: "SDK not found"

**Rozwiązanie:**
```cmd
dotnet --list-sdks
```
Jeśli brak SDK 8.0.x - zainstaluj ponownie .NET 8.0 SDK.

### Problem: "The type or namespace name 'Windows' does not exist"

**Rozwiązanie:**
Sprawdź `TargetFramework` w .csproj:
```xml
<TargetFramework>net8.0-windows</TargetFramework>
```
Musi zawierać `-windows` dla WPF.

### Problem: XAML build errors

**Rozwiązanie:**
1. Clean solution: `Build → Clean Solution`
2. Restart Visual Studio
3. Rebuild: `Build → Rebuild Solution`

### Problem: "Could not load file or assembly"

**Rozwiązanie:**
```cmd
dotnet clean
dotnet restore
dotnet build
```

---

## Continuous Integration (CI/CD)

### GitHub Actions

Utwórz `.github/workflows/build.yml`:

```yaml
name: Build FIS API Client

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Publish
      run: dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
    
    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: FISApiClient
        path: bin/Release/net8.0-windows/win-x64/publish/
```

### Azure DevOps

Utwórz `azure-pipelines.yml`:

```yaml
trigger:
- main

pool:
  vmImage: 'windows-latest'

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

steps:
- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '$(solution)'

- task: VSBuild@1
  inputs:
    solution: '$(solution)'
    platform: '$(buildPlatform)'
    configuration: '$(buildConfiguration)'

- task: PublishBuildArtifacts@1
  inputs:
    PathtoPublish: 'bin/Release'
    ArtifactName: 'FISApiClient'
```

---

## Checklist przed wdrożeniem

- [ ] Wszystkie testy manualne przeszły pomyślnie
- [ ] Zweryfikowano wersję w AssemblyInfo/csproj
- [ ] Build w konfiguracji Release (nie Debug)
- [ ] Sprawdzono rozmiar pliku wykonywalnego
- [ ] Przygotowano dokumentację użytkownika
- [ ] Przygotowano Release Notes
- [ ] Utworzono paczkę instalacyjną (ZIP lub Installer)
- [ ] Przetestowano na czystym środowisku (bez Visual Studio)
- [ ] Zweryfikowano wymagania systemowe
- [ ] Przygotowano plan rollback w razie problemów

---

**Autor**: FIS API Client Team  
**Wersja dokumentu**: 1.0  
**Data ostatniej aktualizacji**: 2025
