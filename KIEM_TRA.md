# Kiểm tra bản ghép Admin_MC → MorningBrew

## Đã kiểm tra tại môi trường tạo file

- Phân tích cú pháp 49 file C# bằng tree-sitter: không có lỗi cú pháp được phát hiện; đây không phải build/kiểm tra kiểu dữ liệu hoặc biên dịch Razor.
- Soát mã nguồn: frontend không còn AppDb, DemoBackend, DemoApiController, SQLite, crm.db hay SQL cũ.
- CSS và JavaScript giao diện gốc giữ nguyên byte.
- Toàn bộ file nguồn backend gốc, Shared/ApiJson.cs và SQL 01–09 giữ nguyên; hai endpoint chỉ đọc được thêm riêng.
- Kiểm tra các form POST có handler, các liên kết trang cục bộ và tệp cấu hình JSON/XML.
- Dùng lại CrmApi gốc với snake_case và display_name; token ở cookie được bảo vệ, không localStorage; các POST dùng antiforgery của Razor.
- Bộ kiểm thử tích hợp gốc đã đổi đường dẫn sang Admin_MC và trường Username, bổ sung kiểm tra các trang cũ.

## Chưa kiểm tra được

- `dotnet build`: môi trường không có .NET SDK; truy cập nguồn tải SDK không thành công.
- Chạy Razor thực, kiểm tra hình ảnh giao diện và lỗi render.
- Kết nối SQL Server, tạo database, seed, đăng nhập và thao tác CRUD.
- Kiểm thử phân quyền, phân trang, CSRF, ETag và transaction lúc chạy.

Các chức năng đã nối mã nguồn không đồng nghĩa đã test thành công bằng dữ liệu thật. Không có dữ liệu kinh doanh thực được cung cấp ngoài bộ seed MorningBrew.

Có thể chạy lại kiểm tra nguồn: `python tests/check_source.py`. Phần cú pháp C# tự bỏ qua nếu máy chưa cài hai gói tree-sitter; vẫn phải chạy BUILD.cmd.

## Tự xác minh trên máy Windows

1. `BUILD.cmd` phải thành công, không chỉ hiện cửa sổ web.
2. Chạy API và mở `/health`; phải trả `ready` và `SqlServer`.
3. Đăng nhập Owner; mở Dashboard, Khách hàng, Sản phẩm, NCC, Tài khoản, Phản hồi, Khảo sát, Báo cáo, Ca làm, Hồ sơ, Phân quyền.
4. Thêm khách có email mới/mật khẩu ≥10 ký tự; đăng nhập ở cổng khách. Khóa bằng Owner rồi xác minh khách bị từ chối. Mở lại rồi thử đăng nhập.
5. Thêm sản phẩm với danh mục có thật; cập nhật giá từng biến thể; ngừng bán/bán trở lại; xác minh SQL thay đổi.
6. Tạo khảo sát có thời hạn hiện tại, phát hành, mời khách; khách trả lời đủ form; kiểm tra thống kê, đóng khảo sát.
7. Khách gửi góp ý; quản lý trả lời; khách xem nội dung phản hồi.
8. Xếp ca đúng chi nhánh, thử ca trùng, ca qua đêm và hủy ca; kiểm tra tổng giờ tháng loại ca đã hủy.
9. Mở cùng hồ sơ khách ở hai tab; tab thứ hai phải bị báo ETag cũ khi lưu sau tab đầu.
10. Đăng nhập AreaManager, BranchManager, Staff; xác minh dữ liệu/phạm vi và các thao tác bị từ chối ngoài quyền.
11. Tắt API/SQL; web phải báo lỗi, không chuyển sang dữ liệu mẫu. Khởi động lại và xác minh dữ liệu vừa thêm vẫn còn.

## Bộ tích hợp tự động (có ghi dữ liệu, dùng database test riêng)

Chạy trong PowerShell ở thư mục chứa solution; yêu cầu Python, dotnet, sqlcmd:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\database\tools\Apply.ps1 -Database MorningBrewCRM_AdminMC_Test
dotnet build .\MorningBrewCRM.slnx
python .\tests\integration.py --database MorningBrewCRM_AdminMC_Test
```

Database test phải mới; script từ chối tên không chứa Test/Verify. Kết quả chạy tại máy bạn được ghi `database/qa/api-integration-results.json`; không có kết quả runtime thành công được đóng sẵn trong ZIP.
