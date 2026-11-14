# Từ điển dữ liệu Morning Brew CRM

41 bảng, 314 trường. Sinh từ tools/schema.py.

## 1. Roles
Tài khoản • Điều chỉnh bảng gốc. Năm vai trò nghiệp vụ dùng chung cho ba ứng dụng.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| RoleID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Code<br>varchar(30) | Bắt buộc<br>UNIQUE | Mã ổn định dùng trong phân quyền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Owner |
| Name<br>nvarchar(100) | Bắt buộc | Tên hiển thị tiếng Việt.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Chủ doanh nghiệp |
- UNIQUE (Code)
- CHECK Code IN ('Owner','AreaManager','BranchManager','Staff','Customer')

## 2. Permissions
Tài khoản • Bảng bổ sung. Tách quyền thao tác khỏi tên vai trò để cấp quyền rõ ràng.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| PermissionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Code<br>varchar(80) | Bắt buộc<br>UNIQUE | Mã chức năng được backend kiểm tra.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Orders.Manage |
| Description<br>nvarchar(250) | Bắt buộc | Giải thích hành động được cho phép.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Xử lý đơn hàng trong phạm vi phụ trách |
- UNIQUE (Code)

## 3. RolePermissions
Tài khoản • Bảng bổ sung. Liên kết vai trò với các quyền được cấp.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| RoleID<br>int | Bắt buộc<br>PK ghép<br>FK → Roles.<br>RoleID | Liên kết tới Roles; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| PermissionID<br>int | Bắt buộc<br>PK ghép<br>FK → Permissions.<br>PermissionID | Liên kết tới Permissions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |

## 4. Users
Tài khoản • Điều chỉnh bảng gốc. Thông tin đăng nhập và liên hệ duy nhất, tránh sao chép sang Customers.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| UserID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| RoleID<br>int | Bắt buộc<br>FK → Roles.<br>RoleID | Liên kết tới Roles; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Email<br>nvarchar(254) | Bắt buộc | Email hiển thị và nhận diện đăng nhập.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: lan@example.test |
| NormalizedEmail<br>nvarchar(254) | Bắt buộc<br>UNIQUE | Email viết hoa và bỏ khoảng trắng hai đầu; duy nhất không phân biệt hoa thường.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: LAN@EXAMPLE.TEST |
| PasswordHash<br>varchar(512) | Bắt buộc | Hash PBKDF2 định dạng ASP.NET PasswordHasher; không lưu mật khẩu thô.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: AQAAAAIAAYag... |
| FullName<br>nvarchar(120) | Bắt buộc | Tên nhân viên/khách hàng.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Nguyễn Lan |
| Phone<br>varchar(20) | Được trống | Số điện thoại liên hệ; không duy nhất vì có thể dùng chung.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 0901234567 |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- UNIQUE (NormalizedEmail)
- CHECK NormalizedEmail = UPPER(LTRIM(RTRIM(Email)))

## 5. UserSessions
Tài khoản • Bảng bổ sung. Lưu phiên và hash refresh token để thu hồi đăng nhập từng thiết bị.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| SessionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| UserID<br>int | Bắt buộc<br>FK → Users.<br>UserID | Liên kết tới Users; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| TokenHash<br>binary(32) | Bắt buộc<br>UNIQUE | SHA-256 của refresh token ngẫu nhiên; không lưu token có thể dùng trực tiếp.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 32 bytes |
| DeviceName<br>nvarchar(120) | Được trống | Nhãn thiết bị cho khách nhận biết phiên.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Android cá nhân |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| ExpiresAt<br>datetime2(3) | Bắt buộc | Hạn sử dụng UTC bắt buộc; ngăn phiên hoặc voucher tồn tại vô thời hạn.<br>Nguồn: Backend ghi khi cấp phiên/voucher; voucher lấy hạn của chương trình.<br>Ví dụ: 2027-09-17T03:00:00Z |
| RevokedAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- UNIQUE (TokenHash)
- CHECK ExpiresAt IS NOT NULL AND ExpiresAt > CreatedAt
- CHECK RevokedAt IS NULL OR RevokedAt >= CreatedAt
- Index  (UserID,ExpiresAt)

## 6. Areas
Tổ chức • Điều chỉnh bảng gốc. Phân vùng chuỗi để giới hạn dữ liệu của quản lý khu vực.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| AreaID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Name<br>nvarchar(120) | Bắt buộc<br>UNIQUE | Tên khu vực quản lý.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Khu vực Đông |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- UNIQUE (Name)

## 7. Branches
Tổ chức • Điều chỉnh bảng gốc. Cấu hình vận hành và nhận đơn của từng quán.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| BranchID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| AreaID<br>int | Bắt buộc<br>FK → Areas.<br>AreaID | Liên kết tới Areas; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ManagerID<br>int | Được trống<br>FK → Users.<br>UserID | Quản lý chi nhánh; backend/trigger kiểm tra đúng vai trò.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 2 |
| Code<br>varchar(30) | Bắt buộc<br>UNIQUE | Mã quán ngắn dùng trên đơn và báo cáo.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: MB01 |
| Name<br>nvarchar(150) | Bắt buộc | Tên chi nhánh.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Morning Brew Thủ Đức |
| Address<br>nvarchar(500) | Bắt buộc | Địa chỉ khách đến nhận hàng.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 12 Võ Văn Ngân, TP.HCM |
| Phone<br>varchar(20) | Bắt buộc | Điện thoại quán.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 02812345678 |
| OpeningTime<br>time(0) | Bắt buộc | Giờ mở cửa hằng ngày theo giờ Việt Nam.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 07:00 |
| ClosingTime<br>time(0) | Bắt buộc | Giờ đóng cửa; bản đầu không mở xuyên nửa đêm.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 22:00 |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| AcceptingOrders<br>bit | Bắt buộc<br>Mặc định: 1 | Công tắc tạm dừng nhận đơn mới.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| PickupEnabled<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép nhận tại quán.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| DeliveryEnabled<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép giao hàng; có thể tắt để chỉ còn pickup.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| DeliveryFee<br>decimal(18,2) | Bắt buộc | Phí giao cố định của chi nhánh. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- UNIQUE (Code)
- CHECK OpeningTime < ClosingTime
- CHECK DeliveryFee >= 0

## 8. AreaManagers
Tổ chức • Điều chỉnh bảng gốc. Phân công quản lý vào nhiều khu vực.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| AreaID<br>int | Bắt buộc<br>PK ghép<br>FK → Areas.<br>AreaID | Liên kết tới Areas; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| UserID<br>int | Bắt buộc<br>PK ghép<br>FK → Users.<br>UserID | Liên kết tới Users; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| AssignedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |

## 9. StaffBranches
Tổ chức • Điều chỉnh bảng gốc. Phân công nhân viên POS; giữ bản ghi khi ngừng phân công.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| UserID<br>int | Bắt buộc<br>PK ghép<br>FK → Users.<br>UserID | Liên kết tới Users; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| BranchID<br>int | Bắt buộc<br>PK ghép<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| AssignedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |

## 10. Customers
CRM • Điều chỉnh bảng gốc. Hồ sơ CRM gắn một tài khoản Customer; tên và liên hệ đọc từ Users.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| CustomerID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| UserID<br>int | Bắt buộc<br>FK → Users.<br>UserID<br>UNIQUE | Liên kết tới Users; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 1 |
| DateOfBirth<br>date | Được trống | Tính tuổi tại thời điểm thống kê, không lưu tuổi bị cũ.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 2002-05-12 |
| Gender<br>varchar(15) | Được trống | Phân nhóm tự nguyện; NULL là chưa cung cấp.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: Female |
| LoyaltyPoints<br>int | Bắt buộc<br>Mặc định: 0 | Số dư cache đối chiếu sổ điểm; có thể âm khi thu hồi điểm đã tiêu.<br>Nguồn: Chỉ nghiệp vụ điểm cập nhật cùng transaction với LoyaltyTransactions.<br>Ví dụ: 0 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- UNIQUE (UserID)
- CHECK Gender IS NULL OR Gender IN ('Male','Female','Other','Undisclosed')
- Index  (DateOfBirth,Gender)

## 11. CustomerAddresses
CRM • Bảng bổ sung. Nhiều địa chỉ giao hàng của khách; không dùng làm địa chỉ động cho đơn cũ.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| AddressID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| CustomerID<br>int | Bắt buộc<br>FK → Customers.<br>CustomerID | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 1 |
| Label<br>nvarchar(50) | Bắt buộc | Tên gợi nhớ địa chỉ.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: Nhà |
| RecipientName<br>nvarchar(120) | Bắt buộc | Người nhận có thể khác chủ tài khoản.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: Nguyễn Lan |
| Phone<br>varchar(20) | Bắt buộc | Số điện thoại người nhận.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 0901234567 |
| AddressText<br>nvarchar(500) | Bắt buộc | Địa chỉ đầy đủ phục vụ giao thủ công.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 12 đường A, phường B, TP.HCM |
| Note<br>nvarchar(300) | Được trống | Hướng dẫn giao đến địa chỉ này.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: Gọi trước khi đến |
| IsDefault<br>bit | Bắt buộc<br>Mặc định: 0 | Địa chỉ chọn sẵn; tối đa một địa chỉ mặc định.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 0 |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 1 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- CHECK IsDefault = 0 OR IsActive = 1
- Index UNIQUE (CustomerID) WHERE IsDefault=1

## 12. Categories
Thực đơn • Điều chỉnh bảng gốc. Nhóm món để duyệt menu và phân tích sở thích.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| CategoryID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Name<br>nvarchar(100) | Bắt buộc<br>UNIQUE | Tên danh mục.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Cà phê |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- UNIQUE (Name)

## 13. Products
Thực đơn • Điều chỉnh bảng gốc. Thông tin món dùng toàn chuỗi; giá nằm ở phiên bản.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| ProductID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| CategoryID<br>int | Bắt buộc<br>FK → Categories.<br>CategoryID | Liên kết tới Categories; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Name<br>nvarchar(150) | Bắt buộc | Tên món trên menu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Cà phê sữa |
| Description<br>nvarchar(2000) | Được trống | Mô tả thành phần/hương vị.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Cà phê và sữa đặc |
| ImageURL<br>nvarchar(1000) | Được trống | Đường dẫn ảnh; không lưu ảnh nhị phân trong bảng.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: /images/coffee.jpg |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |

## 14. ProductVariants
Thực đơn • Bảng bổ sung. Mỗi size là một phiên bản có giá cơ sở; món không size có phiên bản Standard.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| VariantID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| ProductID<br>int | Bắt buộc<br>FK → Products.<br>ProductID<br>UNIQUE ghép | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Name<br>nvarchar(50) | Bắt buộc<br>UNIQUE ghép | Tên size/phiên bản.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: M |
| BasePrice<br>decimal(18,2) | Bắt buộc | Giá dùng khi chi nhánh chưa ghi đè. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| DisplayOrder<br>int | Bắt buộc<br>Mặc định: 0 | Thứ tự hiển thị size.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- UNIQUE (ProductID, Name)
- UNIQUE (VariantID, ProductID)
- CHECK BasePrice >= 0
- CHECK DisplayOrder >= 0

## 15. BranchProducts
Thực đơn • Điều chỉnh bảng gốc. Chỉ các món được phân phối tới chi nhánh mới xuất hiện trên menu.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| BranchID<br>int | Bắt buộc<br>PK ghép<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ProductID<br>int | Bắt buộc<br>PK ghép<br>FK → Products.<br>ProductID | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| IsAvailable<br>bit | Bắt buộc<br>Mặc định: 1 | Quán bật/tắt món mà không xóa phân phối.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |

## 16. BranchProductVariants
Thực đơn • Bảng bổ sung. Ghi đè giá và tình trạng bán size tại chi nhánh.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| BranchID<br>int | Bắt buộc<br>PK ghép<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| VariantID<br>int | Bắt buộc<br>PK ghép<br>FK → ProductVariants.<br>VariantID | Liên kết tới ProductVariants; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ProductID<br>int | Bắt buộc<br>FK → Products.<br>ProductID | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| SellingPrice<br>decimal(18,2) | Bắt buộc | Giá riêng tại quán. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| IsAvailable<br>bit | Bắt buộc<br>Mặc định: 1 | Tắt riêng một size.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- CHECK SellingPrice >= 0
- FK ghép (BranchID,ProductID) → BranchProducts(BranchID,ProductID).
- FK ghép (VariantID,ProductID) → ProductVariants(VariantID,ProductID).

## 17. ModifierGroups
Thực đơn • Bảng bổ sung. Định nghĩa nhóm lựa chọn đường, đá, topping.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| ModifierGroupID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Name<br>nvarchar(100) | Bắt buộc | Tên nhóm tùy chọn.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Mức đường |
| Kind<br>varchar(20) | Bắt buộc | Phân biệt Sugar/Ice chỉ chọn một; Topping có thể nhiều.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Sugar |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- CHECK Kind IN ('Sugar','Ice','Topping')

## 18. ModifierOptions
Thực đơn • Bảng bổ sung. Các lựa chọn có phụ phí trong từng nhóm.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| ModifierOptionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| ModifierGroupID<br>int | Bắt buộc<br>FK → ModifierGroups.<br>ModifierGroupID<br>UNIQUE ghép | Liên kết tới ModifierGroups; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Name<br>nvarchar(100) | Bắt buộc<br>UNIQUE ghép | Nhãn lựa chọn.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 50% đường |
| ExtraPrice<br>decimal(18,2) | Bắt buộc | Phụ phí trên một ly; đường/đá thường bằng 0. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| DisplayOrder<br>int | Bắt buộc<br>Mặc định: 0 | Thứ tự trên giao diện.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- UNIQUE (ModifierGroupID, Name)
- UNIQUE (ModifierOptionID, ModifierGroupID)
- CHECK ExtraPrice >= 0
- CHECK DisplayOrder >= 0

## 19. ProductModifierGroups
Thực đơn • Bảng bổ sung. Giới hạn số lựa chọn theo từng món; không gắn topping vô điều kiện cho mọi món.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| ProductID<br>int | Bắt buộc<br>PK ghép<br>FK → Products.<br>ProductID | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ModifierGroupID<br>int | Bắt buộc<br>PK ghép<br>FK → ModifierGroups.<br>ModifierGroupID | Liên kết tới ModifierGroups; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| MinSelections<br>int | Bắt buộc<br>Mặc định: 0 | Số lựa chọn bắt buộc; 0 nghĩa là tùy chọn.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| MaxSelections<br>int | Bắt buộc | Số lựa chọn khác nhau tối đa; mỗi topping một phần trên mỗi ly.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- CHECK MinSelections >= 0 AND MaxSelections >= MinSelections AND MaxSelections > 0

## 20. BranchModifierOptions
Thực đơn • Bảng bổ sung. Ghi đè phụ phí hoặc tạm hết topping/tùy chọn tại chi nhánh.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| BranchID<br>int | Bắt buộc<br>PK ghép<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ModifierOptionID<br>int | Bắt buộc<br>PK ghép<br>FK → ModifierOptions.<br>ModifierOptionID | Liên kết tới ModifierOptions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ExtraPrice<br>decimal(18,2) | Bắt buộc | Phụ phí riêng tại quán. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| IsAvailable<br>bit | Bắt buộc<br>Mặc định: 1 | Tạm ngừng lựa chọn tại chi nhánh.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
- CHECK ExtraPrice >= 0

## 21. CustomerPreferences
CRM • Điều chỉnh bảng gốc. Danh sách món yêu thích của khách dùng cho app và phân nhóm.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| CustomerID<br>int | Bắt buộc<br>PK ghép<br>FK → Customers.<br>CustomerID | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 1 |
| ProductID<br>int | Bắt buộc<br>PK ghép<br>FK → Products.<br>ProductID | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Khách cập nhật qua backend sau kiểm tra quyền sở hữu.<br>Ví dụ: 1 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |

## 22. LoyaltyPolicies
Điểm và ưu đãi • Bảng bổ sung. Quy tắc tính điểm có thời gian hiệu lực; chọn quy tắc tại lúc tạo đơn.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| PolicyID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Name<br>nvarchar(120) | Bắt buộc | Tên quy tắc để giải thích điểm.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Mỗi 10.000 đồng được 1 điểm |
| AmountPerPoint<br>decimal(18,2) | Bắt buộc | Tiền hàng thực trả tương ứng một điểm. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| EffectiveFrom<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| EffectiveTo<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- CHECK AmountPerPoint > 0
- CHECK EffectiveTo IS NULL OR EffectiveTo > EffectiveFrom

## 23. Promotions
Điểm và ưu đãi • Điều chỉnh bảng gốc. Chương trình phát hành voucher; không đồng nghĩa với voucher đã cấp.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| PromotionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Name<br>nvarchar(150) | Bắt buộc | Tên chương trình.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Giảm 10.000 đồng |
| Description<br>nvarchar(2000) | Được trống | Điều kiện trình bày cho khách.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Áp dụng từ 50.000 đồng |
| Scope<br>varchar(10) | Bắt buộc | Chain/Area/Branch xác định vùng được dùng voucher.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Chain |
| AreaID<br>int | Được trống<br>FK → Areas.<br>AreaID | Liên kết tới Areas; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| BranchID<br>int | Được trống<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| CreatedBy<br>int | Bắt buộc<br>FK → Users.<br>UserID | Người tạo để truy vết và kiểm soát phạm vi.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| DiscountType<br>varchar(10) | Bắt buộc | Amount giảm tiền cố định; Percent giảm phần trăm.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Amount |
| DiscountValue<br>decimal(18,2) | Bắt buộc | Giá trị giảm hoặc phần trăm. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| MaxDiscount<br>decimal(18,2) | Được trống | Trần giảm cho voucher phần trăm.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 20000 |
| MinSubtotal<br>decimal(18,2) | Bắt buộc<br>Mặc định: 0 | Tiền hàng tối thiểu trước giảm, không gồm phí giao. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 35000 |
| StartsAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| EndsAt<br>datetime2(3) | Bắt buộc | Thời điểm UTC kết thúc chương trình/khảo sát; phải sau thời điểm bắt đầu.<br>Nguồn: Quản lý thiết lập trước khi công bố/phát hành.<br>Ví dụ: 2027-09-17T03:00:00Z |
| PointsCost<br>int | Được trống | Điểm cần đổi; NULL là voucher tặng, 0 là tự nhận miễn phí.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 50 |
| TotalLimit<br>int | Bắt buộc | Tổng số voucher tối đa được cấp.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1000 |
| PerCustomerLimit<br>int | Bắt buộc<br>Mặc định: 1 | Số lần cấp tối đa cho một khách, kể cả đã dùng/hủy.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| IsActive<br>bit | Bắt buộc<br>Mặc định: 1 | Cho phép ngừng sử dụng mà không xóa lịch sử đã tham chiếu.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- CHECK Scope IN ('Chain','Area','Branch')
- CHECK (Scope='Chain' AND AreaID IS NULL AND BranchID IS NULL) OR (Scope='Area' AND AreaID IS NOT NULL AND BranchID IS NULL) OR (Scope='Branch' AND AreaID IS NULL AND BranchID IS NOT NULL)
- CHECK DiscountType IN ('Amount','Percent')
- CHECK DiscountValue > 0 AND (DiscountType <> 'Percent' OR (DiscountValue <= 100 AND MaxDiscount IS NOT NULL))
- CHECK MaxDiscount IS NULL OR MaxDiscount > 0
- CHECK MinSubtotal >= 0
- CHECK EndsAt IS NOT NULL AND EndsAt > StartsAt
- CHECK PointsCost IS NULL OR PointsCost >= 0
- CHECK TotalLimit > 0 AND PerCustomerLimit > 0 AND PerCustomerLimit <= TotalLimit

## 24. CustomerPromotions
Điểm và ưu đãi • Điều chỉnh bảng gốc. Một voucher đã cấp; giữ riêng hạn và điều kiện để không thay đổi quyền lợi sau khi cấp.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| CustomerPromotionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| CustomerID<br>int | Bắt buộc<br>FK → Customers.<br>CustomerID<br>UNIQUE ghép | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 1 |
| PromotionID<br>int | Bắt buộc<br>FK → Promotions.<br>PromotionID | Liên kết tới Promotions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 1 |
| Code<br>varchar(40) | Bắt buộc<br>UNIQUE | Mã voucher duy nhất để nhập/quét trên POS.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: MB-ABC123 |
| RequestKey<br>uniqueidentifier | Bắt buộc<br>UNIQUE | Khóa chống cấp trùng khi app gửi lại yêu cầu.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: UUID |
| Status<br>varchar(12) | Bắt buộc<br>Mặc định: 'Available' | Available/Reserved/Used; hết hạn được suy ra từ ExpiresAt.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: Available |
| IssuedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| ExpiresAt<br>datetime2(3) | Bắt buộc | Hạn sử dụng UTC bắt buộc; ngăn phiên hoặc voucher tồn tại vô thời hạn.<br>Nguồn: Backend ghi khi cấp phiên/voucher; voucher lấy hạn của chương trình.<br>Ví dụ: 2027-09-17T03:00:00Z |
| PointsSpent<br>int | Bắt buộc<br>Mặc định: 0 | Số điểm đã trả tại lần cấp, bảo toàn lịch sử khi chương trình đổi giá.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 50 |
| DiscountType<br>varchar(10) | Bắt buộc | Chụp điều kiện voucher tại lúc cấp.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: Amount |
| DiscountValue<br>decimal(18,2) | Bắt buộc | Giá trị giảm tại lúc cấp. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 35000 |
| MaxDiscount<br>decimal(18,2) | Được trống | Trần giảm tại lúc cấp.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 20000 |
| MinSubtotal<br>decimal(18,2) | Bắt buộc | Đơn tối thiểu tại lúc cấp. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 35000 |
| ReservedOrderID<br>int | Được trống<br>FK → Orders.<br>OrderID | Đơn đang giữ voucher; NULL khi chưa giữ.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 1 |
| UsedOrderID<br>int | Được trống<br>FK → Orders.<br>OrderID | Đơn tiêu thụ voucher thành công.<br>Nguồn: IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.<br>Ví dụ: 1 |
| UsedAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- UNIQUE (Code)
- UNIQUE (RequestKey)
- UNIQUE (CustomerPromotionID, CustomerID)
- CHECK Status IN ('Available','Reserved','Used')
- CHECK (Status='Available' AND ReservedOrderID IS NULL AND UsedOrderID IS NULL AND UsedAt IS NULL) OR (Status='Reserved' AND ReservedOrderID IS NOT NULL AND UsedOrderID IS NULL AND UsedAt IS NULL) OR (Status='Used' AND ReservedOrderID IS NULL AND UsedOrderID IS NOT NULL AND UsedAt IS NOT NULL)
- CHECK ExpiresAt IS NOT NULL AND ExpiresAt > IssuedAt
- CHECK PointsSpent >= 0
- CHECK DiscountType IN ('Amount','Percent')
- CHECK DiscountValue > 0 AND (DiscountType <> 'Percent' OR (DiscountValue <= 100 AND MaxDiscount IS NOT NULL))
- CHECK MaxDiscount IS NULL OR MaxDiscount > 0
- CHECK MinSubtotal >= 0
- Index  (CustomerID,Status,ExpiresAt)

## 25. Orders
Bán hàng • Điều chỉnh bảng gốc. Đơn thuộc một chi nhánh; trạng thái vận hành độc lập với thanh toán.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| OrderID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| OrderCode<br>varchar(40) | Bắt buộc<br>UNIQUE | Mã công khai trên hóa đơn; không dùng để thay kiểm tra chủ sở hữu.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: MB-12345 |
| RequestKey<br>uniqueidentifier | Bắt buộc<br>UNIQUE | Khóa yêu cầu đặt hàng để gửi lại không tạo đơn trùng.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: UUID |
| RequestHash<br>binary(32) | Bắt buộc | SHA-256 của đầu vào đặt đơn để ngăn dùng lại cùng khóa với nội dung khác.<br>Nguồn: CreateOrder tính từ payload đầu vào trước khi tạo đơn.<br>Ví dụ: 32 bytes |
| BranchID<br>int | Bắt buộc<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 1 |
| CustomerID<br>int | Được trống<br>FK → Customers.<br>CustomerID<br>UNIQUE ghép | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 1 |
| CreatedBy<br>int | Bắt buộc<br>FK → Users.<br>UserID | Tài khoản khách đặt app hoặc nhân viên lập POS.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 1 |
| Source<br>varchar(5) | Bắt buộc | App hoặc POS để phân tích kênh bán.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: App |
| Fulfillment<br>varchar(10) | Bắt buộc | Pickup hoặc Delivery; đơn tại quầy dùng Pickup.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: Pickup |
| PaymentMode<br>varchar(10) | Bắt buộc | Prepaid hoặc COD; COD pickup nghĩa là trả tại quầy.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: Prepaid |
| Status<br>varchar(20) | Bắt buộc | Trạng thái xử lý trong luồng đơn.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: AwaitingPayment |
| ContactName<br>nvarchar(120) | Bắt buộc | Tên người nhận/chủ đơn chốt tại lúc đặt.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: Nguyễn Lan |
| ContactPhone<br>varchar(20) | Được trống | Liên hệ tại thời điểm đặt; khách vãng lai pickup có thể bỏ trống.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 0901234567 |
| Note<br>nvarchar(500) | Được trống | Ghi chú chung của đơn.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: Mang đi |
| Subtotal<br>decimal(18,2) | Bắt buộc | Tổng tiền món và phụ phí trước giảm. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 35000 |
| DiscountAmount<br>decimal(18,2) | Bắt buộc<br>Mặc định: 0 | Số tiền voucher thực tế đã giảm. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 35000 |
| DeliveryFee<br>decimal(18,2) | Bắt buộc<br>Mặc định: 0 | Phí giao đã chốt; pickup bằng 0. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 35000 |
| TotalAmount<br>decimal(18,2) | Bắt buộc | Subtotal - DiscountAmount + DeliveryFee. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 35000 |
| CustomerPromotionID<br>int | Được trống<br>FK → CustomerPromotions.<br>CustomerPromotionID | Liên kết tới CustomerPromotions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 1 |
| PolicyID<br>int | Bắt buộc<br>FK → LoyaltyPolicies.<br>PolicyID | Liên kết tới LoyaltyPolicies; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 1 |
| AmountPerPoint<br>decimal(18,2) | Bắt buộc | Chụp tỷ lệ tích điểm để quy tắc tương lai không đổi điểm đơn cũ. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.<br>Ví dụ: 35000 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| PaymentDueAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| CompletedAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| CancelledAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- UNIQUE (OrderCode)
- UNIQUE (RequestKey)
- UNIQUE (OrderID, CustomerID)
- CHECK Source IN ('App','POS')
- CHECK Fulfillment IN ('Pickup','Delivery')
- CHECK PaymentMode IN ('Prepaid','COD')
- CHECK Status IN ('AwaitingPayment','Pending','Confirmed','Preparing','Ready','Delivering','Completed','Cancelled')
- CHECK Source <> 'App' OR CustomerID IS NOT NULL
- CHECK Fulfillment <> 'Delivery' OR ContactPhone IS NOT NULL
- CHECK Subtotal >= 0 AND DiscountAmount >= 0 AND DiscountAmount <= Subtotal AND DeliveryFee >= 0
- CHECK TotalAmount = Subtotal - DiscountAmount + DeliveryFee
- CHECK AmountPerPoint > 0
- CHECK Fulfillment <> 'Pickup' OR DeliveryFee = 0
- CHECK CustomerPromotionID IS NULL OR CustomerID IS NOT NULL
- FK ghép (CustomerPromotionID,CustomerID) → CustomerPromotions(CustomerPromotionID,CustomerID).
- Index  (BranchID,Status,CreatedAt)
- Index  (CustomerID,CreatedAt)

## 26. OrderDetails
Bán hàng • Điều chỉnh bảng gốc. Một dòng tương ứng món, size và một bộ tùy chọn giống nhau.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| OrderDetailID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| OrderID<br>int | Bắt buộc<br>FK → Orders.<br>OrderID | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 1 |
| ProductID<br>int | Bắt buộc<br>FK → Products.<br>ProductID | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 1 |
| VariantID<br>int | Bắt buộc<br>FK → ProductVariants.<br>VariantID | Liên kết tới ProductVariants; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 1 |
| ProductName<br>nvarchar(150) | Bắt buộc | Tên món chụp lúc mua.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: Cà phê sữa |
| VariantName<br>nvarchar(50) | Bắt buộc | Tên size chụp lúc mua.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: M |
| Quantity<br>int | Bắt buộc | Số ly cùng size và bộ tùy chọn.<br>Nguồn: Khách/nhân viên nhập lúc đặt; backend kiểm tra.<br>Ví dụ: 2 |
| UnitPrice<br>decimal(18,2) | Bắt buộc | Giá một ly chưa tính topping tại lúc mua. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 35000 |
| ModifierTotal<br>decimal(18,2) | Bắt buộc<br>Mặc định: 0 | Tổng phụ phí tùy chọn trên một ly. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 35000 |
| LineTotal<br>decimal(18,2) | Bắt buộc | (UnitPrice + ModifierTotal) × Quantity. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 35000 |
| Note<br>nvarchar(300) | Được trống | Ghi chú riêng cho dòng món.<br>Nguồn: Khách/nhân viên nhập lúc đặt; backend kiểm tra.<br>Ví dụ: Ít ngọt |
- CHECK Quantity BETWEEN 1 AND 100
- CHECK UnitPrice >= 0 AND ModifierTotal >= 0
- CHECK LineTotal = (UnitPrice + ModifierTotal) * Quantity
- FK ghép (VariantID,ProductID) → ProductVariants(VariantID,ProductID).
- Index  (OrderID)

## 27. OrderItemModifiers
Bán hàng • Bảng bổ sung. Lưu những tùy chọn đã đặt và giá chốt, không đọc giá hiện tại cho đơn cũ.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| OrderDetailID<br>int | Bắt buộc<br>PK ghép<br>FK → OrderDetails.<br>OrderDetailID | Liên kết tới OrderDetails; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 1 |
| ModifierOptionID<br>int | Bắt buộc<br>PK ghép<br>FK → ModifierOptions.<br>ModifierOptionID | Liên kết tới ModifierOptions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 1 |
| GroupName<br>nvarchar(100) | Bắt buộc | Tên nhóm chụp tại lúc đặt.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: Topping |
| OptionName<br>nvarchar(100) | Bắt buộc | Tên lựa chọn tại lúc đặt.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: Trân châu |
| ExtraPrice<br>decimal(18,2) | Bắt buộc | Phụ phí một lựa chọn trên một ly tại lúc đặt. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: CreateOrder chụp từ menu sau kiểm tra.<br>Ví dụ: 35000 |
- CHECK ExtraPrice >= 0

## 28. OrderStatusHistory
Bán hàng • Bảng bổ sung. Dòng thời gian đơn và người chịu trách nhiệm cho mỗi chuyển trạng thái.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| HistoryID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| OrderID<br>int | Bắt buộc<br>FK → Orders.<br>OrderID | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| FromStatus<br>varchar(20) | Được trống | Trạng thái trước; NULL cho sự kiện tạo đơn.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Pending |
| ToStatus<br>varchar(20) | Bắt buộc | Trạng thái sau thao tác.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Confirmed |
| ChangedBy<br>int | Được trống<br>FK → Users.<br>UserID | Người thao tác; NULL cho job hệ thống.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| Reason<br>nvarchar(500) | Được trống | Lý do hủy hoặc ghi chú chuyển trạng thái.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Khách đổi nhu cầu |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- Index  (OrderID,CreatedAt)

## 29. Deliveries
Bán hàng • Bảng bổ sung. Một đơn giao hàng có một hồ sơ giao; lưu địa chỉ bất biến và cập nhật giao thủ công.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| OrderID<br>int | Bắt buộc<br>PK<br>FK → Orders.<br>OrderID | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: 1 |
| RecipientName<br>nvarchar(120) | Bắt buộc | Người nhận chốt lúc đặt.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: Nguyễn Lan |
| Phone<br>varchar(20) | Bắt buộc | Số người nhận chốt lúc đặt.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: 0901234567 |
| AddressText<br>nvarchar(500) | Bắt buộc | Bản sao địa chỉ; sửa sổ địa chỉ không đổi đơn cũ.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: 12 đường A, TP.HCM |
| Note<br>nvarchar(300) | Được trống | Chỉ dẫn giao hàng chốt lúc đặt.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: Gọi trước |
| Status<br>varchar(12) | Bắt buộc<br>Mặc định: 'Pending' | Pending/Delivering/Failed/Delivered/Cancelled.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: Pending |
| CourierName<br>nvarchar(120) | Được trống | Tên người giao được POS nhập; không cần tài khoản tài xế.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: Anh Bình |
| CourierPhone<br>varchar(20) | Được trống | Điện thoại người giao.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: 0907654321 |
| FailureReason<br>nvarchar(500) | Được trống | Lý do giao thất bại để quyết định giao lại/hủy.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: Không liên hệ được |
| AttemptCount<br>int | Bắt buộc<br>Mặc định: 0 | Số lần bắt đầu giao, gồm giao lại.<br>Nguồn: Chụp khi đặt; trạng thái/người giao do POS cập nhật.<br>Ví dụ: 0 |
| DispatchedAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| DeliveredAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| Version<br>rowversion | Bắt buộc<br>SQL tự sinh | Dấu phiên bản để phát hiện ghi đè dữ liệu khi hai người cùng sửa; không phải thời gian.<br>Nguồn: SQL Server tự cập nhật mỗi lần ghi.<br>Ví dụ: 0x00000000000007D1 |
- CHECK Status IN ('Pending','Delivering','Failed','Delivered','Cancelled')
- CHECK AttemptCount >= 0

## 30. PaymentAttempts
Thanh toán • Bảng bổ sung. Một dòng cho mỗi lần thanh toán; hỗ trợ thử lại, COD và đối soát sau này.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| PaymentID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| OrderID<br>int | Bắt buộc<br>FK → Orders.<br>OrderID<br>UNIQUE ghép | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 1 |
| RequestKey<br>uniqueidentifier | Bắt buộc<br>UNIQUE | Khóa chống lặp một yêu cầu thanh toán.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: UUID |
| Method<br>varchar(15) | Bắt buộc | Simulator hoặc Cash trong bản thử nghiệm.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Simulator |
| Provider<br>varchar(40) | Bắt buộc | Nhà cung cấp; bản đầu Simulator/Manual.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Simulator |
| ProviderReference<br>varchar(120) | Được trống | Mã giao dịch duy nhất trong nhà cung cấp.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: SIM-123 |
| Amount<br>decimal(18,2) | Bắt buộc | Toàn bộ số tiền phải trả của đơn. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 35000 |
| Status<br>varchar(12) | Bắt buộc<br>Mặc định: 'Pending' | Pending/Succeeded/Failed.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Pending |
| FailureReason<br>nvarchar(500) | Được trống | Chi tiết thất bại phục vụ hiển thị và kiểm tra.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Người dùng hủy |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| SettledAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- UNIQUE (RequestKey)
- UNIQUE (PaymentID, OrderID)
- CHECK Method IN ('Simulator','Cash')
- CHECK Status IN ('Pending','Succeeded','Failed')
- CHECK Amount >= 0
- Index UNIQUE (OrderID) WHERE Status='Succeeded'
- Index UNIQUE (OrderID) WHERE Status='Pending'
- Index UNIQUE (Provider,ProviderReference) WHERE ProviderReference IS NOT NULL

## 31. Refunds
Thanh toán • Bảng bổ sung. Yêu cầu hoàn toàn bộ một giao dịch; trạng thái độc lập việc hủy đơn.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| RefundID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| PaymentID<br>int | Bắt buộc<br>FK → PaymentAttempts.<br>PaymentID<br>UNIQUE | Liên kết tới PaymentAttempts; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 1 |
| OrderID<br>int | Bắt buộc<br>FK → Orders.<br>OrderID | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 1 |
| RequestKey<br>uniqueidentifier | Bắt buộc<br>UNIQUE | Khóa hoàn tiền ổn định; thử lại cập nhật cùng yêu cầu.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: UUID |
| Amount<br>decimal(18,2) | Bắt buộc | Toàn bộ tiền đã thu, chưa hỗ trợ hoàn một phần. Số thập phân chính xác tránh sai số cộng tiền.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 35000 |
| Status<br>varchar(12) | Bắt buộc<br>Mặc định: 'Pending' | Pending/Succeeded/Failed.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Pending |
| Reason<br>nvarchar(500) | Bắt buộc | Căn cứ hoàn tiền.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Đơn hủy sau thanh toán |
| RequestedBy<br>int | Bắt buộc<br>FK → Users.<br>UserID | Quản lý hoặc khách hủy sớm.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 1 |
| ProviderReference<br>varchar(120) | Được trống | Mã hoàn tiền bên nhà cung cấp.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: REF-123 |
| AttemptCount<br>int | Bắt buộc<br>Mặc định: 0 | Số lần gửi xử lý hoàn tiền.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: 0 |
| FailureReason<br>nvarchar(500) | Được trống | Lỗi gần nhất nếu hoàn thất bại.<br>Nguồn: Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.<br>Ví dụ: Lỗi thử nghiệm |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| CompletedAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- UNIQUE (PaymentID)
- UNIQUE (RequestKey)
- CHECK Status IN ('Pending','Succeeded','Failed')
- CHECK Amount >= 0 AND AttemptCount >= 0
- FK ghép (PaymentID,OrderID) → PaymentAttempts(PaymentID,OrderID).

## 32. LoyaltyTransactions
Điểm và ưu đãi • Điều chỉnh bảng gốc. Sổ điểm bất biến: cộng, đổi voucher, điều chỉnh, thu hồi; dùng để đối chiếu số dư.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| LoyaltyTransactionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| CustomerID<br>int | Bắt buộc<br>FK → Customers.<br>CustomerID | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: 1 |
| OrderID<br>int | Được trống<br>FK → Orders.<br>OrderID | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: 1 |
| CustomerPromotionID<br>int | Được trống<br>FK → CustomerPromotions.<br>CustomerPromotionID | Liên kết tới CustomerPromotions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: 1 |
| Kind<br>varchar(12) | Bắt buộc | Earn/Redeem/Reverse/Adjust giải thích nguyên nhân biến động.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: Earn |
| Points<br>int | Bắt buộc | Số điểm có dấu; Redeem/Reverse âm, Earn dương.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: 5 |
| RequestKey<br>uniqueidentifier | Bắt buộc<br>UNIQUE | Khóa sự kiện tránh ghi hai lần.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: UUID |
| Note<br>nvarchar(300) | Được trống | Diễn giải cho lịch sử điểm trong app.<br>Nguồn: Procedure nghiệp vụ điểm ghi một lần trong transaction.<br>Ví dụ: Tích điểm đơn MB-123 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- UNIQUE (RequestKey)
- CHECK Kind IN ('Earn','Redeem','Reverse','Adjust')
- CHECK (Kind='Earn' AND Points>0 AND OrderID IS NOT NULL) OR (Kind='Redeem' AND Points<0 AND CustomerPromotionID IS NOT NULL) OR (Kind='Reverse' AND Points<0 AND OrderID IS NOT NULL) OR (Kind='Adjust' AND Points<>0)
- Index UNIQUE (OrderID) WHERE Kind='Earn'
- Index UNIQUE (OrderID) WHERE Kind='Reverse'
- Index UNIQUE (CustomerPromotionID) WHERE Kind='Redeem'
- Index  (CustomerID,CreatedAt)

## 33. Feedbacks
CRM • Điều chỉnh bảng gốc. Phản hồi của khách, có thể liên quan đơn/món/quán; quản lý trả lời chính thức.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| FeedbackID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| CustomerID<br>int | Bắt buộc<br>FK → Customers.<br>CustomerID | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| OrderID<br>int | Được trống<br>FK → Orders.<br>OrderID | Liên kết tới Orders; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| BranchID<br>int | Được trống<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ProductID<br>int | Được trống<br>FK → Products.<br>ProductID | Liên kết tới Products; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| Type<br>varchar(15) | Bắt buộc | Nhóm vấn đề để tổng hợp chất lượng.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Service |
| Content<br>nvarchar(4000) | Bắt buộc | Nội dung khách gửi.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Nhân viên phục vụ tốt |
| Rating<br>tinyint | Được trống | Đánh giá 1–5; có thể chỉ gửi nội dung.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 5 |
| Status<br>varchar(12) | Bắt buộc<br>Mặc định: 'New' | New/Processing/Resolved.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: New |
| HandledBy<br>int | Được trống<br>FK → Users.<br>UserID | Người phụ trách/trả lời trong phạm vi được phép.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: 1 |
| ResponseContent<br>nvarchar(4000) | Được trống | Phản hồi chính thức của quán cho khách.<br>Nguồn: Quản trị/backend ghi trong phạm vi được cấp quyền.<br>Ví dụ: Cảm ơn bạn |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| HandledAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- CHECK Type IN ('Product','Service','Space','Other')
- CHECK Rating IS NULL OR Rating BETWEEN 1 AND 5
- CHECK Status IN ('New','Processing','Resolved')
- FK ghép (OrderID,CustomerID) → Orders(OrderID,CustomerID).
- Index  (BranchID,Status,CreatedAt)

## 34. Surveys
Khảo sát • Điều chỉnh bảng gốc. Đợt khảo sát có phạm vi quản trị, thời gian mở và người tạo.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| SurveyID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| Title<br>nvarchar(250) | Bắt buộc | Tiêu đề khách nhìn thấy.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: Bạn thích món mới nào? |
| Description<br>nvarchar(2000) | Được trống | Giải thích mục đích khảo sát.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: Khảo sát tháng 9 |
| CreatedBy<br>int | Bắt buộc<br>FK → Users.<br>UserID | Người tạo có quyền quản lý khảo sát.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| Scope<br>varchar(10) | Bắt buộc | Phạm vi Chain/Area/Branch.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: Chain |
| AreaID<br>int | Được trống<br>FK → Areas.<br>AreaID | Liên kết tới Areas; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| BranchID<br>int | Được trống<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| StartsAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| EndsAt<br>datetime2(3) | Bắt buộc | Thời điểm UTC kết thúc chương trình/khảo sát; phải sau thời điểm bắt đầu.<br>Nguồn: Quản lý thiết lập trước khi công bố/phát hành.<br>Ví dụ: 2027-09-17T03:00:00Z |
| Status<br>varchar(10) | Bắt buộc<br>Mặc định: 'Draft' | Draft/Open/Closed; không mở lại khảo sát đã đóng.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: Draft |
- CHECK Scope IN ('Chain','Area','Branch')
- CHECK (Scope='Chain' AND AreaID IS NULL AND BranchID IS NULL) OR (Scope='Area' AND AreaID IS NOT NULL AND BranchID IS NULL) OR (Scope='Branch' AND AreaID IS NULL AND BranchID IS NOT NULL)
- CHECK EndsAt IS NOT NULL AND EndsAt > StartsAt
- CHECK Status IN ('Draft','Open','Closed')

## 35. SurveyQuestions
Khảo sát • Điều chỉnh bảng gốc. Câu hỏi có kiểu trả lời, thứ tự và tính bắt buộc.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| QuestionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| SurveyID<br>int | Bắt buộc<br>FK → Surveys.<br>SurveyID<br>UNIQUE ghép | Liên kết tới Surveys; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| Text<br>nvarchar(2000) | Bắt buộc | Nội dung câu hỏi.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: Bạn thích hương vị nào? |
| Type<br>varchar(15) | Bắt buộc | SingleChoice/MultipleChoice/Rating/Text.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: MultipleChoice |
| IsRequired<br>bit | Bắt buộc<br>Mặc định: 1 | Bắt buộc trả lời để hoàn tất.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| DisplayOrder<br>int | Bắt buộc<br>UNIQUE ghép | Thứ tự duy nhất trong khảo sát.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| RatingMin<br>int | Được trống | Điểm nhỏ nhất cho câu Rating.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| RatingMax<br>int | Được trống | Điểm lớn nhất cho câu Rating.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 5 |
- UNIQUE (SurveyID, DisplayOrder)
- UNIQUE (QuestionID, SurveyID)
- CHECK Type IN ('SingleChoice','MultipleChoice','Rating','Text')
- CHECK DisplayOrder >= 0
- CHECK (Type='Rating' AND RatingMin IS NOT NULL AND RatingMax IS NOT NULL AND RatingMin < RatingMax) OR (Type<>'Rating' AND RatingMin IS NULL AND RatingMax IS NULL)

## 36. SurveyQuestionOptions
Khảo sát • Bảng bổ sung. Đáp án có định danh để kiểm tra và thống kê chính xác.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| OptionID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| QuestionID<br>int | Bắt buộc<br>FK → SurveyQuestions.<br>QuestionID<br>UNIQUE ghép | Liên kết tới SurveyQuestions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
| Text<br>nvarchar(500) | Bắt buộc | Nội dung lựa chọn.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: Vị socola |
| DisplayOrder<br>int | Bắt buộc<br>UNIQUE ghép | Thứ tự đáp án trong câu hỏi.<br>Nguồn: Quản lý cấu hình khi Draft; khóa sau công bố.<br>Ví dụ: 1 |
- UNIQUE (QuestionID, DisplayOrder)
- UNIQUE (OptionID, QuestionID)
- CHECK DisplayOrder >= 0

## 37. SurveyRecipients
Khảo sát • Điều chỉnh bảng gốc. Khách thực sự được mời và tình trạng hoàn thành; không phụ thuộc bộ lọc thay đổi sau đó.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| SurveyID<br>int | Bắt buộc<br>PK ghép<br>FK → Surveys.<br>SurveyID | Liên kết tới Surveys; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| CustomerID<br>int | Bắt buộc<br>PK ghép<br>FK → Customers.<br>CustomerID | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| SentAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| CompletedAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- Index  (CustomerID,CompletedAt)

## 38. SurveyResponses
Khảo sát • Điều chỉnh bảng gốc. Một câu trả lời cho mỗi câu hỏi/khách; lựa chọn lưu ở bảng con.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| ResponseID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>UNIQUE ghép<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| SurveyID<br>int | Bắt buộc<br>FK → Surveys.<br>SurveyID | Liên kết tới Surveys; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| QuestionID<br>int | Bắt buộc<br>FK → SurveyQuestions.<br>QuestionID<br>UNIQUE ghép | Liên kết tới SurveyQuestions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| CustomerID<br>int | Bắt buộc<br>FK → Customers.<br>CustomerID<br>UNIQUE ghép | Liên kết tới Customers; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| TextValue<br>nvarchar(4000) | Được trống | Nội dung cho câu tự luận; NULL cho kiểu khác.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: Dịch vụ tốt |
| RatingValue<br>int | Được trống | Điểm cho câu Rating; NULL cho kiểu khác.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 5 |
| SubmittedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- UNIQUE (QuestionID, CustomerID)
- UNIQUE (ResponseID, QuestionID)
- FK ghép (QuestionID,SurveyID) → SurveyQuestions(QuestionID,SurveyID).
- FK ghép (SurveyID,CustomerID) → SurveyRecipients(SurveyID,CustomerID).

## 39. SurveyAnswerOptions
Khảo sát • Bảng bổ sung. Các đáp án được chọn; khóa ghép ngăn chọn lặp một đáp án.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| ResponseID<br>int | Bắt buộc<br>PK ghép<br>FK → SurveyResponses.<br>ResponseID | Liên kết tới SurveyResponses; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| OptionID<br>int | Bắt buộc<br>PK ghép<br>FK → SurveyQuestionOptions.<br>OptionID | Liên kết tới SurveyQuestionOptions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
| QuestionID<br>int | Bắt buộc<br>FK → SurveyQuestions.<br>QuestionID | Liên kết tới SurveyQuestions; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.<br>Ví dụ: 1 |
- FK ghép (ResponseID,QuestionID) → SurveyResponses(ResponseID,QuestionID).
- FK ghép (OptionID,QuestionID) → SurveyQuestionOptions(OptionID,QuestionID).

## 40. Notifications
CRM • Bảng bổ sung. Hộp thông báo trong app; chưa tích hợp push/SMS/email.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| NotificationID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| UserID<br>int | Bắt buộc<br>FK → Users.<br>UserID | Liên kết tới Users; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| Title<br>nvarchar(200) | Bắt buộc | Tiêu đề thông báo.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Đơn hàng đã sẵn sàng |
| Body<br>nvarchar(2000) | Bắt buộc | Nội dung tại thời điểm gửi.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Mời bạn đến nhận đơn |
| OrderID<br>int | Được trống<br>FK → Orders.<br>OrderID | Liên kết trực tiếp đơn nếu có.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| CustomerPromotionID<br>int | Được trống<br>FK → CustomerPromotions.<br>CustomerPromotionID | Liên kết voucher nếu có.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| SurveyID<br>int | Được trống<br>FK → Surveys.<br>SurveyID | Liên kết khảo sát nếu có.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
| ReadAt<br>datetime2(3) | Được trống | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- Index  (UserID,ReadAt,CreatedAt)

## 41. AuditLogs
Tài khoản • Bảng bổ sung. Nhật ký ai làm gì; không ghi mật khẩu, refresh token hoặc bí mật thanh toán.

| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |
|---|---|---|
| AuditID<br>int IDENTITY(1,1) | Bắt buộc<br>PK<br>Tự tăng từ 1 | Định danh ổn định để liên kết bản ghi; không dùng tên có thể thay đổi làm khóa.<br>Nguồn: SQL Server sinh khi tạo bản ghi.<br>Ví dụ: 1 |
| ActorID<br>int | Được trống<br>FK → Users.<br>UserID | Tài khoản thao tác; NULL cho hệ thống.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| BranchID<br>int | Được trống<br>FK → Branches.<br>BranchID | Liên kết tới Branches; khóa ngoại ngăn tham chiếu bản ghi không tồn tại.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| Action<br>varchar(80) | Bắt buộc | Tên hành động để tìm kiếm sự cố.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Order.Cancel |
| EntityType<br>varchar(60) | Bắt buộc | Loại đối tượng bị tác động.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Orders |
| EntityID<br>int | Được trống | Mã đối tượng; kết hợp EntityType, không phải FK đa hình.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: 1 |
| Details<br>nvarchar(4000) | Được trống | Thông tin nghiệp vụ đã loại bỏ dữ liệu bí mật.<br>Nguồn: Backend/procedure ghi khi sự kiện phát sinh.<br>Ví dụ: Khách yêu cầu hủy |
| CreatedAt<br>datetime2(3) | Bắt buộc<br>Mặc định: SYSUTCDATETIME() | Thời điểm UTC phục vụ lịch sử và đối chiếu; giao diện đổi sang giờ Việt Nam.<br>Nguồn: Backend/SQL Server ghi khi sự kiện tương ứng xảy ra.<br>Ví dụ: 2026-09-17T03:00:00Z |
- Index  (BranchID,CreatedAt)
