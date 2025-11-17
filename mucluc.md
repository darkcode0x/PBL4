# MỤC LỤC

## MỞ ĐẦU
1. TÍNH CẤP THIẾT CỦA ĐỀ TÀI
2. MỤC ĐÍCH VÀ Ý NGHĨA CỦA ĐỀ TÀI
   2.1. Mục đích
   2.2. Ý nghĩa
3. ĐỐI TƯỢNG VÀ PHẠM VI ĐỀ TÀI
   3.1. Đối tượng
   3.2. Phạm vi

## CHƯƠNG 1. CƠ SỞ LÝ THUYẾT
1. TỔNG QUAN VỀ KEYLOGGER
   1.1. Định nghĩa và phân loại
   1.2. Keylogger phần mềm
   1.3. Các kỹ thuật thu thập dữ liệu phím
2. TỔNG QUAN VỀ BOTNET
   2.1. Định nghĩa và thành phần
   2.2. Kiến trúc Botnet
   2.3. Cơ chế Command and Control (C&C)
   2.4. Các mô hình giao tiếp trong Botnet
3. GIAO THỨC DNS VÀ DNS TUNNELING
   3.1. Tổng quan về DNS
       3.1.1. Kiến trúc DNS
       3.1.2. Các loại DNS Record
       3.1.3. Quy trình phân giải tên miền
   3.2. DNS Tunneling
       3.2.1. Khái niệm và nguyên lý
       3.2.2. Phương thức mã hóa dữ liệu trong DNS Query
       3.2.3. Ưu và nhược điểm của DNS Tunneling
4. CƠ CHẾ WINDOWS KEYBOARD HOOKING
   4.1. Khái niệm Keyboard Hook
   4.2. Windows Hook Chain
   4.3. API SetWindowsHookEx
   4.4. Cơ chế xử lý sự kiện bàn phím
5. KỸ THUẬT OBFUSCATION VÀ EVASION
   5.1. Mã hóa và làm rối mã nguồn
   5.2. Kỹ thuật bypass Antivirus
   5.3. Persistence mechanisms

## CHƯƠNG 2. PHÂN TÍCH VÀ THIẾT KẾ HỆ THỐNG
1. KIẾN TRÚC TỔNG THỂ
   1.1. Mô hình tổng quan
   1.2. Các thành phần chính
   1.3. Luồng dữ liệu trong hệ thống
2. HẠ TẦNG MẠNG VÀ MÔING LAB
   2.1. Thiết kế môi trường Lab
   2.2. TailScale VPN
       2.2.1. Vai trò và chức năng
       2.2.2. Cấu hình và tích hợp
   2.3. DNS Resolver với BIND9
       2.3.1. Cấu hình Authoritative DNS Server
       2.3.2. Zone configuration
       2.3.3. Forwarding và Conditional Forwarding
3. COMMAND AND CONTROL SERVER
   3.1. Kiến trúc Server (C# .NET)
       3.1.1. Cấu trúc dự án và các module
       3.1.2. Client Manager và Protocol Handler
   3.2. DNS Tunneling Handler
       3.2.1. DNS Parser và Request Processing
       3.2.2. DNS Response Builder
       3.2.3. Cơ chế mã hóa và giải mã dữ liệu
   3.3. Server Logic và Command Dispatcher
       3.3.1. Quản lý phiên làm việc
       3.3.2. Xử lý lệnh điều khiển
       3.3.3. Remote Shell Integration
   3.4. Giao diện điều khiển (UI)
       3.4.1. MainForm và quản lý Client
       3.4.2. RemoteShellForm và tương tác Shell
4. BOTNET CLIENT
   4.1. Kiến trúc Client (C++)
       4.1.1. Cấu trúc module và thiết kế OOP
       4.1.2. Interface IClient và ConsoleClient
   4.2. Keylogger Module
       4.2.1. Cài đặt Keyboard Hook
       4.2.2. Callback function và xử lý keystroke
       4.2.3. Lưu trữ và buffer dữ liệu
   4.3. Network Module và DNS Communication
       4.3.1. Thiết kế DNS Query
       4.3.2. Encoding scheme cho dữ liệu
       4.3.3. Xử lý DNS Response
   4.4. Shell Module
       4.4.1. Command execution
       4.4.2. Output capturing và encoding
   4.5. Botnet Core Logic
       4.5.1. Khởi tạo và kết nối với C&C
       4.5.2. Heartbeat và keep-alive mechanism
       4.5.3. Command polling và execution flow
5. VECTOR LÂY NHIỄM BAN ĐẦU
   5.1. Macro-based Delivery
       5.1.1. Phân tích mã VBA Obfuscated
       5.1.2. Kỹ thuật Social Engineering
   5.2. PowerShell Stager
       5.2.1. Phân tích script cài đặt giai đoạn hai
       5.2.2. Web-based payload delivery
   5.3. Persistence Mechanisms
    5.3.1. Scheduled Tasks
    5.3.2. Registry Run Keys
       5.3.3. Service Installation

## CHƯƠNG 3. TRIỂN KHAI VÀ DEMO HỆ THỐNG
1. QUY TRÌNH TRIỂN KHAI
   1.1. Cài đặt môi trường Lab
   1.2. Cấu hình DNS Infrastructure
   1.3. Build và deploy C&C Server
   1.4. Compile và package Client
2. KỊCH BẢN DEMO
   2.1. Initial Infection via Macro
   2.2. Client Registration và C&C Connection
   2.3. Keystroke Logging và Data Exfiltration
   2.4. Remote Shell Interaction
3. ĐÁNH GIÁ KẾT QUẢ
   3.1. Hiệu quả của DNS Tunneling
   3.2. Độ ẩn danh và khả năng bypass
   3.3. Độ ổn định của kênh C&C
   3.4. Performance Analysis

## CHƯƠNG 4. PHƯƠNG PHÁP PHÁT HIỆN VÀ PHÒNG CHỐNG
1. PHÁT HIỆN DNS TUNNELING
   1.1. Network-based Detection
       1.1.1. Phân tích traffic pattern
       1.1.2. Anomaly detection trong DNS queries
       1.1.3. Entropy analysis của domain names
       1.4. Query frequency và packet size analysis
   1.2. DNS Query Characteristics
       1.2.1. Subdomain length và randomness
       1.2.2. Record type distribution
       1.2.3. TTL và response analysis
   1.3. Machine Learning Approaches
       1.3.1. Feature engineering cho DNS traffic
       1.3.2. Classification models
       1.3.3. Real-time detection systems
2. PHÁT HIỆN KEYLOGGER
   2.1. Host-based Detection
       2.1.1. Process monitoring và suspicious behaviors
       2.1.2. Hook detection techniques
       2.1.3. Memory forensics
   2.2. API Monitoring
       2.2.1. Tracking SetWindowsHookEx calls
       2.2.2. Kernel-mode detection
   2.3. Behavioral Analysis
       2.3.1. Keystroke capture patterns
       2.3.2. File I/O monitoring
3. PHÁT HIỆN BOTNET C&C
   3.1. Network Traffic Analysis
       3.1.1. C&C communication patterns
       3.1.2. Beaconing detection
       3.1.3. Protocol analysis
   3.2. Endpoint Detection and Response (EDR)
       3.2.1. Process tree analysis
       3.2.2. Network connection tracking
       3.2.3. Suspicious registry modifications
4. GIẢI PHÁP PHÒNG CHỐNG
   4.1. Network Security Controls
       4.1.1. DNS filtering và sinkholing
       4.1.2. Firewall rules và ACLs
       4.1.3. IDS/IPS signatures
   4.2. Endpoint Protection
       4.2.1. Application whitelisting
       4.2.2. Macro security policies
       4.2.3. PowerShell execution restrictions
   4.3. Security Monitoring
       4.3.1. SIEM integration
       4.3.2. Log analysis và correlation
       4.3.3. Threat hunting procedures
   4.4. User Awareness Training
       4.4.1. Phishing recognition
       4.4.2. Safe computing practices
5. ĐỀ XUẤT GIẢI PHÁP TỔNG HỢP
   5.1. Defense-in-depth Strategy
   5.2. Incident Response Playbook
   5.3. Continuous Monitoring Framework

## KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN
1. TỔNG KẾT CÁC KẾT QUẢ ĐẠT ĐƯỢC
2. HẠN CHẾ VÀ THÁCH THỨC
3. HƯỚNG PHÁT TRIỂN
   3.1. Cải tiến kỹ thuật evasion
   3.2. Mở rộng detection capabilities
   3.3. Automated response mechanisms

## TÀI LIỆU THAM KHẢO

## PHỤ LỤC
A. Cấu hình BIND9
B. DNS Protocol Specifications
C. Code snippets quan trọng
D. Lab Setup Guide