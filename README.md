# MorningBrew CRM

Bản đồ án CRM hiện tại gồm **MorningBrew.Api**, **MorningBrew.Admin** và app khách hàng **.NET MAUI Blazor**. Backend dùng SQL Server qua `SqlStore`, API `/v1`, phiên đăng nhập bearer; không dùng EF InMemory/JWT của bản cũ.

## Chạy sau khi clone (Windows)

Cài .NET SDK 10, workload .NET MAUI, Windows SDK phù hợp project, SQL Server 2022+ hoặc LocalDB và `sqlcmd`. Mở **MorningBrewCRM.slnx** ở thư mục gốc; solution có đủ ba project. Chọn Windows Machine khi chạy app MAUI; Android cần thêm SDK/emulator, iOS cần môi trường Mac để chạy.

Tạo database khớp cấu hình Development (`MorningBrewCRM_Crm`): mở SSMS → kết nối `(localdb)\MSSQLLocalDB` → mở **`database/sql_ssms/morningbrew_fullscript.sql`** → Execute (F5). File tự **cài mới** (database trống: script 01–14 kèm dữ liệu mẫu) hoặc **nâng cấp** (database đang dùng: sao lưu `.bak` rồi chạy 08–14), không cần PowerShell.

Cách khác bằng PowerShell: `./database/tools/Apply.ps1` (chạy cùng file fullscript; thêm `-Upgrade` để nâng cấp database đang dùng). Nếu dùng database/server khác, cấu hình `ConnectionStrings:MorningBrew` bằng user secrets hoặc biến môi trường; xem [hướng dẫn database](database/README.md).

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
- Web nội bộ gồm **hai giao diện tách biệt** chạy từ project `MorningBrew.Admin` (mỗi cổng một phiên đăng nhập riêng) [SEP]:
  - **Admin server**: http://localhost:5190 — chỉ `/Admin/*`, chỉ chủ cửa hàng.
  - **Quản lý CRM**: http://localhost:5191 — chỉ `/Manager/*`, chủ cửa hàng và quản lý.
  - Mở nhầm khu của giao diện kia sẽ được chuyển sang trang hướng dẫn kèm link đúng cổng. Cổng/địa chỉ cấu hình ở `appsettings.json` → `Portals`. Cả hai gọi API ở cổng 5029.
  - `/Admin/*` — **giao diện Quản trị** (chỉ chủ cửa hàng): tổng quan hệ thống, tài khoản (lọc, thêm, đổi vai trò, khóa, xóa), ma trận phân quyền, nhật ký hệ thống, sao lưu & phục hồi, sản phẩm & giá, nhà cung cấp.
  - `/Manager/*` — **giao diện Quản lý CRM** (quản lý và chủ cửa hàng): khách hàng (thêm/khóa/xóa), phản hồi (lọc theo trạng thái), khảo sát (câu hỏi đánh giá/chọn một/chọn nhiều/tự luận, mời hàng loạt theo nhóm), ưu đãi, báo cáo.
  - Web chỉ dành cho nội bộ: chủ cửa hàng đăng nhập Admin server (5190) để quản trị và có thể đăng nhập riêng Quản lý CRM (5191); quản lý chỉ đăng nhập được Quản lý CRM. **Khách hàng chỉ dùng app MAUI** (đăng ký, đăng nhập, sửa hồ sơ, phản hồi, khảo sát); thu ngân dùng POS.
- App MAUI khách hàng: chọn project `MorningBrewwApp` và **Windows Machine** trong Visual Studio rồi chạy. Android emulator gọi API qua `10.0.2.2:5029`; thiết bị thật cần địa chỉ LAN và cấu hình kết nối phù hợp.
- App **POS** cho thu ngân: chọn project `MorningBrewPos` → **Windows Machine**. Đăng nhập bằng `staff.1@morningbrew.test` (quản lý / chủ cửa hàng cũng đăng nhập được và có thêm quyền hủy đơn).

Tài khoản **dữ liệu mẫu local**: `owner@morningbrew.test`, `manager.1@morningbrew.test`, thu ngân `staff.1@morningbrew.test`, khách hàng `lan@morningbrew.test`; mật khẩu `MorningBrew!2026`.

## Phạm vi và ảnh sản phẩm

- CRM: tài khoản, khách hàng, phản hồi, khảo sát, báo cáo; dữ liệu từ SQL Server.
- Thực đơn và đơn hàng là **dữ liệu thật**: app khách đặt đơn (nhận tại quầy / giao hàng; trả trước qua cổng thanh toán mô phỏng / tiền mặt), quản lý xử lý ở `/Manager/Orders`, thu ngân xử lý trên app POS. Đơn trả trước không thanh toán trong 15 phút được API tự hủy (chạy mỗi phút). Khi không kết nối được API, app báo lỗi kèm nút Thử lại.
- 10 ảnh sản phẩm được đóng gói trong `MorningBrewApp/MauiApp1/MauiApp1/wwwroot/images/products`, ánh xạ theo tên tại `Services/ProductPhotos.cs`. Khi ảnh không tải được hoặc món chưa có ảnh, app dùng hình minh họa dự phòng.
- Nguồn ảnh: [sources.json](MorningBrewApp/MauiApp1/MauiApp1/wwwroot/images/products/sources.json). Đây là ảnh tham khảo cho bản demo; hồ sơ nguồn không đồng nghĩa với giấy phép tái phân phối/thương mại. Cold brew có logo 1000M.

## Tài liệu và kiểm thử

- [Database và scripts cài đặt](database/README.md).
- [Sao lưu và phục hồi CSDL](docs/backup-restore.md): sao lưu, phục hồi và sao lưu tự động hằng ngày đều trên trang Admin → Sao lưu & phục hồi.
- [API v1](docs/api-v1.md): quy ước JSON, phân trang, danh sách endpoint.
- [Sổ tay lỗi đã xử lý](docs/bug_history_summary.md) và [hướng dẫn app khách hàng](MorningBrewApp/HUONG_DAN_CHAY.md).
- `tests/integration.py` và `tests/api_contract.py` còn giả định dữ liệu nhiều chi nhánh/quản lý khu vực của bản trước; chưa phù hợp hoàn toàn với seed CRM rút gọn hiện tại. Chỉ chạy trên database kiểm thử riêng.
- Database đã có (cài từ bản trước): chạy lại `database/sql_ssms/morningbrew_fullscript.sql` – file tự sao lưu rồi áp dụng 08–14 (chế độ một cửa hàng, sửa lỗi, góp ý giao diện 09/11, kho nguyên liệu). Không cần chạy lẻ từng script.

Không đưa `bin/`, `obj/`, `.vs/`, file `*.user`, database MDF/LDF, bản backup hoặc cấu hình chứa mật khẩu riêng lên GitHub; các loại file này đã được `.gitignore` loại trừ.
