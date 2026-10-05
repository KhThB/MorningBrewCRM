# MorningBrewCRM

Hệ thống CRM cho chuỗi quán cà phê, .NET 10 + SQL Server 2022/LocalDB.

## Cấu trúc hiện tại

| Project | Vai trò |
|---|---|
| MorningBrew.Api | API xác thực, phân quyền và nghiệp vụ dùng SQL Server |
| MorningBrew.Admin | Razor Pages: quản trị CRM và cổng khách hàng; gọi API qua HTTP |
| MorningBrew.Pos | Khung Blazor của nhóm, chưa có luồng bán hàng hoàn chỉnh |
| database | Schema, stored procedure, dữ liệu demo, tài liệu và kiểm thử |

Admin và API dùng **một database**. Admin không mở kết nối database, không còn SQLite hoặc dữ liệu giả trong bộ nhớ. Giao diện/các chức năng của nhánh Admin được chuyển sang Razor Pages .NET 10, thay cho template Blazor Admin ban đầu. POS vẫn là project riêng. Cổng khách hàng là web trong Admin, **chưa phải ứng dụng mobile**.

## Chạy trên Visual Studio

1. Cài workload **ASP.NET and web development**, SDK .NET 10 và SQL Server 2022 Express LocalDB (`sqlcmd` cần cho script cài database).
2. Clone repo, mở `MorningBrewCRM.slnx`.
3. Nếu **chưa có** database, mở PowerShell tại thư mục solution:

   ```powershell
   .\database\tools\Apply.ps1
   ```

   Script tạo `MorningBrewCRM`, áp dụng `01`–`09` và seed demo. Nó từ chối ghi đè database đã tồn tại. File MDF/LDF nằm trong `database/.local/`, không được commit hoặc di chuyển khi đang attach.

   Nếu đã cài bộ schema `01`–`07` của repo, chỉ chạy phần bổ sung, không chạy lại seed:

   ```powershell
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM -E -C -I -b -f 65001 -i database/sql/08_admin.sql
   ./database/tools/Upgrade-SharedPrices.ps1 -Database MorningBrewCRM
   ```

4. Kiểm tra `MorningBrew.Api/appsettings.Development.json`: kết nối mặc định `(localdb)\MSSQLLocalDB`, database `MorningBrewCRM`, Windows Authentication.
5. Trong Solution Properties → Startup Project, chọn chạy **MorningBrew.Api** và **MorningBrew.Admin**; dùng profile `https` cho cả hai. Có thể thêm POS để tiếp tục phát triển.
6. Nhấn F5, mở [Admin](https://localhost:7287), [API Swagger](https://localhost:7132/swagger).

Chạy từ terminal (hai cửa sổ):

```powershell
dotnet run --project MorningBrew.Api --launch-profile https
dotnet run --project MorningBrew.Admin --launch-profile https
```

Nếu máy chưa tin chứng chỉ HTTPS phát triển: `dotnet dev-certs https --trust`.

### Tài khoản demo

Chỉ có sau khi chạy seed. Mật khẩu: **MorningBrew!2026**.

| Email | Vai trò |
|---|---|
| owner@morningbrew.test | Chủ chuỗi |
| area.east@morningbrew.test | Quản lý khu Đông, chi nhánh 1 và 2 |
| manager.1@morningbrew.test | Quản lý chi nhánh 1 |
| staff.1@morningbrew.test | Nhân viên POS; không vào web quản trị |
| lan@morningbrew.test | Khách hàng, vào cổng thành viên |

Không đưa seed/tài khoản demo lên môi trường thực. API không tự tạo/reset database khi khởi động.

## Chức năng đã nối SQL Server

- Đăng nhập theo email bằng PasswordHasher; phiên bearer ngẫu nhiên, chỉ lưu SHA-256 trong `UserSessions`. Mỗi request kiểm tra tài khoản còn hoạt động, vai trò hiện tại và phạm vi từ SQL.
- Dashboard, tìm khách hàng, tạo tài khoản/hồ sơ, khóa/mở tài khoản, điều chỉnh điểm có lý do và chống ghi lặp.
- Phản hồi khách hàng → quản lý trả lời → khách thấy kết quả.
- Khảo sát: tạo nháp, phát hành, mời, trả lời, xem kết quả, đóng. API hỗ trợ Text/Rating/SingleChoice/MultipleChoice; form tạo nhanh trên Admin dùng thang điểm 1–5.
- Ưu đãi, cấp voucher, đổi điểm; dùng stored procedure để kiểm soát hạn mức và bảo toàn sổ điểm.
- Danh mục sản phẩm/biến thể, giá chung toàn chuỗi, nhà cung cấp, nhân sự và ca làm; từ chối ca trùng hoặc ngoài phạm vi.
- Quyền quản lý theo **vai trò**, áp dụng ngay; báo cáo doanh thu có trừ hoàn tiền.
- Cổng khách hàng: hồ sơ, điểm, voucher, phản hồi, khảo sát.

## Các quyết định khi ghép nhánh

Xem [bản ghi tích hợp](docs/integration.md). Lịch sử `master`, `lmt`, `Admin`, `Backend2(test)` và commit setup trên máy được giữ qua merge commits. ZIP Admin được giải nén thành mã nguồn có thể build; không dùng ZIP làm bản chạy.

Các contract API được thống nhất lại theo schema SQL hiện tại. Không tương thích nguyên trạng với client dùng route/DTO/JWT demo cũ; dùng Swagger và [request mẫu](MorningBrew.Api/MorningBrew.Api.http). Các API chính:

- `POST /v1/auth/login`, `GET /v1/auth/me`, `POST /v1/auth/logout`
- `GET /v1/{customers|feedbacks|surveys|promotions|products|variants|accounts|staff|shifts|suppliers|permissions}`; phân trang qua `pageSize`, `pageToken`.
- `GET /v1/dashboard/overview`, `/v1/reports/revenue`, `/v1/lookups/options`
- `POST /v1/accounts`, `/v1/surveys`, `/v1/promotions`, `/v1/products`, `/v1/shifts`, `/v1/suppliers`; trả `201` và `Location`.
- `GET/PATCH /v1/me/profile`; PATCH yêu cầu `If-Match` lấy từ ETag lần đọc gần nhất.
- `PUT /v1/variants/{id}/price`: Owner thay giá chung; không nhận chi nhánh.

**Hợp đồng v1:** JSON `snake_case`, lỗi thống nhất, timestamp UTC, token phân trang được bảo vệ. Các route `/api/...` đã được thay thế; Admin trong repo đã cập nhật đồng bộ. Xem [hướng dẫn API v1](docs/api-v1.md) và Swagger để tra body và quyền truy cập.

`UserID` và `CustomerID` khác nhau. API tự tìm CustomerID từ phiên đăng nhập. Client không được tự quyết ActorID, quyền hoặc phạm vi.

## Cấu hình

- API: `ConnectionStrings__MorningBrew` hoặc .NET User Secrets; không commit mật khẩu SQL.
- Admin: `Api__BaseUrl` mặc định `https://localhost:7132/`.
- Production cần HTTPS, cấu hình URL đúng và lưu Data Protection keys ổn định cho cookie và token phân trang.
- SQL role `MorningBrewApi` dành riêng cho tài khoản dịch vụ API có quyền CRUD metadata/EXECUTE, không có quyền DDL. Không cấp credential này cho frontend/người dùng. Role `MorningBrewRuntime` cũ chỉ dùng cho các procedure, không đủ để chạy phần metadata của API này.

## Kiểm thử

```powershell
dotnet build MorningBrewCRM.slnx
.\database\tools\Apply.ps1 -Database MorningBrewCRM_TestRun
python database/tests/verify.py --database MorningBrewCRM_TestRun
./database/tools/Apply.ps1 -Database MorningBrewCRM_TestApiRun
python tests/integration.py --database MorningBrewCRM_TestApiRun
```

Cần Python 3 trên PATH để chạy test. Dùng tên database mới cho mỗi lần chạy bộ SQL gốc. Bộ test cố ý thay đổi dữ liệu và từ chối tên không chứa `Test` hoặc `Verify`. Test API mở cổng loopback 15029/15191 và tự dừng các tiến trình do nó tạo; không tác động tiến trình Visual Studio. Log/kết quả ở `database/qa/`, được bỏ qua bởi Git.

## Làm việc nhóm sau khi ghép

```powershell
git switch master
git pull --ff-only origin master
git switch -c feature/ten-chuc-nang
```

Commit source thay vì ZIP, mở Pull Request vào `master`, kiểm tra build và luồng liên quan trước khi merge. Không force-push lên master; giữ nhánh thành viên cho đến khi mọi người xác nhận phần việc.
