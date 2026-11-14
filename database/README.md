# Morning Brew CRM — database

SQL Server 2022+ (hoặc LocalDB 2022). Mô hình **một cửa hàng** (Morning Brew Thủ Đức), **43 bảng** sau khi chạy đủ 14 script:
41 bảng CRM/đơn hàng/khảo sát/nhà cung cấp và 2 bảng kho nguyên liệu (`Ingredients`, `InventoryTransactions`).
Database dùng cho cấu hình Development: **`MorningBrewCRM_Crm`** trên `(localdb)\MSSQLLocalDB`.

## Cài mới hoặc nâng cấp — một file, không cần PowerShell

Mở SSMS → kết nối `(localdb)\MSSQLLocalDB` → File → Open **`database/sql_ssms/morningbrew_fullscript.sql`** → Execute (F5).
Không cần chọn database trước. File tự nhận biết:

| Tình trạng | File làm gì |
|---|---|
| Chưa có database / database trống | **Cài mới**: tạo `MorningBrewCRM_Crm`, chạy 01 → 14 (gồm dữ liệu mẫu). |
| Database MorningBrew đang dùng | **Nâng cấp**: sao lưu `.bak` (CHECKSUM + VERIFYONLY) rồi chạy 08 → 14; không chạy lại 01–07, không xóa dữ liệu. |

Thành công khi tab Messages có `=== HOÀN TẤT ...` và bảng kiểm tra có `SoBang = 43`. Thấy `>>> DỪNG` thì phần sau không chạy;
đọc dòng lỗi đỏ đầu tiên. Đóng API, web Admin, app, POS trước khi nâng cấp. Tên database khác: Find and Replace `MorningBrewCRM_Crm`
trong file và sửa `ConnectionStrings:MorningBrew` của API.

Cách khác (PowerShell + `sqlcmd`): `./database/tools/Apply.ps1` chạy chính file fullscript ở trên. Mặc định tạo mới `MorningBrewCRM_Crm` (file MDF/LDF trong `database/.local`); `-Database <tên>` đổi tên, `-NoSeed` bỏ dữ liệu mẫu, `-Upgrade` nâng cấp database đang dùng (tự sao lưu rồi chạy 08–14).

Chạy lẻ một script trong SSMS: nhớ chọn đúng database `MorningBrewCRM_Crm` ở ô database (không để `master`). `14_inventory.sql` tự dừng
nếu đang chọn nhầm database.

## Thứ tự script (`database/sql`)

| Script | Nội dung |
|---|---|
| `01_schema.sql` | Bảng, CHECK, UNIQUE, khóa ngoại, index; chỉ chạy trên database trống. |
| `02_rules.sql` | Hàm phân quyền, trigger bảo vệ dữ liệu bất biến, vai trò, khảo sát. |
| `03_orders.sql` | Đặt đơn (`CreateOrder`), voucher, thanh toán mô phỏng, trạng thái, giao hàng, hủy, hoàn tiền. |
| `04_crm.sql` | Địa chỉ giao hàng (`SaveAddress`), khảo sát, phản hồi. |
| `05_access.sql` | Đọc dữ liệu theo quyền, phân nhóm khách, báo cáo doanh thu. |
| `06_seed.sql` | Vai trò, quyền, tài khoản mẫu, cửa hàng, thực đơn, chính sách điểm (dữ liệu mẫu). |
| `07_demo.sql` | Đơn hàng, phản hồi, khảo sát mẫu tạo bằng chính các procedure (dữ liệu mẫu). |
| `08_admin.sql` | Nhà cung cấp và quyền cho web Admin. |
| `09_shared_prices.sql` | Giá dùng chung khi tạo đơn / đọc thực đơn; giữ giá lịch sử trên đơn cũ. |
| `10_soft_delete.sql` | Xóa mềm tài khoản / khách hàng, ẩn danh dữ liệu cá nhân. |
| `11_single_store.sql` | Chế độ một cửa hàng: nhiều quản lý, quyền `Customers.Manage`, bỏ vai trò AreaManager. |
| `12_bugfix.sql` | Sửa lỗi đợt 1: thông báo lỗi khảo sát, mã đơn ngắn `MB261108-0028`. |
| `13_ui_review.sql` | Góp ý giao diện 09/11: đồ uống chỉ còn Size M/L + Đá/Nóng, tình trạng còn hàng, nhà cung cấp mẫu. |
| `14_inventory.sql` | Kho nguyên liệu: 2 bảng, trigger chống sửa/xóa phiếu kho, quyền `Inventory.Manage`, 16 nguyên liệu + phiếu mẫu. |

08–14 chạy lại nhiều lần được (dùng cho nâng cấp). 01–07 chỉ dùng khi cài mới.

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
- Chạy bằng `sqlcmd` thì dùng `-I -b -f 65001` (quoted identifier, dừng khi lỗi, đọc UTF-8).

## Tài liệu thiết kế

- [Phân tích nghiệp vụ](docs/architecture.md), [từ điển dữ liệu](docs/data-dictionary.md), Word thiết kế database trong `docs/`.
- ERD: `docs/MorningBrewCRM.drawio`, `docs/MorningBrewCRM.mmd`, ảnh `docs/erd-*.png`; sơ đồ UML hệ thống ở `docs/diagrams/`.
- Từ điển dữ liệu và ERD sinh từ `tools/schema.py` / `tools/document.py` cho 41 bảng lõi; 2 bảng kho mô tả trực tiếp trong `14_inventory.sql`.
- `tests/verify.py` là bộ kiểm thử SQL cũ (viết cho dữ liệu nhiều chi nhánh), chỉ chạy trên database kiểm thử riêng có chữ `Verify` trong tên.
