# Morning Brew CRM — Cấu trúc vận hành để thiết kế UI/UX

> Tài liệu tổng hợp từ 2 repo trong `MorningBrew(1)`. Mục đích: cho người làm UI/UX biết **hệ thống chạy thế nào, ai dùng màn hình nào, mỗi màn hình gọi API gì, dữ liệu/trạng thái nào phải hiển thị**, để thiết kế không vẽ chức năng mà backend không có.

---

## 0. Chọn repo nào làm chuẩn

| Repo | Thực tế bên trong | Dùng cho UI/UX? |
|---|---|---|
| `MorningBrewCRM-integration-crm-sqlserver` | Solution đầy đủ: `MorningBrew.Api` (SQL Server, `/v1`), `MorningBrew.Admin` (Razor Pages: quản trị + cổng khách), `MorningBrew.Pos` (khung Blazor trống), `database/` (43 bảng, procedure, seed), `docs/` | **CHUẨN** — thiết kế theo repo này |
| `MorningBrewCRM-Admin` | Chỉ có `MorningBrew.Api` bản cũ (Backend2: EF **InMemory**, JWT, route `/api/...`, DTO kiểu cũ). README + file `.cmd` nhắc tới `Admin_MC/`, `database/`, `Shared/` **nhưng các thư mục này không có** trong repo | Chỉ tham khảo lịch sử. **Không** dùng route `/api/...` của repo này |

Lưu ý thêm:
- `MorningBrewCRM-Admin/TOAN_BO_TAI_KHOAN_MORNING_BREW.txt` chứa danh sách tài khoản — không đưa vào mockup/slide công khai.
- Route cũ `/api/...` đã bị thay bằng `/v1/...` (xem `docs/api-v1.md`).

---

## 1. Kiến trúc vận hành

```
                ┌──────────────────────────┐
  Trình duyệt → │ MorningBrew.Admin        │  Razor Pages, cookie phiên (HttpOnly, 8h)
  (quản lý,     │  /Index  /Workspace/{x}  │  KHÔNG kết nối DB
   khách hàng)  │  /Portal/{x}  /Answer/id │
                └────────────┬─────────────┘
                             │ HTTP + Bearer token (CrmApi.cs)
                ┌────────────▼─────────────┐
  POS (sắp làm)→│ MorningBrew.Api  /v1     │  kiểm tra vai trò + phạm vi mỗi request
                └────────────┬─────────────┘
                             │ SqlClient + stored procedure
                ┌────────────▼─────────────┐
                │ SQL Server: MorningBrewCRM│  43 bảng, seed demo
                └──────────────────────────┘
```

**Chạy để xem giao diện thật**
1. `.\database\tools\Apply.ps1` (tạo DB + seed, chỉ lần đầu).
2. Chạy `MorningBrew.Api` và `MorningBrew.Admin` profile `https`.
3. Admin: `https://localhost:7287` · Swagger: `https://localhost:7132/swagger`.
4. Mật khẩu seed `MorningBrew!2026`: `owner@`, `area.east@`, `manager.1@`, `staff.1@`, `lan@morningbrew.test`.

Nguyên tắc ảnh hưởng UX:
- Không có dữ liệu giả: API/SQL tắt → giao diện **phải hiện lỗi**, không hiện số mẫu.
- Client không tự quyết quyền/phạm vi; UI chỉ ẩn/hiện cho gọn, API mới là nơi chặn.

---

## 2. Vai trò → ứng dụng → phạm vi dữ liệu

| Vai trò (mã) | Tên hiển thị | Vào đâu | Phạm vi dữ liệu |
|---|---|---|---|
| `Owner` | Chủ chuỗi | Admin (đủ menu) | Toàn chuỗi |
| `AreaManager` | Quản lý khu vực | Admin (không có NCC / Tài khoản / Phân quyền) | Các chi nhánh trong khu vực |
| `BranchManager` | Quản lý chi nhánh | Admin (như trên) | Chi nhánh được giao |
| `Staff` | Nhân viên | **POS** (chưa có UI) — bị chặn khỏi Admin | Chi nhánh đang phân công |
| `Customer` | Khách hàng | Cổng thành viên `/Portal` | Dữ liệu của chính mình |

Quyền vận hành (RolePermissions, Owner bật/tắt cho AreaManager/BranchManager):
`Orders.Create`, `Orders.Manage`, `Orders.Cancel`, `Orders.Read`, `Payments.Manage`, `Refunds.Manage`, `Menu.Manage`, `Customers.Lookup`, `Customers.Read`, `Promotions.Manage`, `Surveys.Manage`, `Feedbacks.Manage`, `Reports.Read`, `Staff.Manage`.

➡ Khi thiết kế: mỗi màn hình quản trị cần **trạng thái "không có quyền"** (403) và **dòng phụ "Dữ liệu trong phạm vi bạn phụ trách"**.

---

## 3. Sitemap

### 3.1 Web quản trị (Owner / AreaManager / BranchManager)

```
/Login
/Index ─────────────── Tổng quan
/Workspace/shifts ──── Lịch làm việc
/Workspace/customers ─ Khách hàng
/Workspace/feedbacks ─ Phản hồi
/Workspace/surveys ─── Khảo sát  (+ ?surveyId=… → Kết quả khảo sát)
/Workspace/promotions  Ưu đãi
/Workspace/products ── Sản phẩm (theo biến thể/kích cỡ)
/Workspace/staff ───── Nhân sự
/Workspace/reports ─── Báo cáo doanh thu
── chỉ Owner ──
/Workspace/suppliers ─ Nhà cung cấp
/Workspace/accounts ── Tài khoản
/Workspace/permissions Phân quyền theo vai trò
/AccessDenied, /Error
```

### 3.2 Cổng thành viên (Customer)

```
/Portal/profile ───── Hồ sơ thành viên
/Portal/loyalty ───── Lịch sử điểm
/Portal/wallet ────── Ví voucher
/Portal/promotions ── Ưu đãi (đổi điểm)
/Portal/feedbacks ─── Phản hồi của tôi (+ gửi góp ý)
/Portal/surveys ───── Khảo sát dành cho tôi
/Answer/{id} ──────── Trả lời khảo sát
```

### 3.3 POS (chưa có UI — cần thiết kế mới)
Hiện chỉ là template Blazor (Home/Counter/Weather). Nghiệp vụ đã có ở **stored procedure SQL** nhưng **chưa có endpoint `/v1`**: `GetMenu`, `LookupCustomer`, `CreateOrder`, `StartPayment`, `SimulatePayment`, `AdvanceOrder`, `UpdateDelivery`, `CancelOrder`, `RequestRefund`, `ListOrders`, `GetOrder`. Thiết kế POS được, nhưng phải đánh dấu "cần API".

Màn hình POS đề xuất: Đăng nhập → Chọn món (menu chi nhánh, tùy chọn/modifier) → Tra thành viên bằng SĐT → Giỏ & voucher → Thu tiền (Cash/Simulator) → Hàng đợi đơn (Pending → Confirmed → Preparing → Ready → Delivering → Completed) → Ca của tôi.

---

## 4. Đặc tả từng màn hình (hiện trạng)

Ký hiệu: **R** = đọc, **W** = ghi. Tất cả list dùng `pageSize` (mặc định 50, tối đa 100) + `pageToken`.

### Đăng nhập — `/Login`
- W `POST /v1/auth/login` `{username(email), password}` → token phiên 8h.
- UX: lỗi sai mật khẩu / tài khoản bị khóa; không có "đăng ký", không có "quên mật khẩu" (không có API).

### Tổng quan — `/Index`
- R `GET /v1/dashboard/overview` → 4 số: `customers`, `pending_feedbacks`, `open_surveys`, `branches`.
- Lối tắt: Xử lý phản hồi, Chăm sóc khách hàng, Theo dõi khảo sát.
- Gợi ý: badge vai trò; card "Phản hồi cần xử lý" nên là CTA nổi bật.

### Khách hàng — `customers`
- R `GET /v1/customers?q=` (họ tên, email, SĐT).
- W (Owner) `POST /v1/accounts` role=Customer — họ tên ≤120, email, SĐT ≤20, mật khẩu ≥10.
- W `POST /v1/customers/{id}:adjustPoints` `{delta (±), note 3–300, request_key}`.
- Lưu ý: **CustomerID ≠ UserID**. Khóa tài khoản làm ở "Tài khoản".

### Phản hồi — `feedbacks`
- R `GET /v1/feedbacks`.
- W `POST /v1/feedbacks/{id}:reply` `{status: Processing|Resolved, response_content ≤4000}`.
- Trạng thái: New → Processing → Resolved. Khách thấy nội dung trả lời trong cổng.

### Khảo sát — `surveys`
- R `GET /v1/surveys`, `GET /v1/surveys/{id}/results`.
- W `POST /v1/surveys` (Draft) → `:publish` → `:invite {customer_id}` → `:close`.
- Trường: tiêu đề ≤250, mô tả ≤2000, câu hỏi, phạm vi (Chain/Area/Branch + area/branch), thời gian bắt đầu/kết thúc.
- API hỗ trợ 4 loại câu: `Rating`, `Text`, `SingleChoice`, `MultipleChoice`; **form Admin hiện chỉ tạo Rating 1–5** → cơ hội cải thiện UI (form builder).
- Không mở lại khảo sát đã đóng → nút "Đóng" cần xác nhận.

### Ưu đãi — `promotions`
- R `GET /v1/promotions`.
- W `POST /v1/promotions`: tên, mô tả, `discount_type` Amount|Percent, giá trị, `max_discount` (bắt buộc nếu Percent), đơn tối thiểu, điểm đổi (trống = chỉ quản lý tặng), tổng số lượng, số lượt/khách, phạm vi, thời gian.
- W `POST /v1/promotions/{id}:issue {customer_id, request_key}` — cấp voucher.
- W `PUT /v1/promotions/{id}/active` — dừng cấp mới / hoạt động.

### Sản phẩm — `products`
- R `GET /v1/variants` (mỗi dòng = 1 kích cỡ + giá chung).
- W (Owner) `POST /v1/products` {tên, mô tả, danh mục, size, giá}; `PUT /v1/variants/{id}/price`; `PUT /v1/products/{id}/active`.
- Giá **chung toàn chuỗi**, không có giá riêng chi nhánh trên UI. Không xóa — chỉ "Ngừng bán".

### Nhân sự — `staff` · Lịch làm việc — `shifts`
- R `GET /v1/staff`, `GET /v1/shifts`.
- W `POST /v1/shifts {user_id, branch_id, starts_at, ends_at}`; `POST /v1/shifts/{id}:cancel`.
- Ràng buộc: từ chối ca trùng / ngoài phạm vi; hỗ trợ ca qua đêm; không sửa ca → "hủy rồi tạo lại".
- Gợi ý: hiển thị dạng **lịch tuần** thay vì bảng.

### Báo cáo doanh thu — `reports`
- R `GET /v1/reports/revenue?fromTime&toTime&branchId` (mặc định 30 ngày). Cột: đơn hoàn tất, doanh thu, đã hoàn tiền, doanh thu thuần; không phân trang.
- Gợi ý: bộ lọc khoảng ngày + chi nhánh, biểu đồ cột theo chi nhánh.

### Nhà cung cấp — `suppliers` (Owner)
- R `GET /v1/suppliers`; W `POST /v1/suppliers {code ≤30, name, phone, email, product_group}`; `PUT …/active` (Tạm ngưng / Đang hợp tác).

### Tài khoản — `accounts` (Owner)
- R `GET /v1/accounts`; W `POST /v1/accounts` (role + branch_id cho Staff/BranchManager, area_id cho AreaManager); `PUT /v1/accounts/{id}/active` — khóa sẽ **thu hồi mọi phiên**.

### Phân quyền — `permissions` (Owner)
- R `GET /v1/permissions`; W `PUT /v1/roles/{role}/permissions/{code} {allowed}` — chỉ AreaManager/BranchManager.
- Gợi ý: ma trận vai trò × quyền dạng toggle.

### Cổng khách
| Màn hình | API | Hành động |
|---|---|---|
| Hồ sơ | `GET/PATCH /v1/me/profile` (+ `If-Match` ETag) | Sửa họ tên, SĐT, ngày sinh, giới tính (không đổi email) |
| Điểm | `GET /v1/me/loyalty` | Xem lịch sử Earn/Redeem/Reverse/Adjust |
| Ví voucher | `GET /v1/me/wallet` | Xem Available/Reserved/Used/Expired |
| Ưu đãi | `GET /v1/me/promotions`, `POST /v1/me/promotions/{id}:redeem {request_key}` | Đổi điểm lấy voucher |
| Phản hồi | `GET/POST /v1/me/feedbacks` | Loại (Service/Product/Space/Other), sao 1–5, nội dung ≤4000 |
| Khảo sát | `GET /v1/me/surveys`, `GET /v1/me/surveys/{id}`, `POST …:submit` | Trả lời **toàn bộ form một lần** |
| Sở thích | `GET/PUT /v1/me/preferences {product_ids}` | (chưa có UI) |

---

## 5. Trạng thái & nhãn hiển thị (dùng cho chip/badge)

| Nhóm | Mã → nhãn | Màu gợi ý |
|---|---|---|
| Phản hồi | New=Mới · Processing=Đang xử lý · Resolved=Đã giải quyết | cam · xanh dương · xanh lá |
| Khảo sát | Draft=Bản nháp · Open=Đang mở · Closed=Đã đóng | xám · xanh lá · xám đậm |
| Voucher | Available=Có thể dùng · Reserved=Đang giữ cho đơn · Used=Đã dùng · Expired=Hết hạn | xanh lá · cam · xám · đỏ nhạt |
| Điểm | Earn=Tích điểm · Redeem=Đổi điểm · Reverse=Hoàn tác · Adjust=Điều chỉnh | +xanh / −đỏ |
| Phạm vi | Chain=Toàn chuỗi · Area=Khu vực · Branch=Chi nhánh | indigo |
| Đơn hàng (POS) | AwaitingPayment · Pending · Confirmed · Preparing · Ready · Delivering · Completed · Cancelled | stepper |
| Giao hàng | Pending · Delivering · Failed · Delivered · Cancelled | |
| Thanh toán/Hoàn tiền | Pending · Succeeded · Failed; phương thức Cash · Simulator | |

Bảng nhãn cột/giá trị đầy đủ nằm ở `MorningBrew.Admin/Services/RowDisplay.cs` — khi thêm cột mới phải thêm nhãn ở đây.

---

## 6. Quy ước dữ liệu bắt buộc phản ánh trên UI

| Quy ước | Hệ quả thiết kế |
|---|---|
| Phân trang bằng `next_page_token` (keyset, hết hạn 2h) | Chỉ có **"Trang tiếp →" / "Về trang đầu"**, không có số trang, không "nhảy tới trang 5". Có thể dùng "Tải thêm" |
| Thời gian lưu UTC, hiển thị `dd/MM/yyyy HH:mm` giờ VN | Ghi rõ múi giờ ở form ngày giờ; báo cáo ghi "theo UTC" |
| Tiền `DECIMAL`, VND | Định dạng `45.000 ₫`, không số lẻ |
| `request_key` (GUID) cho cấp voucher / đổi điểm / chỉnh điểm | Khóa nút sau khi bấm, retry không tạo trùng |
| ETag cho hồ sơ | Thiết kế thông báo "Hồ sơ đã được cập nhật ở nơi khác, tải lại?" (lỗi 412) |
| Không xóa cứng | Dùng "Khóa / Ngừng bán / Tạm ngưng / Hủy ca"; không vẽ nút "Xóa" |
| Lỗi API trả thông điệp tiếng Việt | Hiển thị nguyên văn trong alert đỏ phía trên nội dung |
| `lookups/options` | Nguồn cho mọi dropdown: branches, areas, categories, staff, customers (tối đa 200) |

---

## 7. Design system hiện có (`MorningBrew.Admin/wwwroot/css/site.css`)

**Tokens**
```css
--bg:#f5f7fb; --surface:#fff; --border:#e7ebf2; --text:#172033; --muted:#718096;
--primary:#4f46e5; --primary-dark:#3730a3; --sidebar:#111827;
--shadow:0 10px 30px rgba(15,23,42,.06);
/* Portal (tông cà phê): nền #f6f3ee, header #1c1917, nhấn #d6a76c */
```
- Font: Inter / Segoe UI; bo góc panel 17px, input 10px, nút 9px.
- Thành phần có sẵn: `.app-shell` + `.sidebar` (260px, tối), `.topbar`, `.cards/.stat`, `.panel`, `.grid-2`, `table` trong `.table-wrap`, `.primary/.secondary/.danger`, `.alert/.success`, `.status.active|warning|inactive`, `.empty-state`, `.survey-card`, `.member-card`, `.portal-hero`, `.login-card`.
- Responsive: 1100px (sidebar 225px, card 2 cột) · 800px (sidebar lên trên, grid 1 cột) · 520px (card 1 cột, bảng cuộn ngang).
- `wwwroot/js/ui.js`: đánh dấu menu đang chọn, tự tạo label cho input, empty-state cho bảng, `confirm()` cho nút "Xóa/Khóa", giữ giá trị select theo query.
- Đã có CSS cho portal riêng (`.portal-header`, `.portal-hero`, `.member-card`) **nhưng chưa dùng** — Portal hiện đang dùng chung layout sidebar của Admin.

---

## 8. Khoảng trống UX hiện tại (ưu tiên cải thiện)

1. **Bảng tự sinh từ JSON** (`_Rows.cshtml` in mọi cột trừ `name/etag`) → nhiều cột mã ID, không có hành động theo dòng. Nên: chọn cột hiển thị, gom hành động vào từng dòng (menu ⋯), chip trạng thái.
2. **Thao tác chọn bằng dropdown tách khỏi bảng** (vd. chọn phản hồi trong `<select>` rồi trả lời) → nên mở drawer/modal từ dòng.
3. Menu sidebar dùng chuỗi chữ, không icon, chỉ "Tổng quan" có ký tự ⌂ → bộ icon thống nhất + nhóm menu (Vận hành / CRM / Hệ thống).
4. Portal khách dùng layout quản trị → áp dụng layout portal (header ngang, member card hiển thị điểm).
5. Form khảo sát chỉ Rating → form builder 4 loại câu.
6. Ca làm hiển thị bảng → lịch tuần, cảnh báo trùng ca trước khi gửi.
7. Báo cáo chỉ bảng 30 ngày cố định → bộ lọc ngày/chi nhánh + biểu đồ.
8. `confirm()` chỉ bắt chữ "Xóa/Khóa" → các thao tác không hoàn tác (Đóng khảo sát, Hủy ca, Khóa tài khoản, Ngừng bán) cần modal xác nhận thống nhất.
9. Thiếu trạng thái loading/disabled khi submit (quan trọng với `request_key`).
10. Chưa có tìm kiếm/lọc ngoài trang Khách hàng (API có `q`, `fromTime`, `toTime`, `branchId`).

---

## 9. KHÔNG thiết kế (backend không hỗ trợ)

- Tự đăng ký khách, quên mật khẩu, đăng nhập mạng xã hội.
- Xóa cứng tài khoản/khách/sản phẩm/NCC.
- Tồn kho, nhập/xuất kho, công thức pha chế.
- Sửa tên/nhóm danh mục, sửa chi tiết NCC, sửa ca đã tạo, mở lại khảo sát đã đóng.
- Giá riêng theo chi nhánh trên UI (giá chung toàn chuỗi).
- Thanh toán thật, email/SMS/push (Notifications có bảng nhưng chưa có API).
- App mobile riêng (cổng khách hiện là web; nếu làm mobile thì dùng lại `/v1/me/...`).

---

## 10. Quy trình làm UI/UX đề xuất

1. **Chạy hệ thống** với seed, đăng nhập lần lượt 4 vai trò, chụp hiện trạng từng màn.
2. **Chốt user flow** cho 5 luồng chính: xử lý phản hồi · vòng đời khảo sát · tạo & cấp ưu đãi · xếp ca · khách đổi điểm/trả lời khảo sát.
3. **Wireframe** theo sitemap mục 3, mỗi màn có đủ trạng thái: đang tải · rỗng · lỗi API · không có quyền · thành công.
4. **UI kit** dựa trên tokens mục 7 (giữ indigo cho Admin, tông cà phê cho Portal), thêm: chip trạng thái (mục 5), modal xác nhận, drawer chi tiết, toast.
5. **Đối chiếu API**: mỗi nút/trường trên mockup phải trỏ tới một endpoint ở mục 4; cái nào không có → ghi "cần API" (đặc biệt POS).
6. **Hiện thực**: sửa `Pages/*.cshtml`, `site.css`, `ui.js`, `RowDisplay.cs`; không gọi DB từ Admin; kiểm tra ở 1100/800/520px.
7. **Kiểm thử**: theo checklist trong `MorningBrewCRM-Admin/KIEM_TRA.md` và `python tests/integration.py` (DB test riêng).

---

### Phụ lục — file cần đọc khi làm UI

| File | Nội dung |
|---|---|
| `MorningBrew.Admin/Pages/Shared/_Layout.cshtml` | Khung sidebar + menu theo vai trò |
| `MorningBrew.Admin/Pages/Workspace.cshtml(.cs)` | Toàn bộ màn quản trị (một trang, chia theo `section`) |
| `MorningBrew.Admin/Pages/Portal.cshtml(.cs)`, `Answer.cshtml` | Cổng khách |
| `MorningBrew.Admin/Services/CrmApi.cs`, `RowDisplay.cs` | Gọi API, nhãn tiếng Việt |
| `MorningBrew.Admin/wwwroot/css/site.css`, `js/ui.js` | Giao diện |
| `docs/api-v1.md`, `MorningBrew.Api/MorningBrew.Api.http` | Hợp đồng API |
| `database/docs/architecture.md`, `data-dictionary.md`, `erd-*.png` | Nghiệp vụ & dữ liệu (41+2 bảng) |
