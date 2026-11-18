# Morning Brew CRM — database

SQL Server 2022+ (hoặc LocalDB 2022). Mô hình **một cửa hàng** (Morning Brew Thủ Đức), **45 bảng, 24 thủ tục, 21 trigger, 4 hàm, 3 view** sau khi chạy đủ 7 script:
41 bảng CRM/đơn hàng/khảo sát/nhà cung cấp, 2 bảng kho nguyên liệu (`Ingredients`, `InventoryTransactions`), bảng định mức `RecipeItems` và bảng hạng thành viên `MemberTiers`.
Database dùng cho cấu hình Development: **`MorningBrewCRM_Crm`** trên `(localdb)\MSSQLLocalDB`.

## Cài database — một file, không cần PowerShell

Mở SSMS → kết nối `(localdb)\MSSQLLocalDB` → File → Open **`database/sql_ssms/morningbrew_fullscript.sql`** → Execute (F5).
Không cần chọn database trước: file tự tạo `MorningBrewCRM_Crm` (nếu chưa có) rồi chạy 01 → 07 (gồm dữ liệu mẫu).
**Chỉ cài mới**: nếu database đã có bảng thì file dừng ngay, không thay đổi gì. Muốn cài lại thì đóng API / web / app / POS,
xóa database cũ (SSMS → Databases → chuột phải `MorningBrewCRM_Crm` → Delete, tích *Close existing connections*) rồi chạy lại.
Nên sao lưu dữ liệu đang dùng trước (Admin server → Sao lưu & phục hồi).

Thành công khi tab Messages có `=== HOÀN TẤT ...` và bảng kiểm tra có `SoBang = 45`, `SoThuTuc = 24`, `SoTrigger = 21`, `SoHam = 4`, `SoView = 3`.
Thấy `>>> DỪNG` thì phần sau không chạy; đọc dòng lỗi đỏ đầu tiên. Tên database khác: Find and Replace `MorningBrewCRM_Crm`
trong file và sửa `ConnectionStrings:MorningBrew` của API.

Chạy lẻ từng script (thay cho fullscript): tạo database trống, chọn database đó ở ô database (không để `master`) rồi chạy lần lượt 01 → 07.

## Bản vá thiếu kho ngày 18/11/2026 cho database đang dùng

Chọn đúng database CRM trong SSMS rồi chạy [patches/20261118_zero_stock_shortage.sql](patches/20261118_zero_stock_shortage.sql).
Bản vá chỉ cập nhật CHECK của phiếu kho và trigger tự trừ kho trong một giao dịch, không xóa hay tạo lại dữ liệu; có thể chạy lại.
Khi tồn đã bằng 0, đơn vẫn hoàn tất và có phiếu Xuất dùng tự động với thay đổi 0, kèm lượng thiếu cần kiểm kê.
Phiếu xuất/hủy thủ công vẫn phải có lượng thay đổi âm. Bản vá không khôi phục ghi chú thiếu của các đơn cũ đã hoàn tất.
Cài mới bằng fullscript đã có sẵn sửa này, không cần chạy thêm bản vá.

## Thứ tự script (`database/sql`)

Bảy file, chạy đúng thứ tự trên database trống. [GOP-01] Ngày 17/11/2026 các script nâng cấp 08–17 đã được gộp vào 01–07
(bản cũ lưu ở lịch sử Git); bộ gộp đã kiểm tra: cấu trúc trùng khớp database cài bằng 17 script cũ, test 15–17 đạt 18/18.

| Script | Nội dung |
|---|---|
| `01_schema.sql` | 45 bảng, CHECK, UNIQUE, khóa ngoại, index; sinh từ `tools/schema.py`, chỉ chạy trên database trống. |
| `02_rules.sql` | 4 hàm phân quyền (`CanBranch`, `CanScope`, `CustomerInScope`, `IsCustomer`) và 21 trigger: dữ liệu bất biến, vai trò, khảo sát, mã đơn ngắn, phiếu kho không sửa được, tự trừ kho khi đơn hoàn tất, một SĐT chỉ thuộc một khách. |
| `03_orders.sql` | Đặt đơn (`CreateOrder`), voucher, thanh toán mô phỏng, trạng thái, giao hàng, hủy, hoàn tiền (kèm thông báo cho khách). |
| `04_crm.sql` | Địa chỉ giao hàng, khảo sát, phản hồi, xóa mềm tài khoản (`SoftDeleteUser`). |
| `05_access.sql` | Đọc dữ liệu theo quyền, phân nhóm khách, báo cáo doanh thu; view `LoyaltyReconciliation`, `CustomerStats`, `CustomerTiers`; vai trò database cho API. |
| `06_seed.sql` | Dữ liệu nền: vai trò, quyền, 33 tài khoản, cửa hàng, thực đơn (Size M/L + Đá/Nóng), chính sách điểm, ưu đãi, khảo sát, nhà cung cấp, 16 nguyên liệu + phiếu nhập khởi tạo, định mức món, 4 hạng thành viên. |
| `07_demo.sql` | Giao dịch mẫu tạo bằng chính các thủ tục: lịch sử 12 tháng (68 đơn, nhập kho định kỳ, voucher đã dùng / còn hạn / hết hạn) để báo cáo, phân nhóm khách và hạng thành viên có số liệu; đơn, thanh toán, hủy, hoàn tiền, giao hàng, phản hồi, khảo sát; hủy hàng và kiểm kê cuối ngày. <!-- [DEMO-01] --> |

## Tài khoản mẫu

Mật khẩu chung: **`MorningBrew!2026`** (chỉ dùng cho dữ liệu demo).

| Email | Vai trò | Dùng ở |
|---|---|---|
| `owner@morningbrew.test` | Chủ cửa hàng | Admin server (5190), Quản lý CRM (5191), POS |
| `manager.1@morningbrew.test` | Quản lý cửa hàng | Quản lý CRM (5191), POS |
| `staff.1@morningbrew.test` | Thu ngân | POS |
| `lan@morningbrew.test`, `minh@morningbrew.test`, `khach01…khach28@morningbrew.test` | Khách hàng (30 khách) | App khách hàng |

Cửa hàng mẫu mở 00:00–23:59:59 nên đặt hàng demo không phụ thuộc giờ. Ứng dụng phải dùng ID đọc từ database, không gán cứng ID seed.

## Mã lỗi và quy ước

- `51100` sai nghiệp vụ, `51103` không có quyền, `51109` yêu cầu trùng / xung đột; trigger dùng `51001`. API dịch sang thông báo tiếng Việt.
- `ActorID` luôn lấy từ người dùng đã đăng nhập ở API, không lấy từ dữ liệu client gửi lên.
- Các file SQL lưu UTF-8; mở bằng SSMS để giữ đúng tiếng Việt.

## Tài liệu thiết kế

- [Từ điển dữ liệu](docs/data-dictionary.md) (đủ 45 bảng), Word thiết kế database trong `docs/`. Phân tích nghiệp vụ chi tiết nằm trong báo cáo đồ án.
- ERD: `docs/MorningBrewCRM.drawio`, `docs/MorningBrewCRM.mmd`, ảnh `docs/erd-*.png`; sơ đồ UML hệ thống ở `docs/diagrams/`.
- `01_schema.sql`, `docs/schema.json`, từ điển dữ liệu và ERD sinh từ `tools/schema.py` / `tools/document.py` (đủ 45 bảng).

## Kiểm thử

- `tests/test_15_web_admin.sql` (kho, giao hàng, hoàn tiền), `tests/test_16_crm.sql` (thống kê khách, SĐT), `tests/test_17_member_tiers.sql` (hạng thành viên): chạy trong SSMS trên database `MorningBrewCRM_Crm` sau khi chạy fullscript; mỗi kiểm tra in PASS/FAIL, mọi dữ liệu thử được ROLLBACK.
- `tests/verify.py` là bộ kiểm thử SQL cũ (viết cho dữ liệu nhiều chi nhánh), chỉ chạy trên database kiểm thử riêng có chữ `Verify` trong tên.
