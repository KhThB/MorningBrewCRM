# Database MorningBrew CRM

Project dùng **một database SQL Server**, mặc định **`MorningBrewCRM_Crm`** trên `(localdb)\MSSQLLocalDB`, khớp cấu hình Development của API. Admin, app khách hàng và POS gọi API; không kết nối database trực tiếp. Sau cài đặt đầy đủ, schema có **41 bảng / 318 cột** (bao gồm cột tính toán và rowversion).

## Bộ cài: 4 file SQL

| File | Nội dung | Khi sử dụng |
|---|---|---|
| [01_core.sql](sql/01_core.sql) | Bảng, khóa, index, hàm quyền, trigger, procedure đơn hàng/CRM và quyền SQL | Chỉ cài mới |
| [02_demo_data.sql](sql/02_demo_data.sql) | Tài khoản và dữ liệu demo của một cửa hàng | Tùy chọn, chỉ cài mới |
| [03_crm_updates.sql](sql/03_crm_updates.sql) | Nhà cung cấp, giá chung, xóa mềm, phạm vi một cửa hàng và sửa lỗi CRM | Cài mới hoặc nâng cấp |
| [04_menu_updates.sql](sql/04_menu_updates.sql) | Size, Đá/Nóng, tình trạng món và nhà cung cấp mẫu | Cài mới hoặc nâng cấp |

Đây là gom file, giữ nguyên lệnh và thứ tự batch của 13 script cũ. Các bảng vẫn tách theo nghiệp vụ và dùng khóa ngoại. Không gộp khách hàng, đơn hàng và khảo sát vào một bảng; không cần chia thành nhiều database.

## Cài mới

Cần .NET 10 để chạy app, SQL Server 2022+ / LocalDB, PowerShell và `sqlcmd`. Tại thư mục gốc repo:

```powershell
./database/tools/Apply.ps1
```

Mặc định có seed: 4 vai trò, 1 cửa hàng, 30 khách hàng; menu và dữ liệu CRM mẫu. Tài khoản `owner@morningbrew.test`, `manager.1@morningbrew.test`, `staff.1@morningbrew.test`, `lan@morningbrew.test`; mật khẩu demo `MorningBrew!2026`.

Installer kiểm tra file đầu vào và dừng nếu database đã tồn tại. MDF/LDF được tạo trong `.local/` (đã ignore Git). Lỗi giữa chừng để lại database mới để kiểm tra, không tự xóa hay tự rollback toàn bộ các file.

```powershell
./database/tools/Apply.ps1 -Database MorningBrewCRM_Test
./database/tools/Apply.ps1 -Database MorningBrewCRM_Empty -NoSeed
```

`-NoSeed` bỏ file `02_demo_data.sql`, không tạo tài khoản/khách/menu mẫu. File `04` vẫn tạo nhóm Nhiệt độ với 2 lựa chọn và thêm 6 nhà cung cấp nếu bảng rỗng; hành vi này được giữ từ bộ cũ, nên đây không phải chế độ mọi bảng đều rỗng.

Nếu dùng SQL Server khác, truyền `-Server`. Installer dùng đường dẫn trên máy chạy script: với server từ xa, tạo DB và backup trong thư mục do SQL Server quản lý rồi áp dụng các file trên theo thứ tự phù hợp. Không di chuyển/xóa MDF/LDF khi đang attach.

## Database đã có

Chỉ dùng cho bản CRM một cửa hàng có schema/procedure lõi. Đóng API/Admin/app/POS để ngừng ghi dữ liệu, rồi chạy rõ tên database:

```powershell
./database/tools/Upgrade.ps1 -Database MorningBrewCRM_Crm
```

Helper kiểm tra schema cơ bản/phạm vi một cửa hàng, tạo full backup `COPY_ONLY, CHECKSUM` và chạy `RESTORE VERIFYONLY` trước khi áp dụng **03 → 04**. Nếu backup thất bại thì chưa chạy migration. Nếu migration lỗi, dừng để kiểm tra; không tự restore. `VERIFYONLY` không thay thế việc thử phục hồi trên database riêng. Backup nằm trong `.local/`, cùng yêu cầu quyền ghi của SQL Server như installer.

Không chạy lại `01` hoặc `02` trên DB có dữ liệu. `03` chứa bước chuyển phạm vi sang một cửa hàng; không dùng để tự động chuyển dữ liệu nhiều chi nhánh. `04` thay đổi cấu hình menu và có thể bổ sung giá/nhà cung cấp mẫu: xem nội dung trước khi nâng cấp DB đã tùy biến. Helper mới thay `Upgrade-SharedPrices.ps1` cũ chỉ cập nhật giá.

## Cấu hình API

Khóa cấu hình: `ConnectionStrings:MorningBrew`. Giá trị Development:

```text
Server=(localdb)\MSSQLLocalDB;Database=MorningBrewCRM_Crm;Integrated Security=True;TrustServerCertificate=True
```

User secrets hoặc biến môi trường `ConnectionStrings__MorningBrew` có thể ghi đè. LocalDB thuộc tài khoản Windows chạy ứng dụng. Không commit chuỗi kết nối chứa mật khẩu riêng. Không đổi cấu hình API sang DB kiểm thử khi chưa chủ động chọn môi trường chạy.

## Kiểm tra và thiết kế

Trên database test mới đã cài đầy đủ seed:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Test -E -C -b -f 65001 -i database/tests/setup_smoke.sql
```

- [Nhật ký thay đổi và ứng viên dọn tiếp](../docs/DB_TEST_CHANGES.md).
- [Sao lưu/phục hồi trong web Admin](../docs/backup-restore.md).
- [Thiết kế nghiệp vụ](docs/architecture.md), [từ điển dữ liệu](docs/data-dictionary.md), Word/ERD và `docs/schema.json` là snapshot thiết kế trước migration một cửa hàng; không phải schema cuối cùng. Bộ SQL là nguồn cài đặt hiện hành.
- `tools/schema.py` và `tools/seed.py` xuất SQL thiết kế cũ vào `.local/design-export/`; không tái sinh bộ cài hiện hành. `schema.py` vẫn cập nhật metadata thiết kế `docs/schema.json`; `document.py` dùng metadata đó để sinh tài liệu lịch sử.
- Bộ kiểm thử `database/tests/verify.py`, `tests/integration.py`, `tests/api_contract.py` còn giả định mô hình nhiều chi nhánh; không coi kết quả cũ là xác nhận cho bản này.
