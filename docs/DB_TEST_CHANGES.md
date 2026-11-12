# Nhật ký dọn project — db-test

Ngày: **13/11/2026**. Nhánh: **db-test**, tách từ `lmt` tại commit `0db50ed`.

## Đã thay đổi

1. Gom **13 file SQL thành 4 file** trong `database/sql`, giữ nguyên nội dung và thứ tự SQL batch:

   | File mới | File cũ được gom theo thứ tự |
   |---|---|
   | `01_core.sql` | `01_schema`, `02_rules`, `03_orders`, `04_crm`, `05_access` |
   | `02_demo_data.sql` | `06_seed`, `07_demo` |
   | `03_crm_updates.sql` | `08_admin`, `09_shared_prices`, `10_soft_delete`, `11_single_store`, `12_bugfix` |
   | `04_menu_updates.sql` | `13_ui_review` |

   Tên cũ còn trong comment/thông báo SQL để truy nguồn. Không phải file cần chạy thêm. Schema cuối vẫn **41 bảng / 318 cột**, cùng quan hệ và nghiệp vụ. Bộ mới giữ các bước migration trung gian thay vì viết lại schema để tránh làm lệch logic.
2. `Apply.ps1` dùng 4 file mới, mặc định `MorningBrewCRM_Crm` khớp API, kiểm tra công cụ/file trước khi tạo DB. `-NoSeed` bỏ file demo, vẫn giữ hành vi bổ sung 6 nhà cung cấp của script menu.
3. Thay `Upgrade-SharedPrices.ps1` bằng `Upgrade.ps1`: bắt buộc tên DB, preflight một cửa hàng, full backup có checksum và VERIFYONLY trước khi chạy 03 → 04. Không tự phục hồi khi lỗi.
4. Cập nhật các comment chỉ đường SQL trong code API/app, README gốc (đủ 4 project), README database và API: bỏ đường dẫn SQL/tài liệu hết hiệu lực, sửa số bảng và nhóm endpoint đã có.
5. Chuyển đầu ra SQL của generator thiết kế cũ sang `.local/design-export`, tránh sinh lại file cũ trong bộ cài. Giữ nguồn thiết kế và thêm nhãn lịch sử vào architecture/data dictionary; generator tài liệu giữ nhãn này.
6. Bỏ **16 file Markdown** trùng/lỗi thời (danh sách dưới); giữ Excel test case, báo cáo lỗi, Word, ERD và hướng dẫn backup đang dùng.
7. Thêm `database/tests/setup_smoke.sql` để kiểm tra schema, seed và ràng buộc cài đặt trên DB test riêng.

## Tài liệu đã bỏ

- `file md/CAP_NHAT_VA_HUONG_DAN_CHAY.md`
- `file md/HUONG_DAN.md`
- `file md/README.md`
- `file md/UI_UX_CAU_TRUC_VAN_HANH.md`
- `file md/WALKTHROUGH.md`
- `database/file md/HUONG_DAN.md`
- `database/file md/README.md`
- `database/file md/UI_UX_CAU_TRUC_VAN_HANH.md`
- `database/file md/WALKTHROUGH.md`
- `docs/_cu/README_cu.md`
- `docs/legacy-backend.md`
- `docs/integration.md`
- `docs/verification-api-v1.md`
- `database/docs/verification.md`
- `MorningBrewApp/API_CAN_BO_SUNG.md`
- `MorningBrewApp/HUONG_DAN_CHAY.md`

Các bản hướng dẫn chạy trùng đã được thay bằng README gốc/database. Tài liệu EF/JWT, tích hợp nhiều chi nhánh và kết quả test cũ không còn mô tả chính xác bản đang chạy. Nội dung trước cleanup có thể lấy lại từ lịch sử Git ở commit gốc; không cần giữ bản sao trong project.

## Các phần có thể loại bỏ tiếp — CHƯA xóa schema/nghiệp vụ

Kết quả đối chiếu code API, Admin, app, POS, SQL module, khóa ngoại và metadata DB. “Không thấy caller” là kết quả rà soát tĩnh trong repo, không loại trừ công cụ hoặc người dùng bên ngoài gọi trực tiếp.

| Ứng viên | Bằng chứng/phụ thuộc | Điều kiện trước khi bỏ |
|---|---|---|
| 6 procedure `GetMenu`, `GetOrder`, `ListOrders`, `GetCustomerAccount`, `SegmentCustomers`, `SaveAddress` | Không thấy runtime caller trong app hiện tại; menu/đơn dùng `MenuQueries`, `OrderQueries`, `PageReader`, dữ liệu khách qua controller. `GetMenu` vẫn được migration giá dùng để ALTER. | Đổi cả script cài/migration, quyền GRANT và kiểm thử; xác nhận không còn consumer bên ngoài. |
| `CanScope.@AreaID` | Tham số không được dùng trong thân hàm hiện hành; caller vẫn truyền đối số. | Sửa đồng bộ chữ ký và tất cả câu SQL/API gọi hàm. |
| `CustomerInScope.@Scope`, `@AreaID`, `@BranchID` | Thân hàm một cửa hàng chỉ cần CustomerID; caller vẫn truyền đủ. | Cập nhật mọi caller, kiểm tra lại quyền đọc dữ liệu. |
| `BranchProductVariants.SellingPrice` | Tính giá dùng `ProductVariants.BasePrice`; Admin vẫn ghi đồng bộ cột giá cũ. | Bỏ nhánh ghi cũ rồi sửa schema/seed/test; giữ bảng vì `IsAvailable` và quan hệ sản phẩm vẫn cần. |
| `BranchModifierOptions.ExtraPrice` | Tính giá dùng `ModifierOptions.ExtraPrice`; cột giá riêng không còn quyết định số tiền. | Kiểm tra consumer ngoài repo, sửa schema/seed; giữ quan hệ tình trạng modifier. |
| `Areas` / các trường phạm vi Area | Mô hình một cửa hàng không cần quản lý khu vực, nhưng `Branches`, `Surveys`, `Promotions` còn FK; `Branches.AreaID` còn đi qua SQL đặt đơn/phạm vi. | Cần migration riêng và sửa constraints/DTO/quyền; không xóa bảng trực tiếp. |
| `CustomerAddresses` và `SaveAddress` | App hiện truyền địa chỉ giao hàng trực tiếp, không truyền AddressID; SQL CreateOrder và dữ liệu demo vẫn dùng địa chỉ lưu. | Chốt có giữ tính năng sổ địa chỉ; sửa CreateOrder, seed/demo và test trước khi bỏ. |
| `Version` ở `Branches`, `Products`, `Promotions`, `CustomerPromotions`, `Deliveries` | Chưa thấy consumer dùng để kiểm soát cập nhật đồng thời. | Rà soát contract ngoài repo và định hướng concurrency. **Giữ Users.Version/Customers.Version** vì ETag hồ sơ đang dùng. |
| Generator thiết kế/ERD cũ | Mô tả AreaManager/nhiều chi nhánh, không tái sinh bộ cài hiện tại. | Có thể loại bỏ sau khi chuyển tài liệu sang sinh từ schema hiện hành; hiện giữ để tránh mất nguồn báo cáo đồ án. |
| Bộ test tích hợp cũ | Giả định quản lý khu vực/nhiều cửa hàng và seed cũ. | Cập nhật test theo CRM một cửa hàng; không xóa coverage khi chưa có bộ thay thế. |

Không loại bỏ snapshot tên/giá trên đơn, thông tin người nhận, lịch sử điểm/voucher, request key/hash, token hash, FK/trigger hoặc view `LoyaltyReconciliation`. Các phần này bảo toàn lịch sử, tính đúng số tiền, idempotency, phân quyền và đối chiếu dữ liệu.

## DB đang cấu hình và phạm vi tác động

- Development của API trỏ `(localdb)\MSSQLLocalDB` / **`MorningBrewCRM_Crm`**; cấu hình runtime có thể bị ghi đè bởi secrets/environment.
- Cleanup thay cấu trúc file và công cụ, không tự áp dụng migration lên DB đang dùng, không xóa MDF/LDF/backup và không đổi connection string.
- Gộp file không làm giảm số bảng hay tối ưu tốc độ truy vấn. Mục tiêu là có một luồng cài/nâng cấp rõ ràng, tránh nhiều bản tài liệu mâu thuẫn.
- Các thay đổi DB ứng viên ở trên chỉ là đề xuất; cần làm một đợt migration riêng sau khi thống nhất phạm vi.

## Kiểm tra đợt này

| Kiểm tra | Kết quả |
|---|---|
| So sánh toàn bộ SQL trước/sau gộp | **79 batch giống nguyên bản**, đúng thứ tự, từ 13 nguồn sang 4 file. Chỉ thêm comment provenance và dấu GO phân tách. |
| Syntax công cụ | Python `schema.py`, `seed.py`, `document.py` và parser PowerShell của Apply/Upgrade: **PASS**. |
| Link Markdown còn lại | **PASS**, không có link file bị thiếu. Các tên file cũ trong nhật ký/comment SQL dùng để truy nguồn. |
| Build solution | `dotnet build MorningBrewCRM.slnx --no-restore -v minimal`: **thành công, 0 warnings / 0 errors**, đủ API/Admin/app khách/POS. |
| Cài mới đầy đủ | `MorningBrewCRM_DbTest_20261113`: **PASS**, 41 bảng / 318 cột; 4 vai trò, 1 cửa hàng, 30 khách, 10 sản phẩm / 17 biến thể, 6 nhà cung cấp. |
| Smoke SQL | **PASS**: xóa mềm, trigger mã đơn, loại bỏ AreaManagers/SetBranchPrice, FK/CHECK được bật và trusted, cấu hình Đá/Nóng, tổng điểm cache khớp ledger, đủ SQL roles. |
| Nâng cấp lặp trên DB test đã cài | **PASS**: full backup + RESTORE VERIFYONLY thành công; số đơn, tổng tiền, chi tiết đơn, điểm, số user/khách/phản hồi khảo sát không đổi; smoke chạy lại đạt. |
| Cài đè DB test đã tồn tại | **Từ chối đúng thiết kế**, không chạy seed/migration tiếp. |
| `-NoSeed` | `MorningBrewCRM_DbTestNoSeed_20261113`: **PASS**, 41 bảng / 318 cột, không có user/khách/sản phẩm mẫu; vẫn có cấu hình Nhiệt độ (2 lựa chọn) và 6 NCC theo hành vi script menu cũ. |

Đã giữ lại 2 DB test và backup nâng cấp trong `database/.local/` để có thể kiểm tra tiếp; các file này bị Git ignore. Kết nối test dùng named pipe của instance LocalDB đang chạy, không hardcode pipe tạm thời vào cấu hình project.

Không áp dụng 03/04 lên `MorningBrewCRM_Crm`. Chưa chạy lại toàn bộ test nghiệp vụ/UI từ Excel; build và smoke cài đặt không xác nhận tất cả màn hình không có lỗi. Các test nhiều chi nhánh cũ vẫn cần cập nhật riêng.

Nhánh được chuẩn bị ở local; chưa push GitHub trong đợt này.
