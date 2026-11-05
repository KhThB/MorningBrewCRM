# HƯỚNG DẪN CÀI ĐẶT, CHẠY VÀ DEMO — Morning Brew CRM

> Phiên bản: 1 cửa hàng (**Morning Brew Thủ Đức**), không có phần HRM, 30 khách hàng mẫu.
> Danh sách file đã sửa và checklist kiểm tra: xem `WALKTHROUGH.md`.

## 0. Thư mục và thành phần

| Thành phần | Thư mục (trong `D:\MorningBrew(1)\`) | Địa chỉ khi chạy |
|---|---|---|
| Database (SQL Server LocalDB) | `MorningBrewCRM-integration-crm-sqlserver\...\database` | `(localdb)\MSSQLLocalDB` |
| API | `...\MorningBrew.Api` | `https://localhost:7132`, `http://localhost:5029` |
| Web quản trị | `...\MorningBrew.Admin` | `https://localhost:7287` |
| App khách hàng (MAUI) | `MorningBrewApp\MauiApp1\MauiApp1` | Cửa sổ Windows hoặc Android Emulator |

Gọi tắt thư mục gốc solution là **`<ROOT>`**:
`D:\MorningBrew(1)\MorningBrewCRM-integration-crm-sqlserver\MorningBrewCRM-integration-crm-sqlserver`

## 1. Yêu cầu cài sẵn

- Visual Studio 2026 với workload **ASP.NET and web development** và **.NET MAUI**
- .NET SDK 10
- SQL Server LocalDB (đi kèm Visual Studio) và `sqlcmd` (chạy `sqlcmd -?` trong PowerShell để kiểm tra)

## 2. Tạo database

Database được tạo bằng script **`database\tools\Apply.ps1`**. Script này:

1. Tạo database **mới** trên LocalDB. File dữ liệu `.mdf`/`.ldf` nằm trong `<ROOT>\database\.local\` (thư mục này không đưa lên GitHub).
2. Chạy lần lượt 9 file trong `database\sql\`:

| File | Nội dung |
|---|---|
| `01_schema.sql` | Tạo bảng, khóa chính, khóa ngoại, ràng buộc |
| `02_rules.sql` | Hàm phân quyền (`CanBranch`, `CanScope`…) và trigger kiểm tra vai trò |
| `03_orders.sql` | Procedure đơn hàng, thanh toán, điểm tích lũy |
| `04_crm.sql` | Procedure phản hồi, khảo sát, ưu đãi, voucher |
| `05_access.sql` | Đăng nhập, phiên làm việc |
| `06_seed.sql` | Dữ liệu gốc: vai trò, tài khoản, 1 cửa hàng, 30 khách, thực đơn, 2 khảo sát |
| `07_demo.sql` | Dữ liệu giao dịch: đơn, phản hồi, câu trả lời khảo sát |
| `08_admin.sql` | Bảng Nhà cung cấp, quyền cho web quản trị, SQL role `MorningBrewApi` |
| `09_shared_prices.sql` | Giá dùng chung cho thực đơn |

3. **Không bao giờ ghi đè** database đã có: nếu trùng tên, script dừng ngay.

### 2.1. Kiểm tra công cụ (làm 1 lần)

Mở **PowerShell** (không cần quyền Administrator) và chạy từng lệnh:

```powershell
sqllocaldb info MSSQLLocalDB
```

- Kết quả có `Version: 16.x` trở lên (SQL Server 2022) và `State: Running` là được. Schema dùng mức tương thích 160, **cần LocalDB 2022**.
- Nếu `State: Stopped`, chạy `sqllocaldb start MSSQLLocalDB`.
- Nếu báo không tìm thấy `sqllocaldb` hoặc version 15.x trở xuống: mở **Visual Studio Installer** → Modify → Individual components → tick **SQL Server Express 2022 LocalDB**.

```powershell
sqlcmd -?
```

- Phải thấy tùy chọn **`-C`** (Trust server certificate) trong danh sách. Có nghĩa là sqlcmd bản 18 trở lên.
- Nếu báo không nhận lệnh `sqlcmd`, hoặc báo `Unknown option -C`: cài bản mới bằng `winget install sqlcmd`, **đóng rồi mở lại PowerShell**, chạy lại `sqlcmd -?`.

Kiểm tra kết nối:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "SELECT @@VERSION"
```

Ra dòng `Microsoft SQL Server 2022 ...` là đủ điều kiện.

### 2.2. Xem database đang có

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "SELECT name FROM sys.databases WHERE database_id > 4"
```

Nếu đã có `MorningBrewCRM` (bản cũ, 3 chi nhánh) thì **giữ nguyên**, ta tạo database mới tên `MorningBrewCRM_Crm` bên cạnh. Nếu đã có `MorningBrewCRM_Crm` từ lần thử trước, xem mục 2.6 để xóa.

### 2.3. Chạy script tạo database

```powershell
cd "D:\MorningBrew(1)\MorningBrewCRM-integration-crm-sqlserver\MorningBrewCRM-integration-crm-sqlserver"
Set-ExecutionPolicy -Scope Process Bypass -Force
.\database\tools\Apply.ps1 -Database MorningBrewCRM_Crm
```

- `Set-ExecutionPolicy -Scope Process` chỉ có hiệu lực trong cửa sổ PowerShell đang mở, không thay đổi cài đặt máy.
- Tham số tùy chọn:
  - `-Server "tên_server"`: dùng SQL Server khác thay cho LocalDB (mặc định `(localdb)\MSSQLLocalDB`).
  - `-NoSeed`: chỉ tạo cấu trúc, **không** có dữ liệu mẫu (bỏ `06`, `07`). Không dùng khi demo.

Màn hình chạy đúng sẽ như sau (mất khoảng 10–30 giây):

```
Applying 01_schema.sql
Applying 02_rules.sql
Applying 03_orders.sql
Applying 04_crm.sql
Applying 05_access.sql
Applying 06_seed.sql
Applying 07_demo.sql
Customers   CompletedOrders Feedbacks   SurveyAnswers
----------- --------------- ----------- -------------
         30              24          18            41
Applying 08_admin.sql
Applying 09_shared_prices.sql
TableCount
-----------
        ...
Database ready: MorningBrewCRM_Crm on (localdb)\MSSQLLocalDB
```

Dòng **30 · 24 · 18 · 41** là kết quả của `07_demo.sql`. Khác số này nghĩa là dữ liệu mẫu chưa đúng: chụp lại màn hình gửi nhóm.

### 2.4. Kiểm tra database sau khi tạo

Chạy nhanh trong PowerShell:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -Q "SELECT (SELECT COUNT(*) FROM dbo.Branches) AS CuaHang, (SELECT COUNT(*) FROM dbo.Customers) AS Khach, (SELECT COUNT(*) FROM dbo.Surveys) AS KhaoSat, (SELECT COUNT(*) FROM dbo.Users) AS TaiKhoan"
```

Kết quả mong đợi: **CuaHang 1 · Khach 30 · KhaoSat 2 · TaiKhoan 33**.

Xem bằng giao diện (tùy chọn):

- **Visual Studio**: View → **SQL Server Object Explorer** → `(localdb)\MSSQLLocalDB` → Databases → `MorningBrewCRM_Crm` → Tables. Chuột phải bảng → View Data.
- **SSMS**: Server name `(localdb)\MSSQLLocalDB`, Authentication **Windows Authentication**, tick **Trust server certificate**.

Bộ câu SQL kiểm tra đầy đủ (nhóm tuổi, món được thích, trạng thái phản hồi) nằm ở mục 6B của `WALKTHROUGH.md`.

### 2.5. Lỗi thường gặp khi tạo database

| Thông báo | Nguyên nhân | Cách xử lý |
|---|---|---|
| `...cannot be loaded because running scripts is disabled...` | Windows chặn chạy script `.ps1` | Chạy `Set-ExecutionPolicy -Scope Process Bypass -Force` rồi chạy lại |
| `Database already exists. Choose a new name...` | Đã có database trùng tên | Đổi tên (`-Database MorningBrewCRM_Crm2`) hoặc xóa theo mục 2.6 |
| `Database creation refused or failed.` (kèm lỗi `Cannot create file ... .mdf`) | File `.mdf` cũ còn sót trong `database\.local\` dù database đã bị xóa | Xóa 2 file `MorningBrewCRM_Crm.mdf` và `MorningBrewCRM_Crm_log.ldf` trong `database\.local\` rồi chạy lại |
| `A network-related or instance-specific error...` | LocalDB chưa chạy hoặc chưa cài | `sqllocaldb start MSSQLLocalDB`; nếu vẫn lỗi, xem mục 2.1 |
| `Unknown option -C` / `'sqlcmd' is not recognized` | sqlcmd cũ hoặc chưa cài | `winget install sqlcmd`, mở lại PowerShell |
| `Failed: 0X_xxx.sql. Existing data was not deleted...` | Lỗi trong một file SQL. Database đã được tạo nhưng **chỉ có một phần** | Kéo lên trên tìm dòng `Msg ..., Level ..., Line ...` → chụp lại gửi nhóm. Xóa database dở dang theo mục 2.6 trước khi chạy lại |
| Chữ tiếng Việt trong dữ liệu bị lỗi font (`Cà phÃª`) | File SQL bị lưu sai mã hóa | Mở file bằng VS Code → góc dưới phải phải là **UTF-8 with BOM**; script đã chạy với `-f 65001` (UTF-8) |

### 2.6. Xóa database để tạo lại

Dừng API trước (database đang được API dùng thì không xóa được), rồi chạy:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "ALTER DATABASE [MorningBrewCRM_Crm] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [MorningBrewCRM_Crm]"
```

- Lệnh này xóa luôn file `.mdf`/`.ldf`. Nếu vẫn còn file trong `database\.local\` thì xóa tay.
- Sau đó chạy lại mục 2.3.
- **Không** xóa database `MorningBrewCRM` cũ nếu nhóm còn cần dữ liệu test trong đó.

### 2.7. Dùng lại tên `MorningBrewCRM` (không bắt buộc)

Nếu muốn giữ connection string cũ, không phải sửa mục 3:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "ALTER DATABASE [MorningBrewCRM] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [MorningBrewCRM]"
.\database\tools\Apply.ps1
```

Cách này **mất toàn bộ dữ liệu** trong database cũ.

## 3. Trỏ API vào database mới

Mở `<ROOT>\MorningBrew.Api\appsettings.Development.json`, đổi tên database:

```json
"MorningBrew": "Server=(localdb)\\MSSQLLocalDB;Database=MorningBrewCRM_Crm;Integrated Security=true;TrustServerCertificate=true"
```

## 4. Chạy API và web quản trị

1. Mở `<ROOT>\MorningBrewCRM.slnx` bằng Visual Studio.
2. Chuột phải Solution → **Configure Startup Projects…** → **Multiple startup projects**:
   - `MorningBrew.Api`: **Start**, profile **https**
   - `MorningBrew.Admin`: **Start**, profile **https**
   - `MorningBrew.Pos`: None
3. Nhấn **F5**. Lần đầu Visual Studio hỏi tin cậy chứng chỉ HTTPS → chọn **Yes**.
4. Kiểm tra:
   - `https://localhost:7132/health` → trả `ready`
   - `https://localhost:7132/swagger` → danh sách API (không còn `/v1/shifts`, `/v1/staff`)
   - `https://localhost:7287` → trang đăng nhập quản trị

> Web quản trị gọi API qua `https://localhost:7132`, app khách gọi qua `http://localhost:5029`. Profile **https** của API mở cả hai cổng, nên phải dùng profile này.

Chạy bằng dòng lệnh (thay cho bước 1–3), mở 2 cửa sổ PowerShell:

```powershell
cd "<ROOT>"; dotnet run --project MorningBrew.Api --launch-profile https
cd "<ROOT>"; dotnet run --project MorningBrew.Admin --launch-profile https
```

## 5. Chạy app khách hàng

1. **Giữ API đang chạy.**
2. Mở `D:\MorningBrew(1)\MorningBrewApp\MauiApp1\MauiApp1.slnx` bằng một cửa sổ Visual Studio khác.
3. Chọn target **Windows Machine** (dễ demo nhất) rồi nhấn F5. Cửa sổ mở ở kích thước điện thoại.
   - Muốn dùng **Android Emulator**: app tự gọi `http://10.0.2.2:5029`, không cần sửa gì.
4. Đổi địa chỉ API (nếu cần) tại `Services/ApiClient.cs` → `ApiSettings.BaseUrl`.

## 6. Tài khoản demo

Mật khẩu chung: **`MorningBrew!2026`** (chỉ dùng cho demo, không đưa vào báo cáo công khai).

| Email | Vai trò | Dùng ở đâu |
|---|---|---|
| `owner@morningbrew.test` | Chủ cửa hàng | Web quản trị — toàn quyền, báo cáo |
| `manager.1@morningbrew.test` | Quản lý cửa hàng | Web quản trị — xử lý phản hồi, khảo sát, ưu đãi |
| `staff.1@morningbrew.test` | Thu ngân | Chuyển trạng thái đơn (POS) |
| `lan@morningbrew.test` | Khách hàng (100 điểm) | App — **tài khoản demo chính**, còn 1 khảo sát chưa làm |
| `minh@morningbrew.test` | Khách hàng (100 điểm) | App — tài khoản phụ |
| `khach01` … `khach28@morningbrew.test` | Khách hàng (50 điểm) | Dữ liệu nền cho báo cáo và khảo sát |

## 7. Kịch bản demo cho giảng viên (khoảng 10 phút)

Mục tiêu: đi trọn một vòng CRM **Khách mua hàng → Phản hồi → Quản lý xử lý → Khảo sát → Phân tích → Ưu đãi lại cho khách**.

### Bước 1 — Khách hàng (app, đăng nhập `lan@`)

1. **Trang chủ**: thẻ thành viên (điểm, hạng), banner khảo sát mới, gợi ý món.
2. **Thực đơn**: tên cửa hàng Morning Brew Thủ Đức, lọc danh mục, bấm ♡ một món (lưu **sở thích khách hàng**).
3. Thêm món vào giỏ → **Giỏ hàng** → chọn voucher → **Đặt hàng**.
4. **Đơn hàng**: mở đơn vừa đặt, xem tiến trình.

> **Lưu ý:** API hiện **chưa có** endpoint đơn hàng (`/v1/me/orders`), nên đơn đặt từ app là **đơn mẫu** (nhãn "Dữ liệu mẫu"), tự chuyển trạng thái mỗi 6 giây tới **Hoàn tất**. Đơn và điểm thật của 30 khách đã có sẵn trong database (24 đơn hoàn tất). Muốn đặt đơn thật thì cần làm thêm API theo `MorningBrewApp/API_CAN_BO_SUNG.md`.

### Bước 2 — Chờ đơn hoàn tất

5. Đợi khoảng 30 giây cho đơn mẫu chuyển sang **Hoàn tất** (nút **Đánh giá đơn** hiện ra).

### Bước 3 — Phản hồi (app `lan@`)

6. Mở đơn đã hoàn tất → **Đánh giá đơn**: chọn sao, chọn món, viết nội dung → Gửi. Phản hồi được lưu **thật** vào database qua `POST /v1/me/feedbacks`.
7. **Tài khoản → Phản hồi**: thấy phản hồi trạng thái *Mới*.

### Bước 4 — Quản lý xử lý phản hồi (web quản trị `manager.1@`)

8. **Phản hồi**: thấy phản hồi của Lan cùng 18 phản hồi mẫu (*Mới / Đang xử lý / Đã xử lý*).
9. Mở phản hồi → **Trả lời**: chuyển **Đang xử lý**, rồi **Đã xử lý** (bắt buộc nhập câu trả lời).
10. Quay lại app: phản hồi đã có trạng thái và câu trả lời của cửa hàng.

### Bước 5 — Khảo sát (app `lan@` + web quản trị)

11. App: **Tài khoản → Khảo sát** (tab có badge 1) → làm "Thăm dò món mới mùa đông" → Gửi.
12. Web quản trị → **Khảo sát** → chọn khảo sát đó → **Xem kết quả**: khoảng 20 người trả lời, Cacao muối dẫn đầu, có câu trả lời vừa gửi của Lan.
13. (Tùy chọn) **Tạo khảo sát mới** → Phát hành → Mời khách → khách thấy ngay trong app.

### Bước 6 — Phân tích và chăm sóc lại

14. **Tổng quan / Khách hàng**: 30 khách, xem hồ sơ, điểm tích lũy; có thể cộng/trừ điểm thủ công.
15. **Báo cáo**: doanh thu từ 24 đơn mẫu.
16. **Ưu đãi**: tạo chương trình → phát voucher cho khách → khách thấy ở tab **Ưu đãi** trong app.
17. (Tùy chọn, đăng nhập `owner@`) **Tài khoản** → khóa tài khoản một khách → app của khách đó bị đăng xuất (thể hiện quản lý tài khoản: khóa chứ không xóa dữ liệu).

## 8. Lỗi thường gặp

| Lỗi | Nguyên nhân | Cách xử lý |
|---|---|---|
| `Cannot open database "..." requested by the login` | Chưa tạo database hoặc sai tên trong connection string | Làm lại bước 2 và 3 |
| App báo "Không kết nối được máy chủ" | API chưa chạy hoặc chạy sai profile | Chạy API profile **https** (mở cả cổng 5029) |
| Web quản trị báo lỗi kết nối API | API không chạy ở `https://localhost:7132` | Như trên; tin cậy chứng chỉ dev: `dotnet dev-certs https --trust` |
| `Scope denied` khi tạo khảo sát hoặc ưu đãi | Database hoặc code cũ | Dùng database mới và bản code đã sửa |
| App hiện nhãn "Dữ liệu mẫu" | API chưa có endpoint thực đơn/đơn hàng (`/v1/me/menu`, `/v1/me/orders`) | Xem `MorningBrewApp/API_CAN_BO_SUNG.md` |
| App Android không gọi được API | Emulator không hiểu `localhost` | App đã dùng `10.0.2.2`; kiểm tra tường lửa Windows cho cổng 5029 |
| Đăng nhập app bằng `owner@` bị từ chối | App chỉ dành cho khách hàng | Dùng web quản trị cho tài khoản quản lý |

## 9. Không nên làm

- Không chạy `database/tools/seed.py` (sẽ ghi đè `06_seed.sql` mới).
- Không dùng `tests/integration.py` và `database/tests/verify.py` để đánh giá: hai script này còn viết theo bản 3 chi nhánh.
- Không commit file chứa mật khẩu thật hay danh sách tài khoản lên GitHub.
