# Morning Brew CRM

Hệ thống thông tin quản lý quán cà phê kết hợp CRM — đồ án HTTTDN.
Bản demo hiện tại: **1 cửa hàng (Morning Brew Thủ Đức), 30 khách hàng mẫu, không có phần nhân sự (HRM)**.

---

## Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Cấu trúc thư mục](#2-cấu-trúc-thư-mục)
3. [Cài đặt công cụ](#3-cài-đặt-công-cụ)
4. [Chạy project từng bước](#4-chạy-project-từng-bước)
5. [Tài khoản demo](#5-tài-khoản-demo)
6. [Kịch bản demo](#6-kịch-bản-demo)
7. [Lỗi thường gặp](#7-lỗi-thường-gặp)
8. [Giới hạn hiện tại](#8-giới-hạn-hiện-tại)
9. [Tài liệu liên quan](#9-tài-liệu-liên-quan)

---

## 1. Kiến trúc tổng quan

```
 ┌───────────────────────┐      ┌──────────────────────────┐
 │ Web quản trị          │      │ App khách hàng (MAUI)    │
 │ MorningBrew.Admin     │      │ MorningBrewApp           │
 │ Razor Pages, :5191    │      │ Windows / Android        │
 └──────────┬────────────┘      └────────────┬─────────────┘
            │  HTTP + Bearer token           │
            └───────────────┬────────────────┘
                 ┌──────────▼───────────┐
                 │ MorningBrew.Api /v1  │  kiểm tra vai trò + phạm vi
                 │ :5029 (http)         │  Swagger: /swagger
                 └──────────┬───────────┘
                            │ SqlClient + stored procedure
                 ┌──────────▼───────────┐
                 │ SQL Server LocalDB   │  database MorningBrewCRM_Crm
                 └──────────────────────┘
```

| Thành phần | Thư mục | Công nghệ | Địa chỉ khi chạy |
|---|---|---|---|
| Database | `database/` | SQL Server 2022 / LocalDB | `(localdb)\MSSQLLocalDB` |
| API | `MorningBrew.Api/` | ASP.NET Core .NET 10 | http://localhost:5029 · https://localhost:7132 |
| Web quản trị + cổng khách | `MorningBrew.Admin/` | Razor Pages | http://localhost:5191 · https://localhost:7287 |
| App khách hàng | `MorningBrewApp/MauiApp1/` | .NET MAUI Blazor Hybrid | Cửa sổ Windows / Android Emulator |

Nguyên tắc: Admin và App **không kết nối thẳng database**, mọi thao tác đi qua API. API là nơi duy nhất kiểm tra quyền.

---

## 2. Cấu trúc thư mục

```
MorningBrewCRM/
├─ MorningBrewCRM.slnx            Solution API + Admin (mở bằng Visual Studio)
├─ README.md                      File này
│
├─ MorningBrew.Api/               ── BACKEND
│  ├─ Controllers/
│  │  ├─ AuthController.cs        Đăng ký khách, đăng nhập, đăng xuất, /auth/me
│  │  ├─ AdminController.cs       Danh sách khách, phản hồi, sản phẩm, tài khoản, dashboard, báo cáo
│  │  ├─ AdminCrm.cs              Khảo sát, ưu đãi, điều chỉnh điểm
│  │  ├─ AdminCrmReports.cs       Báo cáo khách hàng (tuổi, giới tính, sở thích), thống kê khảo sát, xóa mềm
│  │  ├─ AdminOperations.cs       Nhà cung cấp, phân quyền
│  │  ├─ AdminResources.cs        Đọc chi tiết 1 bản ghi
│  │  ├─ CustomerController.cs    /v1/me/... : hồ sơ, điểm, voucher, phản hồi, khảo sát, sở thích
│  │  └─ VouchersController.cs    Tra cứu voucher
│  ├─ Infrastructure/             SqlStore (gọi SQL), phân trang, xác thực phiên, chuẩn lỗi
│  ├─ Program.cs                  Cấu hình DI, Swagger, rate limit
│  ├─ appsettings.Development.json  ⚙ Connection string database
│  └─ MorningBrew.Api.http        Mẫu request để test nhanh trong Visual Studio
│
├─ MorningBrew.Admin/             ── WEB QUẢN TRỊ
│  ├─ Pages/
│  │  ├─ Login, Logout, AccessDenied, Error
│  │  ├─ Index                    Tổng quan (dashboard)
│  │  ├─ Workspace                Mọi màn hình quản lý: /Workspace/{customers|feedbacks|surveys|...}
│  │  ├─ Portal, Answer           Cổng khách hàng trên web + trả lời khảo sát
│  │  └─ Shared/                  _Layout (menu), _Rows (bảng), _Scope (phạm vi áp dụng),
│  │                               _CustomerReport (báo cáo khách), _SurveySummary (thống kê khảo sát)
│  ├─ Services/CrmApi.cs          HttpClient gọi API
│  ├─ Services/RowDisplay.cs      Nhãn tiếng Việt cho cột / trạng thái
│  ├─ Services/ReportView.cs      Tính tỷ lệ %, vẽ thanh tỷ lệ cho báo cáo
│  └─ wwwroot/css/site.css, js/ui.js
│
├─ MorningBrewApp/                ── APP KHÁCH HÀNG (solution riêng)
│  ├─ MauiApp1/MauiApp1.slnx      Mở file này để chạy app
│  ├─ MauiApp1/MauiApp1/
│  │  ├─ Components/Pages/        Home, MenuPage, Cart, Orders, Rewards, Account, Surveys, ...
│  │  ├─ Components/Shared/       MemberCard, StarRating, Toast, ...
│  │  └─ Services/                ApiClient (⚙ địa chỉ API), AuthService, CrmService, ShopService
│  └─ API_CAN_BO_SUNG.md          Các API app cần mà backend chưa có
│
├─ Shared/ApiJson.cs              Quy ước JSON snake_case dùng chung API + Admin
│
├─ database/                      ── CƠ SỞ DỮ LIỆU
│  ├─ sql/01 … 10_*.sql           Script tạo bảng, rule, procedure, seed (chạy theo thứ tự)
│  ├─ tools/Apply.ps1             ⭐ Script tạo database
│  ├─ docs/                       ERD, từ điển dữ liệu, báo cáo Word các tuần
│  └─ README.md                   Mô tả chi tiết thiết kế CSDL
│
├─ docs/                          Đặc tả API v1 (api-v1.md), lịch sử tích hợp
└─ tests/                         Script test Python (⚠ còn viết theo bản nhiều chi nhánh)
```

### Thứ tự file SQL

| File | Nội dung |
|---|---|
| `01_schema.sql` | Bảng, khóa, ràng buộc |
| `02_rules.sql` | Hàm phân quyền `CanBranch`, `CanScope`, trigger vai trò |
| `03_orders.sql` | Procedure đơn hàng, thanh toán, tích điểm |
| `04_crm.sql` | Procedure phản hồi, khảo sát, ưu đãi, voucher |
| `05_access.sql` | Đăng nhập, phiên |
| `06_seed.sql` | Vai trò, tài khoản, 1 cửa hàng, 30 khách, thực đơn 10 món, 2 khảo sát |
| `07_demo.sql` | Đơn hàng, phản hồi, câu trả lời khảo sát mẫu |
| `08_admin.sql` | Bảng nhà cung cấp, quyền web quản trị, SQL role |
| `09_shared_prices.sql` | Giá chung cho thực đơn |
| `10_soft_delete.sql` | Cột `Users.DeletedAt` + procedure `SoftDeleteUser` (xóa mềm tài khoản / khách hàng) |

---

## 3. Cài đặt công cụ

| Công cụ | Bắt buộc | Kiểm tra |
|---|---|---|
| .NET SDK 10 | ✅ | `dotnet --version` |
| Visual Studio 2026: workload **ASP.NET and web development** | ✅ | |
| SQL Server 2022 LocalDB (Visual Studio Installer → Individual components) | ✅ | `sqllocaldb info MSSQLLocalDB` → Version 16.x |
| `sqlcmd` bản mới (có tùy chọn `-C`) | ✅ | `sqlcmd -?` · cài: `winget install sqlcmd` |
| Workload **.NET MAUI** | Chỉ khi chạy app khách | |

Kiểm tra kết nối SQL:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "SELECT @@VERSION"
```

---

## 4. Chạy project từng bước

> Mọi lệnh chạy trong **PowerShell**, tại thư mục gốc project (thư mục chứa `MorningBrewCRM.slnx`).

### Bước 1 — Tạo database (làm 1 lần)

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\database\tools\Apply.ps1 -Database MorningBrewCRM_Crm
```

- Script tạo database **mới** và chạy SQL 01 → 10. Nếu tên đã tồn tại, script **dừng, không ghi đè**.
- Chạy đúng sẽ in dòng tổng kết `30 · 24 · 18 · 41` (khách · đơn hoàn tất · phản hồi · câu trả lời khảo sát) và `Database ready`.
- Kiểm tra lại:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -Q "SELECT (SELECT COUNT(*) FROM dbo.Branches) CuaHang,(SELECT COUNT(*) FROM dbo.Customers) Khach,(SELECT COUNT(*) FROM dbo.Surveys) KhaoSat"
```

Kết quả mong đợi: **CuaHang 1 · Khach 30 · KhaoSat 2**.

**Đã có database từ trước** (tạo trước khi có file 10): chỉ cần chạy bổ sung, không mất dữ liệu:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i .\database\sql\10_soft_delete.sql
```

Muốn tạo lại từ đầu: dừng API, rồi

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "ALTER DATABASE [MorningBrewCRM_Crm] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [MorningBrewCRM_Crm]"
```

### Bước 2 — Kiểm tra cấu hình

| Cấu hình | File | Giá trị mặc định |
|---|---|---|
| Database API dùng | `MorningBrew.Api/appsettings.Development.json` | `Database=MorningBrewCRM_Crm` |
| Admin gọi API ở đâu | biến môi trường `Api__BaseUrl` | mặc định `https://localhost:7132/` |
| App gọi API ở đâu | `MorningBrewApp/MauiApp1/MauiApp1/Services/ApiClient.cs` | Windows `http://localhost:5029/`, Android `http://10.0.2.2:5029/` |

> ⚠ **Lưu ý solution:** `MorningBrewCRM.slnx` còn dòng `MorningBrew.Pos/MorningBrew.Pos.csproj` nhưng thư mục POS **không có** trong bản này → `dotnet build` báo lỗi thiếu project. Xóa dòng đó trong `MorningBrewCRM.slnx` trước khi build.

### Bước 3 — Chạy API (cửa sổ PowerShell thứ nhất)

```powershell
dotnet run --project .\MorningBrew.Api --launch-profile http
```

Mở thử:
- http://localhost:5029/health → `{"status":"ready"}`
- http://localhost:5029/swagger → danh sách API

### Bước 4 — Chạy web quản trị (cửa sổ PowerShell thứ hai)

```powershell
$env:Api__BaseUrl = 'http://localhost:5029/'
dotnet run --project .\MorningBrew.Admin --launch-profile http
```

Mở http://localhost:5191 và đăng nhập bằng tài khoản ở mục 5.

**Chạy bằng Visual Studio thay cho bước 3–4:** mở `MorningBrewCRM.slnx` → chuột phải Solution → *Configure Startup Projects* → *Multiple startup projects* → `MorningBrew.Api` và `MorningBrew.Admin` đều **Start**, profile **https** → F5. Khi đó API ở `https://localhost:7132`, Admin ở `https://localhost:7287` (lần đầu chọn *Yes* để tin cậy chứng chỉ).

### Bước 5 — Chạy app khách hàng (tùy chọn)

1. **Giữ API đang chạy** (cổng 5029).
2. Mở `MorningBrewApp/MauiApp1/MauiApp1.slnx` bằng một cửa sổ Visual Studio khác.
3. Chọn target **Windows Machine** → F5 (hoặc Android Emulator).
4. Đăng nhập `lan@morningbrew.test`.

---

## 5. Tài khoản demo

Mật khẩu chung: **`MorningBrew!2026`** (chỉ dùng cho demo).

| Email | Vai trò | Dùng ở |
|---|---|---|
| `owner@morningbrew.test` | Chủ cửa hàng — toàn quyền | Web quản trị |
| `manager.1@morningbrew.test` | Quản lý cửa hàng | Web quản trị |
| `staff.1@morningbrew.test` | Thu ngân (POS chưa có giao diện, bị chặn khỏi web) | — |
| `lan@morningbrew.test` | Khách hàng — tài khoản demo chính | App / Cổng khách trên web |
| `minh@morningbrew.test` | Khách hàng phụ | App / Cổng khách |
| `khach01` … `khach28@morningbrew.test` | Khách nền cho báo cáo, khảo sát | — |

---

## 6. Kịch bản demo

Vòng CRM: **Khách mua → Phản hồi → Quản lý xử lý → Khảo sát → Phân tích → Ưu đãi lại**.

| # | Ai | Ở đâu | Thao tác |
|---|---|---|---|
| 1 | `lan@` | App | Trang chủ (thẻ thành viên) → Thực đơn → ♡ một món (lưu sở thích) |
| 2 | `lan@` | App | Thêm món → Giỏ → Đặt hàng (*đơn mẫu*, tự hoàn tất sau ~30 giây) |
| 3 | `lan@` | App | Mở đơn đã hoàn tất → **Đánh giá đơn** (lưu thật vào DB) |
| 4 | `manager.1@` | Web | Phản hồi → chọn phản hồi của Lan → *Đang xử lý* / *Đã giải quyết* + nội dung trả lời |
| 5 | `lan@` | App | Tài khoản → Phản hồi: thấy trả lời của cửa hàng |
| 6 | `lan@` | App | Tài khoản → Khảo sát → làm "Thăm dò món mới mùa đông" |
| 7 | `manager.1@` | Web | Khảo sát → chọn khảo sát → **Xem kết quả** |
| 8 | `owner@` | Web | Ưu đãi → tạo chương trình → **Cấp voucher** cho Lan → Lan thấy trong app |
| 9 | `owner@` | Web | Tài khoản → **Khóa** một khách → app khách đó bị đăng xuất |
| 10 | `owner@` | Web | **Báo cáo**: tỷ lệ độ tuổi, giới tính, món được yêu thích, món thích nhất theo nhóm tuổi |
| 11 | `manager.1@` | Web | Khảo sát → **Xem kết quả**: điểm trung bình, tỷ lệ % từng lựa chọn, tỷ lệ hoàn thành |
| 12 | Khách mới | App | **Đăng ký** tài khoản → tự đăng nhập |
| 13 | `owner@` | Web | Khách hàng → **Xóa khách hàng** (xóa mềm) → báo cáo doanh thu, khảo sát vẫn giữ số liệu |

---

## 7. Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| `running scripts is disabled` khi chạy `Apply.ps1` | `Set-ExecutionPolicy -Scope Process Bypass -Force` |
| `Database already exists` | Dùng tên khác (`-Database MorningBrewCRM_Crm2`, nhớ sửa appsettings) hoặc xóa DB như bước 1 |
| `Cannot create file ... .mdf` | Xóa file `.mdf` / `.ldf` cũ trong `database\.local\` |
| `Unknown option -C` / không nhận `sqlcmd` | `winget install sqlcmd`, mở lại PowerShell |
| `dotnet build` báo không tìm thấy `MorningBrew.Pos.csproj` | Xóa dòng POS trong `MorningBrewCRM.slnx` |
| `Cannot open database ... requested by the login` | Chưa tạo DB hoặc sai tên trong `appsettings.Development.json` |
| Web báo "Không kết nối được API" | API chưa chạy, hoặc thiếu `$env:Api__BaseUrl` khi chạy profile http |
| App báo "Không kết nối được máy chủ" | API phải chạy ở cổng 5029 |
| App hiện nhãn "Dữ liệu mẫu" | Bình thường: API thực đơn / đơn hàng chưa có (xem mục 8) |
| Đăng nhập app bằng `owner@` bị từ chối | App chỉ dành cho khách hàng |

---

## 8. Giới hạn hiện tại

| Hạng mục | Tình trạng |
|---|---|
| Đăng ký khách (`POST /v1/auth/register`) | ✅ App dùng được; web chưa có trang đăng ký |
| Xóa khách hàng / xóa account | ✅ Xóa mềm (chỉ Owner): khóa đăng nhập, ẩn danh thông tin cá nhân, giữ dữ liệu giao dịch |
| Thực đơn / đặt hàng từ app | ❌ App dùng dữ liệu mẫu; procedure SQL đã có, chưa có endpoint |
| POS cho thu ngân | ❌ Chưa có giao diện |
| Báo cáo khách hàng | ✅ Tỷ lệ độ tuổi, giới tính, sở thích (món, danh mục, theo nhóm tuổi) + doanh thu 30 ngày |
| Thống kê khảo sát | ✅ Điểm trung bình, phân bố điểm, tỷ lệ từng lựa chọn, tỷ lệ hoàn thành |
| Tạo khảo sát trên web | ⚠ Form chỉ tạo câu chấm điểm 1–5 (API hỗ trợ đủ 4 loại) |
| Phạm vi khách của Quản lý | ⚠ Chỉ thấy khách đã có đơn hoàn tất |
| Test tự động (`tests/`, `database/tests/verify.py`) | ⚠ Còn viết theo bản nhiều chi nhánh — chưa dùng để đánh giá |
| `database/tools/seed.py` | ⛔ Không chạy — sẽ ghi đè seed 1 cửa hàng |

Chi tiết API cần bổ sung: `MorningBrewApp/API_CAN_BO_SUNG.md`.

---

## 9. Tài liệu liên quan

| File | Nội dung |
|---|---|
| `docs/api-v1.md` | Đặc tả API v1 (quy ước JSON, phân trang, lỗi) |
| `database/README.md` | Thiết kế cơ sở dữ liệu |
| `database/docs/data-dictionary.md`, `erd-*.png` | Từ điển dữ liệu, ERD |
| `MorningBrewApp/API_CAN_BO_SUNG.md` | Endpoint app khách còn thiếu |
| `docs/_cu/README_cu.md` | README cũ của nhánh test (lưu tham khảo) |
