# Sổ tay lỗi & việc đã xử lý — MorningBrew CRM

Bản tóm tắt để bàn giao. Các mục dưới đây là lỗi đã gặp, nguyên nhân, cách xử lý, và checklist demo.

---

## Tóm tắt nhanh

| # | Vấn đề | Nguyên nhân ngắn | Đã xử lý |
|---|--------|------------------|----------|
| 1 | `/Manager/Inventory` báo 404 | Chỉ có UI, chưa có API và bảng | Migration `14_inventory.sql` + `AdminInventory.cs` (7 API) |
| 2 | `Invalid column name 'DeletedAt'` | DB backup cũ, thiếu script 09–13 | Chạy SQL 09–14 vào `MorningBrewCRM_Crm` |
| 3 | POS: “Courier name and phone are required” | Cho bấm giao khi trống tên/SĐT; API không chặn sớm | Khóa nút trên POS + validate backend, báo tiếng Việt |
| 4 | Font tiếng Việt vỡ (`TÃºi giáº¥y`…) | `sqlcmd` không `-f 65001` | Nạp lại chuỗi Unicode với `-f 65001` |
| 5 | Crash `CourierName.Trim()` | Gọi `.Trim()` khi `CourierName` = null | Đổi thành `input.CourierName?.Trim()` |
| 6 | Kho: sắp hết không lên đầu; kiểm kê 0 bị chặn; món ngừng dùng vẫn ghi phiếu | Validation và sort chưa đủ | Sort `is_low DESC`; kiểm kê cho = 0; chặn phiếu khi `IsActive = 0` |
| 7 | Nhập kho không cập nhật nhà cung cấp | Chỉ lưu `SupplierID` vào phiếu, không cập nhật `Ingredients` | Update `SupplierID` khi nhập; form tự điền NCC và giá |
| 8 | Phân trang trang 2 lỗi 400 | `PageReader` không đọc boolean | Đọc `GetBoolean()`, quy về 1/0 |

**Chưa làm:** kho chưa tự trừ theo công thức pha chế (BOM). Thủ kho xuất dùng hoặc kiểm kê cuối ca.

**Build:** Api, Admin, POS, App — 0 lỗi, 0 cảnh báo. Luồng login, kho, POS, delivery, chuyển khoản, báo cáo đã chạy ổn.

---

## Chi tiết từng lỗi

### 1. Kho nguyên liệu — 404
- UI `/Manager/Inventory` có sẵn, backend và DB chưa có.
- Đã thêm `14_inventory.sql`: bảng `Ingredients`, `InventoryTransactions`; 16 nguyên liệu + 21 phiếu mẫu.
- `AdminInventory.cs`: thống kê, danh sách, thêm/sửa, bật/tắt, ghi phiếu, lịch sử.

### 2. Thiếu cột `DeletedAt`
- File `.mdf` là backup cũ, chưa chạy `10_soft_delete.sql` (trong bộ 09–13).
- Đã chạy script 09–14. Bảng `Users` đã có `DeletedAt`.

### 3. Giao shipper bị crash SQL
- Đơn Delivery sang `Delivering` bắt buộc tên + SĐT.
- POS vẫn cho bấm khi ô trống; ô chưa `oninput`; API đẩy dữ liệu rỗng xuống SQL.
- POS (`Orders.razor`): khóa nút khi thiếu tên hoặc SĐT, `@bind:event="oninput"`, giao xong xóa ô.
- API (`PosController.cs`): kiểm tra trước, trả lỗi tiếng Việt, không crash SQL.

### 4. Mojibake tiếng Việt trên trang Kho
- PowerShell chạy `sqlcmd` không `-f 65001` → file UTF-8 bị đọc theo Windows-1252.
- Đã chuẩn hóa lại chuỗi trên `Ingredients`, `Suppliers`, `InventoryTransactions` với `-f 65001`.

### 5. `NullReferenceException` khi đổi trạng thái đơn
- `input.CourierName.Trim()` chạy cả bước không có shipper (Xác nhận, Pha chế…).
- Sửa ở `PosController.cs` và `AdminOrders.cs`: `input.CourierName?.Trim()`.

### 6. Bất cập kho (khi test)
- **Sắp hết:** API chưa select/sort `is_low` → thêm `is_low` và `ORDER BY is_low DESC`, nhãn đỏ lên đầu.
- **Kiểm kê = 0:** rule cũ bắt > 0.001 → nhập/xuất vẫn > 0, kiểm kê cho = 0.
- **Ngừng dùng:** món `IsActive = 0` vẫn ghi phiếu được → SQL chặn và báo lỗi.
- **BOM:** chưa có bảng công thức. Bán POS không tự trừ cà phê/sữa.

### 7. Nút “Nhập kho” không gắn nhà cung cấp
- Phiếu lưu `SupplierID`, nhưng `UPDATE Ingredients` không cập nhật cột đó. Dropdown cũng không kèm NCC.
- Backend: nhập kho có chọn công ty thì cập nhật luôn `Ingredients.SupplierID`.
- UI (`Inventory.cshtml`): chọn nguyên liệu → tự điền NCC quen và giá nhập gần nhất. Xuất/kiểm kê hiện tồn hiện tại ngay.

### 8. Phân trang keyset lỗi với boolean
- Sort `["is_low", "ingredient_id"]`. `is_low` là boolean; `PageReader` chỉ đọc số/chuỗi, gọi `.GetString()` → HTTP 400.
- `PageReader.cs`: thêm `GetBoolean()`, quy về 1/0 để so sánh SQL.

---

## Checklist demo

Tài khoản dùng chung mật khẩu `MorningBrew!2026`.

| Vai trò | Email |
|---------|--------|
| Chủ quán | `owner@morningbrew.test` |
| Thu ngân | `staff.1@morningbrew.test` |
| Khách | `lan@morningbrew.test` |

### A. Bật máy
1. LocalDB `(localdb)\MSSQLLocalDB`, DB `MorningBrewCRM_Crm` đã có script 01–14.
2. API trước — `http://localhost:5029/health` trả `{"status":"ready"}`.
3. Admin `http://localhost:8080`.
4. POS desktop, rồi app khách.

### B. Admin
- **Dashboard:** biểu đồ 12 tháng, doanh thu ngày, top 5, việc cần làm.
- **Kho `/Manager/Inventory`:**
  - 4 ô thống kê; món dưới định mức viền đỏ, lên đầu danh sách.
  - Lật trang 1 → 2 không lỗi.
  - Taskbar **Nhập kho:** chọn món → NCC và đơn giá tự điền → ghi phiếu → cột NCC và tồn cập nhật (không còn `—`).
  - Xuất: tồn giảm. Kiểm kê nhập `0` được lưu. Ngừng dùng rồi ghi phiếu phải bị chặn.
  - Tab lịch sử thấy phiếu vừa ghi.
- **Đơn `/Manager/Orders`:** lọc tab, đổi trạng thái, thu COD, giao thất bại/giao lại, hoàn tiền.
- **Khách / ưu đãi / tài khoản:** cộng trừ điểm, xem voucher, đổi vai trò Owner / Manager / Staff.

### C. POS
- Lên đơn: bắt buộc Size + Đá/Nóng. Tra SĐT `0901234567` để gắn hội viên.
- Thu tiền ngay → đơn đã thanh toán.
- Hàng chờ tự tải lại ~15 giây: Xác nhận → Pha chế → Sẵn sàng → Hoàn tất.
- Delivery ở bước Sẵn sàng: nút “Giao cho shipper” khóa khi thiếu tên hoặc SĐT; nhập đủ thì giao được.
- Đơn chuyển khoản từ app: đổi trạng thái không crash.

### D. App khách
- Chọn nhận tại quầy hoặc giao tận nơi, thêm giỏ.
- **COD:** đơn hiện trên POS.
- **Chuyển khoản:** trạng thái “Chờ thanh toán” → bấm thanh toán mô phỏng → “Chờ quán xác nhận” → POS thấy đơn để pha.
- Hoàn tất: điểm tăng; đánh giá được.

---

**Kết luận:** các lỗi trên đã xử lý. Hệ thống sẵn sàng demo, trừ phần kho tự trừ theo công thức (chưa làm).