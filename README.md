# MorningBrew CRM

Bản đồ án CRM hiện tại gồm **MorningBrew.Api**, **MorningBrew.Admin** và app khách hàng **.NET MAUI Blazor**. Backend dùng SQL Server qua `SqlStore`, API `/v1`, phiên đăng nhập bearer; không dùng EF InMemory/JWT của bản cũ.

## Chạy sau khi clone (Windows)

Cài .NET SDK 10, workload .NET MAUI, Windows SDK phù hợp project, SQL Server 2022+ hoặc LocalDB và `sqlcmd`. Mở **MorningBrewCRM.slnx** ở thư mục gốc; solution có đủ ba project. Chọn Windows Machine khi chạy app MAUI; Android cần thêm SDK/emulator, iOS cần môi trường Mac để chạy.

Tại thư mục gốc repo, tạo database khớp cấu hình Development:

```powershell
./database/tools/Apply.ps1 -Database MorningBrewCRM_Crm
```

Installer chạy scripts 01–11 và tạo dữ liệu mẫu. Nếu database đã tồn tại, installer dừng để bảo vệ dữ liệu; không chạy lại để ghi đè. Nếu dùng database/server khác, cấu hình `ConnectionStrings:MorningBrew` bằng user secrets hoặc biến môi trường; xem [hướng dẫn database](database/README.md).

Build solution:

```powershell
dotnet build MorningBrewCRM.slnx
```

Nếu chỉ kiểm tra Windows, có thể build riêng app:

```powershell
dotnet build MorningBrew.Api/MorningBrew.Api.csproj
dotnet build MorningBrew.Admin/MorningBrew.Admin.csproj
dotnet build MorningBrewApp/MauiApp1/MauiApp1/MorningBrewwApp.csproj -p:TargetFrameworks=net10.0-windows10.0.19041.0
```

Chạy API và Admin trong hai terminal:

```powershell
dotnet run --project MorningBrew.Api --launch-profile http
```

```powershell
dotnet run --project MorningBrew.Admin --launch-profile http
```

- API: http://localhost:5029 — kiểm tra `/health`, Swagger tại `/swagger`.
- Admin: http://localhost:5191 — cấu hình Development gọi API ở cổng 5029.
  - `/Admin/*` — **giao diện Quản trị** (chỉ chủ cửa hàng): tổng quan hệ thống, tài khoản (lọc, thêm, đổi vai trò, khóa, xóa), ma trận phân quyền, nhật ký hệ thống, sao lưu & phục hồi, sản phẩm & giá, nhà cung cấp.
  - `/Manager/*` — **giao diện Quản lý CRM** (quản lý và chủ cửa hàng): khách hàng (thêm/khóa/xóa), phản hồi (lọc theo trạng thái), khảo sát (câu hỏi đánh giá/chọn một/chọn nhiều/tự luận, mời hàng loạt theo nhóm), ưu đãi, báo cáo.
  - `/Portal` — cổng khách hàng. Sau khi đăng nhập, hệ thống tự chuyển tới đúng giao diện theo vai trò.
- App MAUI: chọn project `MorningBrewwApp` và **Windows Machine** trong Visual Studio rồi chạy. Android emulator gọi API qua `10.0.2.2:5029`; thiết bị thật cần địa chỉ LAN và cấu hình kết nối phù hợp.

Tài khoản **dữ liệu mẫu local**: `owner@morningbrew.test`, `manager.1@morningbrew.test`, khách hàng `lan@morningbrew.test`; mật khẩu `MorningBrew!2026`.

## Phạm vi và ảnh sản phẩm

- CRM: tài khoản, khách hàng, phản hồi, khảo sát, báo cáo; dữ liệu từ SQL Server.
- Thực đơn/đơn hàng trong app có chế độ minh họa khi backend trả về endpoint chưa hỗ trợ. Không xem việc đặt đơn mô phỏng là kiểm thử đơn hàng thật; backend hiện chưa có `/v1/me/menu` và `/v1/me/orders`.
- 10 ảnh sản phẩm được đóng gói trong `MorningBrewApp/MauiApp1/MauiApp1/wwwroot/images/products`, ánh xạ theo tên tại `Services/ProductPhotos.cs`. Khi ảnh không tải được hoặc món chưa có ảnh, app dùng hình minh họa dự phòng.
- Nguồn ảnh: [sources.json](MorningBrewApp/MauiApp1/MauiApp1/wwwroot/images/products/sources.json). Đây là ảnh tham khảo cho bản demo; hồ sơ nguồn không đồng nghĩa với giấy phép tái phân phối/thương mại. Cold brew có logo 1000M.

## Tài liệu và kiểm thử

- [Database và scripts cài đặt](database/README.md).
- [Sao lưu và phục hồi CSDL](docs/backup-restore.md): `database/tools/Backup.ps1`, `Restore.ps1` và trang Admin → Sao lưu & phục hồi.
- [API v1](docs/api-v1.md) và [ghi chú tích hợp](docs/integration.md): đối chiếu với controller/seed hiện tại vì một số nội dung mô tả phạm vi cũ.
- `tests/integration.py` và `tests/api_contract.py` còn giả định dữ liệu nhiều chi nhánh/quản lý khu vực của bản trước; chưa phù hợp hoàn toàn với seed CRM rút gọn hiện tại. Chỉ chạy trên database kiểm thử riêng.
- [Tài liệu backend cũ](docs/legacy-backend.md) chỉ còn để tham khảo lịch sử; mã EF/JWT cũ đã được xóa khỏi repo (xem tag `backup-20261107` nếu cần đối chiếu).
- Chế độ **một cửa hàng**: `database/sql/11_single_store.sql` (quản lý thấy toàn bộ khách/phản hồi/khảo sát, nhiều quản lý, quyền `Customers.Manage`, bỏ vai trò AreaManager). **Database đã có phải chạy script này trước khi chạy API mới** (nếu không, tạo quản lý / xóa khách sẽ báo lỗi quyền):

  ```powershell
  sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\11_single_store.sql
  ```

Không đưa `bin/`, `obj/`, `.vs/`, file `*.user`, database MDF/LDF, bản backup hoặc cấu hình chứa mật khẩu riêng lên GitHub; các loại file này đã được `.gitignore` loại trừ.
