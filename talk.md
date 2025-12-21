# Kịch bản Bảo vệ Đồ án PBL4: Hệ điều hành và Mạng máy tính

**Đề tài:** PHÁT TRIỂN MỘT MÃ ĐỘC KEYLOGGER VÀ BOTNET THU THẬP DỮ LIỆU VÀ KIỂM SOÁT NGƯỜI DÙNG

---

## 1. Mở đầu

Kính thưa Thầy/Cô và các bạn,

Em xin đại diện nhóm trình bày về đồ án PBL4 với đề tài: **"Phát triển một mã độc Keylogger và Botnet thu thập dữ liệu và kiểm soát người dùng"**.

Lý do chúng em chọn đề tài này là vì nó phản ánh một cách trực quan và sâu sắc nhất sự giao thoa giữa hai môn học cốt lõi: **Hệ điều hành** và **Mạng máy tính**. Để xây dựng được hệ thống này, chúng em buộc phải hiểu sâu về cách hệ điều hành quản lý tiến trình, xử lý sự kiện đầu vào, cũng như cách các giao thức mạng vận hành ở tầng thấp. Mục tiêu cuối cùng không phải là tạo ra công cụ tấn công, mà là thông qua việc mô phỏng tấn công để hiểu rõ bản chất, từ đó có tư duy tốt hơn cho việc phòng thủ và bảo mật hệ thống.

## 2. Phạm vi và định hướng nghiên cứu

Em xin nhấn mạnh ngay từ đầu, đây là một mô hình **nghiên cứu và mô phỏng (Proof-of-Concept)**. Toàn bộ hệ thống được triển khai trong môi trường phòng thí nghiệm (Lab) có kiểm soát, sử dụng mạng riêng ảo để cô lập hoàn toàn với Internet bên ngoài.

Chúng em tiếp cận đề tài dưới góc độ kỹ thuật: phân tích kiến trúc phần mềm, cơ chế giao tiếp và luồng dữ liệu. Chúng em tuyệt đối không hướng dẫn hay khuyến khích việc phát tán, sử dụng mã độc này vào các mục đích phi pháp hay gây hại cho cộng đồng.

## 3. Kiến trúc tổng thể hệ thống

Về mặt tổng quan, hệ thống của chúng em được xây dựng theo mô hình **Client – Server** kinh điển, nhưng có sự tùy biến đặc thù của một Botnet:

*   **Phía Client (Bot):** Là phần mềm chạy ngầm trên máy nạn nhân, có nhiệm vụ thu thập thông tin và thực thi lệnh.
*   **Phía Server (C&C - Command & Control):** Đóng vai trò là trung tâm chỉ huy, quản lý các kết nối và điều phối hoạt động của mạng lưới bot.
*   **Giao thức giao tiếp:** Điểm đặc biệt trong kiến trúc này là chúng em không sử dụng các kết nối TCP/HTTP thông thường mà sử dụng kỹ thuật **DNS Tunneling**. Tức là, mọi dữ liệu điều khiển và thu thập đều được "ngụy trang" bên trong các gói tin truy vấn và phản hồi tên miền (DNS).

## 4. Phân tích chi tiết phía Client (Phần em phụ trách)

Là người chịu trách nhiệm chính phát triển phía Client, em xin đi sâu vào cơ chế hoạt động của thành phần này trên Hệ điều hành Windows.

**Thứ nhất, về tương tác với Hệ điều hành:**
Client hoạt động như một tiến trình nền (background process). Để thu thập dữ liệu bàn phím, em sử dụng cơ chế **Hooking** của Windows API, cụ thể là `WH_KEYBOARD_LL`. Cơ chế này cho phép chương trình "lắng nghe" toàn bộ sự kiện nhấn phím trên hệ thống trước khi chúng được gửi đến các ứng dụng đích. Điều này minh chứng rõ nét cho khái niệm xử lý sự kiện và ngắt (interrupt) trong Hệ điều hành.

Ngoài ra, Client còn có khả năng tương tác với **Shell** (cmd.exe). Em sử dụng kỹ thuật tạo tiến trình con (child process) với các đường ống (pipes) để chuyển hướng đầu vào/đầu ra (I/O Redirection). Nhờ đó, Server có thể gửi lệnh CMD và nhận kết quả trả về mà người dùng không hề hay biết.

**Thứ hai, về luồng dữ liệu:**
Khi người dùng gõ phím, dữ liệu thô sẽ được Client bắt lại, đưa vào hàng đợi (Queue), sau đó được mã hóa (thường là Hex hoặc Base64) để đảm bảo tính toàn vẹn khi truyền qua mạng.

**Thứ ba, về vector lây nhiễm:**
Chúng em lựa chọn **Excel** làm môi trường khởi phát. Đây là một ví dụ điển hình của kỹ thuật "Living off the Land" – tận dụng chính các công cụ hợp pháp có sẵn trên OS. Thông qua **VBA Macro**, chúng em kích hoạt một chuỗi thực thi: từ Macro gọi PowerShell, từ PowerShell tải và chạy Client. Việc này cho thấy sự nguy hiểm khi các tiến trình hợp pháp có quyền gọi lẫn nhau mà thiếu sự kiểm soát.

## 5. Tổng quan phía Server

Về phía Server (do bạn cộng sự phụ trách chính), vai trò của nó không chỉ là một máy chủ nhận tin, mà nó hoạt động như một **Authoritative DNS Server** (Máy chủ DNS có thẩm quyền).

Thay vì phân giải tên miền ra IP như thông thường, Server này sẽ phân tích các chuỗi ký tự lạ trong tên miền (subdomain) mà Client gửi lên để trích xuất dữ liệu. Ngược lại, khi cần gửi lệnh xuống Client, Server sẽ nhúng lệnh đó vào các bản ghi phản hồi như TXT record hoặc thậm chí là giả mạo địa chỉ IP trong A record. Server cũng quản lý danh sách các Bot thông qua ID định danh, đảm bảo việc điều khiển chính xác từng máy trạm.

## 6. Góc nhìn Mạng máy tính

Từ góc độ Mạng máy tính, đồ án này khai thác sâu vào giao thức **DNS (Domain Name System)**.

Thông thường, tường lửa (Firewall) sẽ chặn các cổng lạ nhưng luôn mở cổng 53 (UDP) cho DNS để đảm bảo kết nối Internet. Lợi dụng đặc điểm này, chúng em thiết lập một "đường hầm" (Tunnel).
*   **Luồng lên (Upstream):** Dữ liệu từ Client được chia nhỏ, gắn vào làm subdomain (ví dụ: `data.malware.com`) và gửi đi dưới dạng truy vấn DNS.
*   **Luồng xuống (Downstream):** Server phản hồi truy vấn đó kèm theo dữ liệu điều khiển.

Đây là minh chứng rõ ràng cho việc một giao thức mạng có thể bị lạm dụng nếu không có cơ chế giám sát nội dung gói tin (Deep Packet Inspection).

## 7. Kịch bản trình bày Demo

Sau đây, em xin phép trình bày phần Demo hoạt động của hệ thống.

**(Bước 1: Khởi tạo Server)**
Đầu tiên, trên máy Server, chúng em khởi chạy ứng dụng C&C. Giao diện hiện lên cho thấy Server đang lắng nghe trên cổng 53 UDP và sẵn sàng nhận kết nối.

**(Bước 2: Lây nhiễm phía Client)**
Chuyển sang máy nạn nhân (Client). Giả sử người dùng nhận được một file Excel báo cáo tài chính. Khi người dùng mở file này và chọn "Enable Content" để chạy Macro, quá trình lây nhiễm bắt đầu.
Như Thầy/Cô thấy trên màn hình, Macro âm thầm gọi PowerShell để tải xuống Payload. Một cửa sổ UAC có thể hiện lên nếu cần quyền cao hơn, và ngay sau đó, một Scheduled Task tên là "Check memory" được tạo ra để đảm bảo mã độc tự chạy lại sau khi khởi động máy.
Tiến trình mã độc sau đó tự đổi tên thành `system.exe` để ngụy trang trong Task Manager.

**(Bước 3: Thiết lập kết nối)**
Quay lại màn hình Server, chúng ta thấy xuất hiện một Client mới vừa đăng ký với ID định danh. Điều này chứng tỏ gói tin DNS đầu tiên (gói 'a') đã đi qua được tường lửa và đến đích.

**(Bước 4: Thu thập dữ liệu phím - Keylogger)**
Bây giờ, em sẽ gõ thử một đoạn văn bản trên máy Client, ví dụ: "Login facebook...".
Ngay lập tức trên Server, tại tab "Keylogger", các ký tự này xuất hiện gần như theo thời gian thực. Dữ liệu này đã được Client đóng gói vào các subdomain (gói 'b') và Server đã giải mã thành công.

**(Bước 5: Điều khiển từ xa - Remote Shell)**
Cuối cùng, em sẽ thử gửi một lệnh từ Server xuống Client. Em nhập lệnh `dir` để xem danh sách file.
Lệnh này được Server hàng đợi và gửi xuống Client qua bản ghi TXT (gói 'p'). Client thực thi và gửi kết quả trả về (gói 'c'). Trên giao diện Server, chúng ta đã nhận được kết quả liệt kê thư mục từ máy nạn nhân.

## 8. Phương pháp phát hiện và phòng chống

Từ việc phân tích cơ chế tấn công trên, nhóm đề xuất các phương pháp phòng chống cụ thể như sau:

**Thứ nhất, ở cấp độ Người dùng cuối:**
Biện pháp đơn giản nhưng hiệu quả nhất là **tắt hoàn toàn Macro** trong bộ Office và cảnh giác với các file lạ yêu cầu quyền Admin (UAC).

**Thứ hai, ở cấp độ Mạng (Network):**
Hệ thống phòng thủ cần giám sát lưu lượng DNS (DNS Monitoring). Các dấu hiệu bất thường của DNS Tunneling rất dễ nhận biết nếu chú ý:
*   Tên miền truy vấn (Query Name) có độ dài bất thường và chứa nhiều ký tự ngẫu nhiên (entropy cao).
*   Tần suất truy vấn cao đột biến từ một máy trạm tới một tên miền cụ thể.
*   Kích thước gói tin phản hồi (TXT record) lớn hơn mức trung bình.

**Thứ ba, ở cấp độ Máy trạm (Endpoint):**
Cần giám sát hành vi của các tiến trình (Behavior Monitoring).
*   Phát hiện các chuỗi khởi tạo bất thường: Ví dụ `Excel.exe` sinh ra tiến trình con là `PowerShell.exe` hoặc `cmd.exe`. Đây là dấu hiệu chắc chắn của hành vi độc hại.
*   Kiểm tra các tiến trình chạy từ thư mục tạm (`AppData`, `Temp`) nhưng lại mang tên hệ thống (`svchost.exe`, `system.exe`).

## 9. Đánh giá và hạn chế

**Về ưu điểm:** Hệ thống đã mô phỏng thành công luồng tấn công trọn vẹn: từ lây nhiễm qua file văn phòng, thiết lập kênh ngầm, đến việc chiếm quyền điều khiển shell và lấy dữ liệu.

**Tuy nhiên, hệ thống còn nhiều hạn chế:**
*   **Hiệu năng:** Do sử dụng DNS (giao thức UDP không tin cậy) và giới hạn kích thước gói tin rất nhỏ, tốc độ truyền dữ liệu rất chậm và độ trễ cao.
*   **Tính ổn định:** Việc gửi quá nhiều truy vấn DNS liên tục dễ gây nghẽn hoặc bị các hệ thống giám sát mạng phát hiện bất thường.
*   **Bảo mật:** Dữ liệu hiện tại chỉ được mã hóa đơn giản, chưa áp dụng các thuật toán mã hóa mạnh, nên nếu bị bắt gói tin (sniffing) thì dễ dàng giải mã được nội dung.

## 10. Hướng phát triển và bài học rút ra

Qua đồ án này, bài học lớn nhất mà nhóm rút ra được là sự mong manh của các hệ thống nếu chỉ dựa vào các biện pháp bảo vệ bề mặt. Chúng em hiểu sâu sắc hơn về cách OS quản lý quyền hạn và cách các giao thức mạng vận hành.

Nếu được phát triển tiếp, chúng em sẽ tập trung vào khía cạnh **Phòng thủ chuyên sâu**:
*   Xây dựng module IDS (Intrusion Detection System) đơn giản để tự động phát hiện mẫu tấn công này.
*   Nghiên cứu các kỹ thuật "Unhooking" để khôi phục trạng thái sạch cho hệ điều hành khi bị nhiễm.

## 11. Kết thúc

Tóm lại, trong đồ án này, em đã đóng góp chính trong việc xây dựng nhân (core) của Client, xử lý các kỹ thuật Hooking, đa luồng và giao tiếp mạng mức thấp.

Đồ án không chỉ giúp em củng cố kiến thức lý thuyết về Hệ điều hành và Mạng máy tính mà còn mang lại cái nhìn thực tế về an ninh mạng. Em tin rằng việc hiểu rõ cách thức tấn công là bước chuẩn bị tốt nhất để trở thành một kỹ sư an toàn thông tin trong tương lai.

Em xin chân thành cảm ơn Thầy/Cô đã lắng nghe. Em rất mong nhận được những câu hỏi và góp ý từ Hội đồng ạ.
