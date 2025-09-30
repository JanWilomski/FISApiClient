# Architektura FIS API Client

## Diagram MVVM

```
┌─────────────────────────────────────────────────────────────────┐
│                           VIEW LAYER                             │
│  ┌────────────────────────────────────────────────────────┐     │
│  │             MainWindow.xaml                             │     │
│  │  ┌──────────────────────────────────────────────┐      │     │
│  │  │  Formularz połączenia:                        │      │     │
│  │  │  - TextBox (IP, Port, User, Node, Subnode)   │      │     │
│  │  │  - PasswordBox (Password)                     │      │     │
│  │  │  - Button (Połącz, Rozłącz)                  │      │     │
│  │  │  - StatusBar                                  │      │     │
│  │  └──────────────────────────────────────────────┘      │     │
│  └────────────────────────────────────────────────────────┘     │
│                            ▲                                     │
│                            │ Data Binding                        │
│                            │ Commands                            │
└────────────────────────────┼─────────────────────────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────────┐
│                       VIEWMODEL LAYER                            │
│  ┌────────────────────────▼──────────────────────────┐          │
│  │         ConnectionViewModel                        │          │
│  │  ┌──────────────────────────────────────────┐     │          │
│  │  │  Properties:                              │     │          │
│  │  │  - IpAddress, Port, User, Password        │     │          │
│  │  │  - Node, Subnode                          │     │          │
│  │  │  - IsConnected, StatusMessage             │     │          │
│  │  │                                            │     │          │
│  │  │  Commands:                                 │     │          │
│  │  │  - ConnectCommand                          │     │          │
│  │  │  - DisconnectCommand                       │     │          │
│  │  │                                            │     │          │
│  │  │  Methods:                                  │     │          │
│  │  │  - ConnectAsync()                          │     │          │
│  │  │  - Disconnect()                            │     │          │
│  │  │  - ValidateInputs()                        │     │          │
│  │  └──────────────────────────────────────────┘     │          │
│  └───────────────────────┬────────────────────────────┘          │
│                          │                                       │
│                          │ uses                                  │
└──────────────────────────┼───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                        MODEL LAYER                               │
│  ┌─────────────────────────────────────────────────┐            │
│  │      MdsConnectionService                        │            │
│  │  ┌────────────────────────────────────────┐     │            │
│  │  │  Properties:                            │     │            │
│  │  │  - IsConnected                          │     │            │
│  │  │                                          │     │            │
│  │  │  Events:                                 │     │            │
│  │  │  - InstrumentsReceived                   │     │            │
│  │  │  - InstrumentDetailsReceived             │     │            │
│  │  │                                          │     │            │
│  │  │  Public Methods:                         │     │            │
│  │  │  - ConnectAndLoginAsync()                │     │            │
│  │  │  - Disconnect()                          │     │            │
│  │  │  - RequestAllInstrumentsAsync()          │     │            │
│  │  │  - RequestInstrumentDetails()            │     │            │
│  │  │                                          │     │            │
│  │  │  Private Methods:                        │     │            │
│  │  │  - ListenForMessages()                   │     │            │
│  │  │  - ProcessIncomingMessage()              │     │            │
│  │  │  - ProcessDictionaryResponse()           │     │            │
│  │  │  - ProcessInstrumentDetailsResponse()    │     │            │
│  │  │  - BuildLoginRequest()                   │     │            │
│  │  │  - BuildDictionaryRequest()              │     │            │
│  │  │  - BuildStockWatchRequest()              │     │            │
│  │  │  - BuildMessage()                        │     │            │
│  │  │  - EncodeField()                         │     │            │
│  │  │  - DecodeField()                         │     │            │
│  │  └────────────────────────────────────────┘     │            │
│  └─────────────────────────────────────────────────┘            │
│                          │                                       │
│                          │ TCP/IP Communication                  │
└──────────────────────────┼───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                    FIS MDS/SLC SERVER                            │
│  ┌─────────────────────────────────────────────────┐            │
│  │  IP: 192.168.45.25                               │            │
│  │  Port: 25503                                     │            │
│  │  Protocol: FIS API v5 (GL Format)                │            │
│  │                                                   │            │
│  │  Endpoints:                                       │            │
│  │  - Request 1100: Login                            │            │
│  │  - Request 1102: Logout                           │            │
│  │  - Request 5108: Dictionary (Instruments)         │            │
│  │  - Request 1000-1003: Stock Watch (Details)       │            │
│  └─────────────────────────────────────────────────┘            │
└──────────────────────────────────────────────────────────────────┘
```

## Model-View-ViewModel Relationships

```
┌────────────┐         ┌──────────────┐         ┌─────────────┐
│            │         │              │         │             │
│    View    │◄───────►│  ViewModel   │◄───────►│    Model    │
│            │         │              │         │             │
└────────────┘         └──────────────┘         └─────────────┘
     │                       │                         │
     │                       │                         │
     ▼                       ▼                         ▼
 UI Elements          Properties              Business Logic
 Data Binding         Commands                Network Communication
 User Input           Validation              Data Processing
 Visual State         UI Logic                Protocol Implementation
```

## Helper Classes

```
┌──────────────────────────────────────┐
│         ViewModelBase                │
│  ┌────────────────────────────┐      │
│  │  INotifyPropertyChanged    │      │
│  │  - OnPropertyChanged()     │      │
│  │  - SetProperty()           │      │
│  └────────────────────────────┘      │
└──────────────────────────────────────┘
              ▲
              │ inherits
              │
┌──────────────────────────────────────┐
│     ConnectionViewModel              │
└──────────────────────────────────────┘


┌──────────────────────────────────────┐
│         RelayCommand                 │
│  ┌────────────────────────────┐      │
│  │  ICommand                  │      │
│  │  - Execute()               │      │
│  │  - CanExecute()            │      │
│  └────────────────────────────┘      │
└──────────────────────────────────────┘
              ▲
              │ used by
              │
┌──────────────────────────────────────┐
│     ConnectionViewModel              │
│  - ConnectCommand                    │
│  - DisconnectCommand                 │
└──────────────────────────────────────┘
```

## Data Flow - Connection Process

```
User clicks "Połącz"
        │
        ▼
┌───────────────────────────────────────┐
│  MainWindow.xaml                      │
│  Button triggers ConnectCommand       │
└───────────┬───────────────────────────┘
            │
            ▼
┌───────────────────────────────────────┐
│  ConnectionViewModel                  │
│  1. ValidateInputs()                  │
│  2. ConnectAsync()                    │
└───────────┬───────────────────────────┘
            │
            ▼
┌───────────────────────────────────────┐
│  MdsConnectionService                 │
│  1. TCP Connect                       │
│  2. Send Client ID                    │
│  3. BuildLoginRequest()               │
│  4. Send Request 1100                 │
│  5. VerifyLoginResponse()             │
│  6. Start ListenForMessages()         │
└───────────┬───────────────────────────┘
            │
            ▼
┌───────────────────────────────────────┐
│  FIS MDS/SLC Server                   │
│  Process login request                │
│  Send response (1100 or 1102)         │
└───────────┬───────────────────────────┘
            │
            ▼
┌───────────────────────────────────────┐
│  MdsConnectionService                 │
│  Parse response                       │
│  Return success/failure               │
└───────────┬───────────────────────────┘
            │
            ▼
┌───────────────────────────────────────┐
│  ConnectionViewModel                  │
│  Update IsConnected property          │
│  Update StatusMessage                 │
│  Show MessageBox                      │
└───────────┬───────────────────────────┘
            │
            ▼
┌───────────────────────────────────────┐
│  MainWindow.xaml                      │
│  UI updates via data binding          │
│  - Connection status indicator        │
│  - Enable/Disable buttons             │
│  - Status bar message                 │
└───────────────────────────────────────┘
```

## Message Format (FIS API Protocol)

```
Message Structure:
┌──────────────────────────────────────────────────────────┐
│  [LG]  [HEADER]  [DATA]  [FOOTER]                        │
│  (2)    (32)     (var)    (3)                            │
└──────────────────────────────────────────────────────────┘

LG (Length):
┌──────────┬──────────┐
│  LG[0]   │  LG[1]   │
│ (% 256)  │ (/ 256)  │
└──────────┴──────────┘

HEADER (32 bytes):
┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐
│ STX │ VER │SIZE │CALL │FILL │CLLG │FILL │ REQ │FILL │
│ (1) │ (1) │ (5) │ (5) │ (5) │ (5) │ (2) │ (5) │ (3) │
└─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┘
  0x02  '0'  ASCII ASCII  ' '  ASCII  ' '  ASCII  ' '

DATA (Variable):
┌──────────────────────────────────────┐
│  GL encoded fields                    │
│  First byte = length + 32             │
│  Rest = ASCII value                   │
└──────────────────────────────────────┘

FOOTER (3 bytes):
┌──────────┬──────────┐
│  FILLER  │   ETX    │
│   (2)    │   (1)    │
└──────────┴──────────┘
    ' '      0x03
```

## Threading Model

```
┌─────────────────────────────────────────────────────────┐
│                    UI THREAD                             │
│  ┌───────────────────────────────────────────┐          │
│  │  MainWindow                                │          │
│  │  ConnectionViewModel                       │          │
│  │  - User interactions                       │          │
│  │  - Data binding updates                    │          │
│  └───────────────────────────────────────────┘          │
└────────────────────────┬────────────────────────────────┘
                         │
                         │ async/await
                         │
┌────────────────────────▼────────────────────────────────┐
│               ASYNC OPERATIONS                           │
│  ┌───────────────────────────────────────────┐          │
│  │  ConnectAndLoginAsync()                    │          │
│  │  - TCP connection                          │          │
│  │  - Login request/response                  │          │
│  └───────────────────────────────────────────┘          │
└────────────────────────┬────────────────────────────────┘
                         │
                         │ Task.Run()
                         │
┌────────────────────────▼────────────────────────────────┐
│              BACKGROUND THREAD                           │
│  ┌───────────────────────────────────────────┐          │
│  │  ListenForMessages()                       │          │
│  │  - Continuous message reading              │          │
│  │  - Message parsing                         │          │
│  │  - Event invocation                        │          │
│  └───────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────┘
```

## Future Architecture Extensions

```
┌─────────────────────────────────────────────────────────┐
│                    FUTURE MODULES                        │
│                                                          │
│  ┌─────────────────────────────────────────────┐       │
│  │  InstrumentListViewModel                     │       │
│  │  - Display instruments from Dictionary       │       │
│  │  - Filter and search                         │       │
│  └─────────────────────────────────────────────┘       │
│                                                          │
│  ┌─────────────────────────────────────────────┐       │
│  │  InstrumentDetailsViewModel                  │       │
│  │  - Display real-time quotes                  │       │
│  │  - Subscribe to updates                      │       │
│  └─────────────────────────────────────────────┘       │
│                                                          │
│  ┌─────────────────────────────────────────────┐       │
│  │  OrderEntryViewModel (SLE)                   │       │
│  │  - Place orders                              │       │
│  │  - Manage order book                         │       │
│  └─────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────┘
```
