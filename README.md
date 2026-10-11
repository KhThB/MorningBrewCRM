# Admin_MC + MorningBrew SQL Server

Đã thay backend của Admin_MC bằng MorningBrew.Api trong file MorningBrewCRM-integration-crm-sqlserver.zip. Giao diện Razor, bố cục, CSS và JavaScript của Admin_MC là nền giao diện. Các trường/nút được chỉnh theo hợp đồng API mới.

## Chạy trên Windows

1. Cài **.NET 10 SDK** (project MorningBrew gốc là net10.0), SQL Server 2022+ hoặc LocalDB tương thích, và `sqlcmd`. Nếu Visual Studio hiện tại không hỗ trợ net10.0, vẫn có thể mở thư mục và chạy các file CMD bằng SDK; không đổi riêng một project về net8.0.
2. Giải nén toàn bộ ZIP. Mở đúng thư mục có `CHAY_CA_HAI.cmd` và `MorningBrewCRM.slnx`.
3. Nếu chưa có database: chạy `TAO_DATABASE_MOI.cmd`. Bộ cài dùng schema, procedure và seed **của MorningBrew**, không dùng dữ liệu Admin_MC. Script từ chối ghi đè database đã tồn tại. Nếu đã có MorningBrewCRM đủ script 01–09, bỏ qua bước tạo mới.
4. Sửa `MorningBrew.Api/appsettings.Development.json` nếu dùng server khác:

```json
{
  "ConnectionStrings": {
    "MorningBrew": "Server=(localdb)\\MSSQLLocalDB;Database=MorningBrewCRM;Integrated Security=true;TrustServerCertificate=true"
  }
}
```

5. Chạy `BUILD.cmd`. Sau khi build thành công, chạy `CHAY_CA_HAI.cmd`.
6. Đợi cả hai cửa sổ báo `Now listening on`. Mở **http://localhost:5199**. API: **http://localhost:5099**; kiểm tra database: **http://localhost:5099/health**; Swagger trong Development: **http://localhost:5099/swagger**.

Nếu chạy bằng PowerShell từ thư mục gốc:

```powershell
dotnet build .\MorningBrewCRM.slnx
.\CHAY_CA_HAI.cmd
```

Không chạy `dotnet build` từ thư mục cha chỉ chứa file ZIP; đó là nguyên nhân MSB1003. Khi giải nén, giữ nguyên `wwwroot` trong project web để CSS/JS được phục vụ.

## Đăng nhập

Chỉ có các tài khoản dưới đây nếu đã nạp seed MorningBrew. Mật khẩu seed: `MorningBrew!2026`.

| Email | Vai trò |
|---|---|
| owner@morningbrew.test | Owner |
| area.east@morningbrew.test | AreaManager |
| manager.1@morningbrew.test | BranchManager |
| staff.1@morningbrew.test | Staff — trang hồ sơ và ca cá nhân |
| lan@morningbrew.test | Customer — cổng thành viên |

Các tài khoản owner/area/store/employee và mật khẩu 123456 của Admin_MC cũ đã bị bỏ. Không có đăng nhập dự phòng khi SQL Server/API tắt.

## Cấu trúc

- `Admin_MC/`: frontend Razor Pages và lớp gọi HTTP API; không có kết nối database.
- `MorningBrew.Api/`: toàn bộ backend SQL Server của MorningBrew; thêm hai API chỉ đọc cho ca cá nhân và thống kê sản phẩm yêu thích.
- `Shared/ApiJson.cs`: quy ước JSON dùng chung nguyên bản MorningBrew.
- `database/`: schema, procedure, dữ liệu seed, công cụ cài SQL Server của MorningBrew.
- `MorningBrew.Pos/`: project POS nguyên bản của MorningBrew, vẫn là khung, không được coi là POS hoàn thiện.
- `tests/`: kiểm thử nguồn và bộ kiểm thử tích hợp cần .NET/SQL Server.

## Thay đổi nghiệp vụ trên giao diện

| Giao diện | Cách dùng backend MorningBrew |
|---|---|
| Đăng nhập/đăng xuất | Email và mật khẩu → bearer session; cookie web được bảo vệ; POST logout có chống CSRF |
| Dashboard | API tổng quan + nhân sự trong phạm vi; không có số liệu hardcode |
| Khách hàng | Xem/tìm theo API; Owner tạo tài khoản, khóa/mở khóa qua account; phân biệt CustomerID và UserID |
| Sản phẩm | Biến thể/kích cỡ và giá chung; Owner tạo, sửa giá, ngừng bán/bán trở lại |
| Nhà cung cấp | Xem/tìm/lọc/thêm/đổi trạng thái; Owner quản lý |
| Tài khoản | Owner tạo theo vai trò và phạm vi, khóa/mở khóa; không xóa lịch sử |
| Phân quyền | Theo RolePermissions của MorningBrew, không phải UserPermissions cũ |
| Ca làm | Chọn nhân viên + chi nhánh, tạo/hủy; ca qua đêm; tổng tháng loại ca đã hủy; giờ Việt Nam |
| Phản hồi | Gửi/phản hồi/đánh dấu Resolved qua API; nội dung trả lời xuất hiện ở cổng khách |
| Khảo sát | Tạo nháp, chọn phạm vi/thời hạn, phát hành, mời, trả lời toàn bộ form một lần, đóng |
| Báo cáo CRM | Độ tuổi, khóa tài khoản, sản phẩm yêu thích, kết quả khảo sát theo dữ liệu trong phạm vi |
| Ưu đãi/điểm/doanh thu | Các trang MorningBrew bổ sung nằm trong shell Admin_MC |
| Hồ sơ khách | PATCH kèm ETag để tránh ghi đè cập nhật đồng thời; không thay email đăng nhập |

Các chức năng **không có API tương ứng trong bản MorningBrew** không được giả lập: tự đăng ký khách hàng; xóa cứng tài khoản/khách/sản phẩm/nhà cung cấp; tồn kho kiểu Admin_MC; sửa tên/nhóm danh mục/chi tiết nhà cung cấp; sửa trực tiếp ca đã tạo; mở lại khảo sát đã đóng. Các thao tác được chuyển thành khóa/ngừng bán/hủy khi backend hỗ trợ; trường chỉ xem được đánh dấu readonly. Muốn đổi ca hãy hủy rồi tạo ca mới.

Không chuyển dữ liệu từ crm.db cũ sang SQL Server. Bộ seed MorningBrew cũng là **dữ liệu minh họa trong SQL Server**, không phải dữ liệu kinh doanh thực tế đã xác minh.

## Trạng thái kiểm tra

Đọc `KIEM_TRA.md`. Trong môi trường bàn giao chưa có .NET SDK, SQL Server hay sqlcmd; do đó **chưa xác nhận build, chạy web hay CRUD thực tế thành công**. Không lấy báo cáo kiểm thử của project MorningBrew gốc làm kết quả của bản ghép này.
