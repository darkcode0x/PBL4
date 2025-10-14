# REFACTORING GUIDE - SERVER ARCHITECTURE

## TONG QUAN

Da refactor thanh cong project Server tu 1 file 621 dong thanh 7 file modular, tong cong ~800 dong code (nhung de quan ly va mo rong).

---

## CAU TRUC MOI

```
Server/
├── Logic/
│   ├── ServerLogic.cs           (220 dong - Main orchestrator)
│   ├── ClientManager.cs         (90 dong - Quan ly connections)
│   └── ProtocolHandler.cs       (120 dong - Xu ly protocol a/b)
│
├── DNS/
│   ├── DNSParser.cs             (70 dong - Parse DNS packets)
│   ├── DNSResponseBuilder.cs   (140 dong - Build DNS responses)
│   └── AuthoritativeDNSHandler.cs (50 dong - Handle normal DNS)
│
├── Utilities/
│   └── IPGenerator.cs           (60 dong - Generate fake IPs)
│
├── Models/
│   ├── ClientInfo.cs            (Existing - Model cho client info)
│   ├── DataParser.cs            (Existing - Parse keystroke data)
│   └── DNSProtocol.cs           (Existing - Enums va exceptions)
│
└── UI/
    └── MainForm.cs               (Existing - Windows Forms GUI)
```

---

## CHI TIET CAC FILE MOI

### 1. DNS/DNSParser.cs
**Trach nhiem:** Parse raw UDP data thanh DNS query structure

**Public methods:**
- `ParseQuery(byte[] data)` -> DNSQueryInfo
  - Parse DNS header (transaction ID, query type)
  - Extract query name tu byte array
  - Return structured query info

- `EncodeDomainName(string domain)` -> byte[]
  - Encode domain name theo DNS format
  - Vi du: "example.com" -> [7]example[3]com[0]

**Lien ket:**
- Duoc goi boi: ServerLogic.ProcessQuery()
- Dependencies: None (pure utility)

---

### 2. DNS/DNSResponseBuilder.cs
**Trach nhiem:** Xay dung cac loai DNS response packets

**Public methods:**
- `CreateSimpleAResponse(query, ipAddress)` -> byte[]
  - Tao DNS A record response don gian
  - Dung cho tat ca keylogger responses (connection + data)

- `CreateEmptyResponse(query)` -> byte[]
  - Tao response rong (NXDOMAIN-like)
  - Dung khi khong biet cach xu ly query

- `CreateAuthoritativeResponse(query, ip, domain)` -> byte[]
  - Tao full authoritative response voi NS, SOA records
  - CHI DUNG trong production mode
  - Dung khi DNS Resolver query NS records

**Lien ket:**
- Duoc goi boi: ServerLogic.ProcessQuery()
- Dependencies: DNSParser (de encode domain names)

---

### 3. DNS/AuthoritativeDNSHandler.cs
**Trach nhiem:** Xu ly normal DNS queries (khong phai keylogger protocol)

**Public methods:**
- `HandleQuery(originalQuery, query, queryName)` -> byte[]
  - Check query type: domain chinh? ns1/ns2? subdomain?
  - Return appropriate DNS response
  - LOCAL TEST: Hiem khi dung
  - PRODUCTION: Bat buoc co

**Query types xu ly:**
```
example.com           -> Authoritative response (A + NS + SOA)
ns1.example.com       -> A record -> server IP
ns2.example.com       -> A record -> server IP
*.example.com         -> A record -> server IP
unknown.com           -> Empty response
```

**Lien ket:**
- Duoc goi boi: ServerLogic.ProcessQuery() trong catch(UnrelatedException)
- Dependencies: DNSResponseBuilder

---

### 4. Utilities/IPGenerator.cs
**Trach nhiem:** Generate fake IP addresses cho DNS responses

**Public static methods:**
- `CreateStartIp(connections)` -> string
  - Generate IP cho connection response
  - Format: {random}.{random}.{random}.{connectionID+1}
  - Tranh reserved IPs (127, 192, 10, response codes, ...)
  - Vi du: connections=0 -> "45.123.67.1"

- `CreateResponseIp(ResponseCode)` -> string
  - Generate IP cho response codes
  - Format: {code}.{random}.{random}.{random}
  - Vi du: ResponseCode.OK -> "200.45.123.67"

**Logic tranh conflict:**
```csharp
Reserved first octets: 0, 10, 100, 127, 169, 172, 192, 198, 203, 224, 233, 250, 255
Reserved for codes: 200, 201, 202, 203, 204
```

**Lien ket:**
- Duoc goi boi: ServerLogic.ProcessQuery()
- Dependencies: Models/DNSProtocol (ResponseCode enum)

---

### 5. Logic/ClientManager.cs
**Trach nhiem:** Quan ly danh sach client connections va data parsers

**Public properties:**
- `ClientCount` -> int

**Public methods:**
- `AddClient(clientIp)` -> int
  - Tao DataParser moi
  - Them vao list
  - Fire events: OnClientAdded, OnClientCountChanged
  - Return connection ID

- `GetParser(connectionId)` -> DataParser?
  - Tra ve parser theo ID
  - Return null neu khong ton tai

- `ConnectionExists(connectionId)` -> bool
  - Check ID co hop le khong

- `SaveAllLogs(logCallback)` -> void
  - Goi SaveToFile() cho tat ca parsers
  - Goi logCallback de update UI

**Events:**
- `OnClientAdded` -> ClientInfo
- `OnClientCountChanged` -> int

**Lien ket:**
- Duoc dung boi: ServerLogic, ProtocolHandler
- Dependencies: Models/DataParser, Models/ClientInfo

---

### 6. Logic/ProtocolHandler.cs
**Trach nhiem:** Xu ly keylogger protocol (type a/b packets)

**Public methods:**
- `GetData(queryName)` -> string
  - Extract subdomain tu full query name
  - Validate: Query co thuoc domain khong?
  - Validate: So dau cham co dung khong?
  - Return: "a.1.1.1" hoac "b.0.5.hexdata"
  - Throw exceptions neu sai format

- `ParseDataPacket(data, clientManager)` -> (PacketNumber, ConnectionId)
  - Parse format: "packetNumber.connectionId.hexData"
  - Validate: Connection ID ton tai?
  - Validate: Hex data hop le?
  - Decode hex -> bytes -> ASCII
  - Goi parser.AddData()
  - Fire event: OnDataReceived
  - Return metadata

**Events:**
- `OnDataReceived` -> (connectionId, decodedText)

**Exceptions throw:**
- ShortCircuitException: Query khong thuoc domain
- UnrelatedException: Format sai (so dau cham)
- DNSSyntaxException: Hex data le ky tu
- NXConnectionException: Connection ID khong ton tai
- DuplicatePacketException: (tu DataParser)

**Lien ket:**
- Duoc dung boi: ServerLogic.ProcessQuery()
- Dependencies: ClientManager, Models

---

### 7. Logic/ServerLogic.cs (REFACTORED)
**Trach nhiem:** Main orchestrator - Dieu phoi tat ca components

**Giam tu 621 dong -> 220 dong**

**Chua:**
- Network layer (UdpClient receive/send)
- Component initialization
- Event wiring
- High-level error handling
- Logging

**KHONG chua nua:**
- DNS parsing logic -> DNSParser
- DNS response building -> DNSResponseBuilder
- Client management -> ClientManager
- Protocol parsing -> ProtocolHandler
- IP generation -> IPGenerator
- Normal DNS handling -> AuthoritativeDNSHandler

**Luong xu ly:**
```
1. ReceiveAsync() -> UDP packet
2. DNSParser.ParseQuery() -> Structured query
3. ProtocolHandler.GetData() -> Extract subdomain
4. Phan loai type "a" hoac "b"
5. Type "a": ClientManager.AddClient() + IPGenerator.CreateStartIp()
6. Type "b": ProtocolHandler.ParseDataPacket()
7. DNSResponseBuilder.Create*Response() -> Response packet
8. Send() response
9. Catch exceptions -> Appropriate responses
```

---

## LOI ICH CUA REFACTORING

### 1. Single Responsibility Principle
Moi class chi lam 1 viec:
- DNSParser: Parse DNS packets
- DNSResponseBuilder: Build DNS responses
- ClientManager: Manage clients
- ProtocolHandler: Handle protocol logic
- ServerLogic: Orchestrate everything

### 2. Testability
Co the test tung component doc lap:
```csharp
// Test DNSParser
var query = DNSParser.ParseQuery(testPacket);
Assert.AreEqual("example.com", query.QueryName);

// Test IPGenerator
var ip = IPGenerator.CreateStartIp(0);
Assert.IsTrue(ip.EndsWith(".1"));

// Test ProtocolHandler
var handler = new ProtocolHandler("example.com");
var data = handler.GetData("a.1.1.1.example.com");
Assert.AreEqual("a.1.1.1", data);
```

### 3. Maintainability
Sua bug hoac them feature chi can sua 1 file nho:
- Bug trong DNS parsing? -> Sua DNSParser.cs
- Them response type moi? -> Sua DNSResponseBuilder.cs
- Thay doi logic IP? -> Sua IPGenerator.cs

### 4. Reusability
Co the reuse components trong project khac:
- DNSParser, DNSResponseBuilder -> Dung cho bat ky DNS server nao
- IPGenerator -> Dung cho bat ky fake IP generator nao

### 5. Readability
Code ro rang, de hieu:
```csharp
// TRUOC (621 dong, tat ca trong 1 file):
private byte[] CreateResponse(...) { /* 80 dong code */ }
private string CreateStartIp(...) { /* 40 dong code */ }
private (int, int) ParseData(...) { /* 60 dong code */ }
...

// SAU (modular):
var response = DNSResponseBuilder.CreateSimpleAResponse(...);
var ip = IPGenerator.CreateStartIp(...);
var metadata = _protocolHandler.ParseDataPacket(...);
```

### 6. Scalability cho Production
De dang mo rong khi deploy production:
- Them AuthoritativeDNSHandler methods cho NS, SOA, MX records
- Them cache layer vao DNSResponseBuilder
- Them rate limiting vao ClientManager
- Them encryption vao ProtocolHandler

---

## HUONG DAN SU DUNG

### Build Project
```
Trong Visual Studio:
1. Build -> Rebuild Solution
2. Kiem tra khong co loi
3. Tat ca 7 file moi da duoc compiled
```

### Testing
```
1. Start Server
2. Start Client
3. Go phim
4. Kiem tra Server log -> Co hien thi decoded keystrokes
5. Kiem tra ./logs/ -> Co file log
```

### Debugging
Neu co loi, check tung component:
```
1. DNSParser: Dung breakpoint trong ParseQuery()
2. ProtocolHandler: Check GetData() va ParseDataPacket()
3. ClientManager: Check AddClient() va GetParser()
4. ServerLogic: Check ProcessQuery() flow
```

---

## MIGRATION NOTES

### Khong can thay doi:
- Client code (van hoat dong nhu cu)
- Models (ClientInfo, DataParser, DNSProtocol)
- UI (MainForm)
- Protocol (van la DNS tunneling voi type a/b)

### Da thay doi:
- Cau truc folder (them DNS/, Utilities/)
- Code organization (tach thanh nhieu file)
- Dependency injection (ServerLogic nhan components)

### Breaking changes:
KHONG CO - API public cua ServerLogic khong doi:
```csharp
// Van dung nhu cu:
server.Start(53, "example.com", "./logs");
server.Stop();
server.OnLogMessage += (msg) => Console.WriteLine(msg);
```

---

## FUTURE ENHANCEMENTS

### 1. Them Config System
```csharp
Utilities/ConfigManager.cs:
- Load settings tu file
- Domain, IP, Port, Timeouts
- Production vs Local mode
```

### 2. Them Logging System
```csharp
Utilities/Logger.cs:
- File logging
- Structured logging (JSON)
- Log levels (Debug, Info, Warning, Error)
```

### 3. Them Encryption
```csharp
Utilities/Encryption.cs:
- AES encryption cho hex data
- Key exchange protocol
```

### 4. Them Authentication
```csharp
Logic/AuthenticationHandler.cs:
- Client authentication
- Token-based auth
- Prevent unauthorized connections
```

### 5. Them Caching
```csharp
DNS/DNSCache.cs:
- Cache DNS responses
- TTL management
- Reduce processing overhead
```

---

## KET LUAN

Refactoring thanh cong! Project Server da duoc to chuc lai theo best practices:
- Single Responsibility
- Separation of Concerns
- Modular design
- Easy to test
- Easy to extend

Code van hoat dong giong y nhu truoc, nhung bay gio:
- De maintain hon
- De test hon
- De mo rong hon
- De hieu hon
- San sang cho production deployment

