# Hướng dẫn chạy hệ thống quản lý nhóm học tập

Solution gồm ba project: **CommandService** xử lý đăng ký/đăng nhập và lệnh quản lý nhóm, **QueryService** cung cấp API đọc, **ConsumerService** nhận sự kiện Kafka và cập nhật read model MongoDB.

## Chạy nhanh bằng một file

Sau khi đã cài Python, .NET 9 SDK/runtime, SQL Server, MongoDB và Docker Desktop:

1. Mở thư mục gốc solution `D:\HocTap\KTVTKPM\CQRS`.
2. Chạy `chay_du_an.py` bằng cách nhấp đúp hoặc mở terminal tại thư mục đó và chạy:

   ```powershell
   python chay_du_an.py
   ```

3. Chờ script kiểm tra/khởi động SQL Server `SIU\SQLEXPRESS`, MongoDB, Docker Desktop, Kafka/ZooKeeper và ba project .NET. Script sẽ mở giao diện tại [http://localhost:5088/](http://localhost:5088/).

Script tự tạo hoặc đồng bộ JWT signing key trong .NET user-secrets nếu cần; key không được ghi vào source code. Database `WriteDB` cần được tạo sẵn trong SQL Server.

Giữ cửa sổ script mở trong khi sử dụng ứng dụng. Nhấn **Ctrl+C** để dừng ba service .NET do script chạy; SQL Server, MongoDB và các container Kafka/ZooKeeper vẫn tiếp tục chạy. Muốn dừng Kafka/ZooKeeper, mở terminal tại thư mục gốc rồi chạy `docker compose down`.

Nếu script báo thiếu phần mềm, không đủ quyền khởi động SQL Server/MongoDB hoặc Docker Desktop chưa sẵn sàng, hãy làm theo các bước thủ công bên dưới.

## 1. Mở lại dự án sau khi tắt VS Code

1. Mở VS Code.
2. Chọn **File → Open Folder...**
3. Mở thư mục:

   ```text
   D:\HocTap\KTVTKPM\CQRS
   ```

4. Chờ VS Code tải solution. Mở terminal bằng **Terminal → New Terminal** và kiểm tra:

   ```powershell
   dotnet --list-sdks
   ```

   Máy cần có .NET 9 SDK. Nếu chưa có, cài .NET 9 SDK rồi mở lại VS Code.

Đóng VS Code không xóa source code, database hay JWT signing key đã lưu trong .NET user-secrets. Nếu service không còn chạy sau khi mở lại, làm tiếp các bước dưới đây để khởi động lại.

## 2. Bật SQL Server, MongoDB và Docker

### SQL Server

- Mở SSMS và kết nối tới instance `SIU\SQLEXPRESS`.
- Đảm bảo database `WriteDB` đã tồn tại. Service tự tạo các bảng `Accounts`, `StudyGroups`, `GroupMembers`, `JoinRequests` và `OutboxMessages` khi khởi động; service không tự tạo database `WriteDB`.
- Connection string nằm trong `CommandService\appsettings.json`. Nếu SQL Server hoặc tên database khác, sửa connection string ở đó.

### MongoDB

Đảm bảo MongoDB đang chạy và lắng nghe tại `mongodb://localhost:27017/`. 
MongoDB không nằm trong `docker-compose.yml` của solution.

### Kafka và ZooKeeper

Mở Docker Desktop, sau đó tại terminal VS Code ở thư mục gốc solution chạy:

```powershell
docker compose up -d
docker compose ps
```

Kafka cần lắng nghe ở `localhost:9092`. Lệnh `up -d` có thể chạy lại an toàn khi các container đã chạy.

## 3. Cấu hình JWT signing key

Hai API phải dùng chung signing key. Trên máy này key đã được lưu bằng .NET user-secrets; nếu chuyển sang máy khác hoặc thấy lỗi `Authentication:SigningKey is not configured`, mở một terminal ở thư mục gốc solution và chạy một lần:

```powershell
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
$rng.Dispose()
dotnet user-secrets set "Authentication:SigningKey" $key --project CommandService
dotnet user-secrets set "Authentication:SigningKey" $key --project QueryService
Remove-Variable key, bytes, rng
```

Không ghi signing key vào `appsettings.json`, Git hoặc tài liệu. User-secrets nằm ngoài solution và vẫn còn sau khi đóng VS Code.

## 4. Chạy ba service

Mở **ba terminal riêng** trong VS Code. Giữ mỗi terminal mở trong khi dùng ứng dụng.

**Terminal 1 - CommandService** (API ghi và giao diện):

```powershell
dotnet run --project CommandService --launch-profile http
```

**Terminal 2 - QueryService** (API đọc):

```powershell
dotnet run --project QueryService --launch-profile http
```

**Terminal 3 - ConsumerService** (Kafka consumer):

```powershell
dotnet run --project ConsumerService
```

Khi mỗi terminal báo ứng dụng đã khởi động, mở:

- Giao diện đăng ký/đăng nhập: [http://localhost:5088/](http://localhost:5088/)
- Swagger CommandService: [http://localhost:5088/swagger](http://localhost:5088/swagger)
- Swagger QueryService: [http://localhost:5135/swagger](http://localhost:5135/swagger)

Nếu muốn dùng các service đang chạy ở port tùy chỉnh, ví dụ CommandService `5201` và QueryService `5202`, mở `http://localhost:5201/?queryApi=http://localhost:5202` và đảm bảo origin của giao diện đã có trong `QueryService\appsettings.json` mục `Cors:AllowedOrigins`.

## 5. Thử chức năng

1. Trên giao diện, chọn **Đăng ký**, nhập tên, email và mật khẩu (ít nhất 8 ký tự). Đăng ký thành công sẽ tự đăng nhập.
2. Tạo một nhóm khi đã đăng nhập.
3. Đăng ký tài khoản thứ hai, đăng nhập tài khoản đó rồi gửi yêu cầu tham gia nhóm.
4. Đăng nhập lại tài khoản chủ nhóm, chọn **Yêu cầu chờ duyệt**, sau đó duyệt hoặc từ chối yêu cầu.
5. Đợi ConsumerService xử lý Kafka event; số thành viên và trạng thái yêu cầu sẽ cập nhật ở read model MongoDB.

API tự lấy danh tính từ JWT, không nhận `OwnerId` hoặc `UserId` tùy ý từ giao diện. Tìm kiếm và xem chi tiết nhóm là public; tạo nhóm, gửi yêu cầu và xem/duyệt yêu cầu cần đăng nhập. Token hết hạn sau 60 phút.

### Tài khoản demo phân quyền nhóm

Đã tạo sẵn ba tài khoản demo trong database `WriteDB` để kiểm tra phân quyền trên nhóm `Phan quyen demo`:

| Email | Vai trò trong nhóm | Quyền kiểm tra |
|---|---|---|
| `owner.demo@studyspace.test` | Chủ nhóm (`Owner`) | Duyệt hoặc từ chối yêu cầu tham gia |
| `member.demo@studyspace.test` | Thành viên (`Member`) | Là thành viên; không được duyệt yêu cầu |
| `applicant.demo@studyspace.test` | Chưa là thành viên | Có một yêu cầu tham gia đang chờ chủ nhóm duyệt |

Ba tài khoản dùng cùng mật khẩu demo được cung cấp riêng trong phiên thiết lập. Đây chỉ là dữ liệu học tập trên máy local; không dùng mật khẩu này cho tài khoản thật hoặc môi trường production. README không lưu mật khẩu để tránh đưa thông tin đăng nhập vào source code.

Sau khi đăng nhập, giao diện có các tab **Khám phá nhóm**, **Nhóm của tôi**, **Tạo nhóm** và **Cách hoạt động**. Tab **Nhóm của tôi** tách nhóm bạn làm chủ, nhóm đã tham gia, yêu cầu bạn đã gửi đang chờ duyệt và số yêu cầu trong nhóm của bạn cần xử lý; danh sách được lấy theo tài khoản đăng nhập. Trong nhóm làm chủ, chọn **Yêu cầu chờ duyệt** để xem và duyệt hoặc từ chối từng yêu cầu. Khi tạo nhóm, ngoài tên, môn học, mô tả và sĩ số, bạn có thể nhập mục tiêu học tập, lịch sinh hoạt, trình độ phù hợp, hình thức học (trực tuyến / gặp trực tiếp / linh hoạt) và địa điểm hoặc đường dẫn họp. Các thông tin này được lưu trong SQL Server, gửi qua Kafka và hiển thị từ read model MongoDB.

Có thể xem ví dụ gọi API trong `CommandService\CommandService.http` và `QueryService\QueryService.http`. Các giá trị token, group ID và request ID trong file cần thay bằng giá trị nhận được khi chạy thử.

> Nhóm được tạo trước khi thêm đăng nhập có thể mang OwnerId không thuộc tài khoản nào. Để kiểm tra chức năng chủ nhóm duyệt yêu cầu, hãy tạo nhóm mới sau khi đăng nhập.

## 6. Dừng và chạy lại

- Để dừng một service, vào terminal tương ứng và nhấn **Ctrl+C**.
- Để dừng Kafka/ZooKeeper, chạy ở thư mục gốc solution:

  ```powershell
  docker compose down
  ```

- Lần sau mở VS Code, thực hiện lại bước 2 và 4. Nếu Docker vẫn đang chạy, `docker compose up -d` sẽ dùng lại các container hiện có.

Tài liệu C4, sơ đồ kiến trúc, mô hình dữ liệu và hợp đồng API nằm trong `.github\modernize\assessment\engines\facts\`.

## Giới hạn của bản demo

- Kafka local dùng plaintext; không dùng cấu hình này để triển khai production.
- Chưa có refresh token, giới hạn số lần đăng nhập sai, xác minh email hoặc đặt lại mật khẩu.
