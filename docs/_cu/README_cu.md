# Morning Brew CRM — bản nhóm cùng kiểm thử

Nhánh `codex/crm-team-test` chứa bản CRM đã thu gọn: **1 cửa hàng Morning Brew Thủ Đức, 30 khách hàng mẫu**, bỏ giao diện/API nhân sự và ca làm. Bao gồm API, web quản trị, app khách hàng MAUI và SQL Server. Đây là bản bàn giao để kiểm thử, chưa phải bản hoàn thiện toàn bộ yêu cầu đồ án.

## 1. Clone đúng nhánh

```powershell
git clone --branch codex/crm-team-test --single-branch https://github.com/KhThB/MorningBrewCRM.git MorningBrewCRM-test
cd MorningBrewCRM-test
```

Nếu đã clone repo:

```powershell
git fetch origin
git switch --track origin/codex/crm-team-test
```

## 2. Công cụ cần có

- .NET SDK 10; Visual Studio có workload ASP.NET nếu chạy bằng IDE.
- SQL Server 2022+ hoặc LocalDB tương thích mức 160, và `sqlcmd` hỗ trợ `-C`.
- Chỉ cần workload .NET MAUI nếu muốn chạy app khách hàng. API/web có thể test riêng.

## 3. Tạo database cho bản một cửa hàng

Tại thư mục gốc vừa clone, chạy PowerShell:

```powershell
.\database\tools\Apply.ps1 -Database MorningBrewCRM_Crm
```

Nếu PowerShell chặn script, chạy `Set-ExecutionPolicy -Scope Process Bypass` trong chính cửa sổ này rồi thử lại. Script tạo database mới và nạp SQL 01–09; từ chối ghi đè database đã tồn tại.

`MorningBrew.Api/appsettings.Development.json` đã trỏ đến `(localdb)\MSSQLLocalDB`, database `MorningBrewCRM_Crm`, xác thực Windows. Nếu tên database đã tồn tại và muốn một bộ dữ liệu test mới, dùng tên khác với `-Database` và sửa tên tương ứng trong cấu hình API. Không cần xóa database cũ.

Schema dùng chung nền tảng với nhánh `integration/crm-sqlserver`, nhưng **seed của nhánh này đã đổi thành 1 cửa hàng / 30 khách**. Dùng database nhiều chi nhánh cũ sẽ không tự biến thành bộ dữ liệu một cửa hàng.

## 4. Build và chạy API/web

```powershell
dotnet build .\MorningBrewCRM.slnx
```

Mở hai cửa sổ PowerShell, đều ở thư mục gốc repo.

Cửa sổ API:

```powershell
dotnet run --project .\MorningBrew.Api --launch-profile http
```

Cửa sổ web quản trị:

```powershell
$env:Api__BaseUrl = 'http://localhost:5029/'
dotnet run --project .\MorningBrew.Admin --launch-profile http
```

- Web quản trị: http://localhost:5191
- Kiểm tra kết nối SQL: http://localhost:5029/health
- Swagger: http://localhost:5029/swagger

Cách chạy HTTP trên dành cho test tại máy cá nhân. Nếu dùng profile HTTPS trong Visual Studio, API dùng cổng 7132 và web dùng 7287 theo launchSettings có sẵn.

## 5. Chạy app khách hàng

Giữ API chạy ở cổng 5029. Mở `MorningBrewApp/MauiApp1/MauiApp1.slnx` bằng Visual Studio có workload .NET MAUI, chọn **Windows Machine** rồi chạy. Đây là solution riêng, không nằm trong lệnh build API/web ở trên.

App Windows gọi `http://localhost:5029/`; Android Emulator gọi `http://10.0.2.2:5029/`. Cấu hình nằm tại `MorningBrewApp/MauiApp1/MauiApp1/Services/ApiClient.cs`.

## 6. Tài khoản seed chỉ dùng thử

Mật khẩu demo chung: `MorningBrew!2026`.

| Email | Vai trò / nơi dùng |
|---|---|
| owner@morningbrew.test | Chủ cửa hàng, web quản trị |
| manager.1@morningbrew.test | Quản lý cửa hàng, web quản trị |
| lan@morningbrew.test | Khách hàng, app hoặc cổng khách trên web |
| minh@morningbrew.test | Khách hàng phụ |
| staff.1@morningbrew.test | Thu ngân; POS vẫn là khung |

## 7. Checklist để thành viên test

- [ ] Tạo database mới thành công; kiểm tra 1 cửa hàng và 30 khách trước khi thêm dữ liệu thử.
- [ ] `/health` trả `ready`; đăng nhập owner, manager và khách hàng.
- [ ] Owner thêm khách hàng; khóa tài khoản làm phiên cũ mất hiệu lực; mở khóa đăng nhập lại được.
- [ ] Khách chỉnh hồ sơ và sở thích; tải lại để xác nhận dữ liệu đã lưu.
- [ ] Khách gửi phản hồi sản phẩm; quản lý trả lời; khách nhìn thấy phản hồi.
- [ ] Tạo khảo sát, phát hành, mời khách, khách trả lời; quản lý xem kết quả.
- [ ] Kiểm tra khách chưa từng mua hàng có xuất hiện trong danh sách và lời mời của manager không.
- [ ] Kiểm tra quyền: khách không truy cập được trang/API quản trị.

Khi báo lỗi, ghi vai trò đang dùng, thao tác, kết quả mong đợi, lỗi thực tế và tên database. Đính kèm ảnh hoặc HTTP status nếu có.

## 8. Phần còn thiếu và giới hạn đã biết

- Có màn hình đăng ký app nhưng API `/v1/auth/register` chưa có.
- Chưa có thao tác xóa khách hàng riêng; hiện có khóa/mở khóa.
- Báo cáo web hiện là doanh thu; chưa hoàn thiện báo cáo tỷ lệ độ tuổi và sở thích theo đề CRM.
- API khảo sát hỗ trợ 4 kiểu câu hỏi, nhưng form tạo khảo sát trên web hiện chỉ tạo câu chấm điểm 1–5. Kết quả hiện là danh sách câu trả lời, chưa phải bảng tổng hợp tỷ lệ/điểm trung bình.
- Tạo và khóa tài khoản hiện dành cho Owner. Phạm vi khách của BranchManager vẫn phụ thuộc đơn hàng đã hoàn tất, cần đánh giá lại cho CRM một cửa hàng.
- API thực đơn/đặt hàng của app chưa có; app dùng dữ liệu mẫu khi gặp endpoint chưa được hỗ trợ. Không coi đơn mẫu là giao dịch SQL đã lưu.
- `tests/integration.py`, `tests/api_contract.py`, `database/tests/verify.py` và nhiều tài liệu cũ vẫn dựa trên nhiều chi nhánh/HRM. Chưa dùng chúng để kết luận bản rút gọn đã đạt toàn bộ kiểm thử.
- `database/tools/seed.py` là công cụ sinh seed cũ; chạy nó sẽ thay bộ seed đã thu gọn.

## 9. Trạng thái kiểm chứng khi bàn giao

- Đã build solution API/web/POS: thành công, 0 lỗi; NuGet báo cảnh báo NU1902/NU1903 ở dependency `System.Security.Cryptography.Pkcs` 9.0.11.
- Đã tạo database QA bằng bộ SQL gốc trên nhánh GitHub `integration/crm-sqlserver` ở commit `187ecb1`: thành công, 43 bảng. Việc này không thay thế kiểm thử bộ seed một cửa hàng của nhánh này.
- Chưa hoàn tất chạy xuyên suốt API/web với SQL; chưa xác nhận build/chạy app MAUI trên thiết bị. Không coi checklist ở trên là kết quả đã pass.

Các tài liệu `HUONG_DAN.md`, `WALKTHROUGH.md`, `UI_UX_CAU_TRUC_VAN_HANH.md` được giữ từ bản thành viên gửi để tham khảo. Chúng có đường dẫn máy cũ; dùng cấu trúc và lệnh trong README này khi clone nhánh test.
