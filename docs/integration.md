# Bản ghi tích hợp — 05/10/2026

> Cập nhật 2026-10-05: API hiện dùng `/v1`, JSON snake_case và giá chung toàn chuỗi. Nội dung bên dưới ghi lại lần ghép nhánh ban đầu; hợp đồng hiện hành và cách nâng cấp ở [API v1](api-v1.md).

## Nguồn và lịch sử

| Nguồn | Commit đã đối chiếu | Kết quả |
|---|---|---|
| master/lmt | 5215fca | Cùng nội dung, không có thay đổi riêng cần chọn ở lmt |
| Admin | 9686495 | ZIP Admin: lấy giao diện CSS/JS và chuyển chức năng sang SQL/API |
| Backend2(test) | a793766 | Logic CRM gốc và README; backend code đã có trong master từ PR #1 |
| Setup trên máy | 6b58fec | Giữ solution gồm API, Admin, POS |
| database trên máy | a7120a8 | Lưu schema, script, tài liệu, kiểm thử trước khi ghép |

Có nhánh local `backup/before-integration-20261005` giữ mốc setup ban đầu. Các commit của thành viên vẫn trong lịch sử merge; không rewrite lịch sử hay xóa nhánh.

## Điều chỉnh bắt buộc để dùng chung database

- Model EF demo không khớp tên bảng/cột của SQL, nên thay lớp truy cập InMemory bằng SqlClient có tham số và các stored procedure hiện có. Không chạy EnsureCreated/EF migration lên schema SQL.
- SQLite trong ZIP và dữ liệu seed demo của Admin không được trộn vào SQL. Seed SQL là nguồn demo duy nhất; database hiện có được giữ nguyên dữ liệu.
- Mật khẩu thường/JWT secret demo được thay bằng PasswordHasher và token phiên opaque trong UserSessions. Admin giữ token trong cookie được Data Protection mã hóa; API kiểm tra vai trò, quyền và phạm vi ở mỗi lần gọi.
- Quyền BusinessOwner/StoreManager/Employee cũ tương ứng Owner/BranchManager/Staff. UserID không được dùng thay CustomerID.
- Quyền tùy biến theo người dùng của demo chuyển thành RolePermissions theo schema chính thức. Form quản trị chỉ chỉnh quyền vận hành của AreaManager/BranchManager, không cho tự cấp quyền chủ chuỗi.
- Sổ điểm lưu delta có dấu. Điểm Earn đến từ hoàn tất đơn; Redeem đến từ cấp voucher; chỉnh tay dùng Adjust và audit log. Chống gửi lặp bằng RequestKey và khóa giao dịch.
- Đóng khảo sát không mở lại để giữ nguyên câu trả lời; khóa tài khoản, ngừng bán sản phẩm/nhà cung cấp thay cho xóa dây chuyền lịch sử.
- Thời hạn khảo sát/ưu đãi nhận DateTimeOffset và chuyển UTC trước khi ghi SQL. Thời gian trong biểu mẫu web hiện theo múi giờ máy chạy Admin.
- `08_admin.sql` thêm Suppliers, StaffShifts, quyền Staff.Manage và SQL role cho dịch vụ API. Đây là migration bổ sung có thể chạy lại, không reset dữ liệu.

## Phạm vi còn để phát triển

- POS đang là template Blazor; các luồng đặt đơn/thanh toán/hoàn tiền đã có stored procedure và test SQL, chưa có giao diện POS đầy đủ.
- Cổng khách hàng là web trong Admin; chưa có app mobile riêng.
- Số tồn kho demo trên sản phẩm không được đưa thành tồn kho thật: schema chính chưa có nhập/xuất kho. Chỉ giữ trạng thái có bán/tạm hết và giá theo chi nhánh.
- Form tạo khảo sát dùng câu hỏi Rating; API và trang trả lời hỗ trợ thêm Text, SingleChoice, MultipleChoice.
- Không tự đăng ký công khai, không nối nhà cung cấp thanh toán/email/SMS; chủ chuỗi tạo tài khoản trong bản đồ án này.
- Tài liệu ERD/Word gốc mô tả 41 bảng; xem phụ lục 08_admin.sql cho 2 bảng bổ sung (tổng 43).

## Kiểm chứng

Kết quả chi tiết tại database/qa trên máy chạy test (không commit log/token). Không dùng database MorningBrewCRM thật cho các bài test có ghi dữ liệu.

Xác nhận ngày 05/10/2026 trên SQL Server LocalDB:
- Build solution: 0 lỗi, 0 cảnh báo.
- database/tests/verify.py: 49/49 kiểm tra đạt trên MorningBrewCRM_TestMergedFinal.
- tests/integration.py: 13/13 nhóm kiểm tra đạt, gồm CSRF, phạm vi chi nhánh, phiên bị thu hồi, chống ghi điểm lặp và dữ liệu còn sau khi API khởi động lại.
- Database MorningBrewCRM đang dùng chưa được chạy migration 08 trong quá trình test; xem README để áp dụng khi chạy nhánh này.
