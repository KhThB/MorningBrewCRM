# WALKTHROUGH — Thu gọn Morning Brew về phạm vi CRM

> Ngày sửa: 26/10/2026 · Áp dụng cho `MorningBrewCRM-integration-crm-sqlserver` và `MorningBrewApp`.
> Bản sao lưu trước khi sửa: `D:\MorningBrew(1)\_backup_20261026\truoc_khi_go_HRM.tar`.

## 1. Nhóm đã thống nhất

| Quyết định | Áp dụng |
|---|---|
| Bỏ phần HRM | Bỏ ca làm (`StaffShifts`, quyền `Staff.Manage`), trang Lịch làm việc, trang Nhân sự, vai trò Quản lý khu vực |
| Chỉ còn 1 cửa hàng | **Morning Brew Thủ Đức** (mã `MB01`, BranchID 1). Bỏ chi nhánh 2, 3 và khu vực Tây |
| Khách vừa đủ cho khảo sát | **30 khách hàng** có ngày sinh trải đều các nhóm tuổi, kèm sở thích, đơn hàng, phản hồi, câu trả lời khảo sát |

**Nguyên tắc khi sửa:**
- **Không xóa bảng** trong schema (`01_schema.sql`, `02_rules.sql`… giữ nguyên). Báo cáo thiết kế CSDL và ERD vẫn đúng, procedure và hàm phân quyền không phải viết lại.
- Chỉ giảm dữ liệu và gỡ giao diện, API thuộc phần nhân sự.

## 2. Danh sách file đã thay đổi (20 file)

### Database (`database/sql/`)

| File | Thay đổi |
|---|---|
| `06_seed.sql` | **Viết lại.** 4 vai trò (Owner, BranchManager, Staff, Customer), không còn AreaManager. 1 khu vực, 1 cửa hàng. Tài khoản: `owner@`, `manager.1@`, `staff.1@` và 30 khách (`lan@`, `minh@`, `khach01…khach28@`). Thực đơn 10 món, 11 size (món 8 và 10 tạm hết). Sở thích 2 món mỗi khách. Thêm khảo sát thứ 2 "Thăm dò món mới mùa đông" (4 câu: chọn 1, chấm sao, chọn nhiều, tự luận). Mọi ID tra theo email hoặc mã, không gán cứng |
| `07_demo.sql` | **Viết lại.** Giữ các luồng mẫu cũ (voucher, thanh toán lỗi rồi thành công, giao hàng, POS, hủy, hoàn tiền) nhưng tra ID theo email. Thêm vòng lặp cho 28 khách: đơn hoàn tất, phản hồi gắn sản phẩm, trả lời 2 khảo sát. Phân bố theo `CustomerID`, không ngẫu nhiên, nên lần cài nào cũng ra cùng số liệu |
| `08_admin.sql` | Bỏ tạo bảng `StaffShifts` và quyền `Staff.Manage`. Giữ bảng `Suppliers` và SQL role `MorningBrewApi` |

### API (`MorningBrew.Api/`)

| File | Thay đổi |
|---|---|
| `Controllers/AdminController.cs` | Bỏ `GET /v1/staff`, `GET /v1/shifts` và câu SQL tương ứng. `lookups/options` bỏ `areas`, `staff`. `[Authorize]` bỏ `AreaManager`. `POST /v1/accounts`: bỏ `branchId`/`areaId`; quản lý và thu ngân **tự gán vào cửa hàng duy nhất**; bỏ nhánh tạo AreaManager |
| `Controllers/AdminOperations.cs` | Bỏ `POST /v1/shifts`, `POST /v1/shifts/{id}:cancel`. Phân quyền chỉ cho vai trò `BranchManager` |
| `Controllers/AdminResources.cs` | Bỏ `GET /v1/shifts/{id}` |
| `Controllers/AdminCrm.cs` | Phạm vi khảo sát và ưu đãi: `Area` bị từ chối ("chỉ có 1 cửa hàng"). Còn `Chain` (chủ cửa hàng) và `Branch` (quản lý) |
| `Controllers/VouchersController.cs` | `[Authorize]` bỏ `AreaManager` |
| `Infrastructure/ApiDocumentation.cs` | Bỏ tên `Shift` khỏi danh sách endpoint trả 201 trên Swagger |

### Web Admin (`MorningBrew.Admin/`)

| File | Thay đổi |
|---|---|
| `Pages/Shared/_Layout.cshtml` | Bỏ menu **Lịch làm việc** và **Nhân sự** |
| `Pages/Workspace.cshtml` | Bỏ khối Phân ca / Hủy ca. Form tạo tài khoản: bỏ ô chọn chi nhánh và khu vực, vai trò hiển thị tiếng Việt (Khách hàng / Quản lý cửa hàng / Thu ngân / Chủ cửa hàng). Phân quyền: cố định vai trò Quản lý cửa hàng, bỏ quyền `Staff.Manage` |
| `Pages/Workspace.cshtml.cs` | Bỏ section `staff`, `shifts` và thao tác `shift`, `shift-cancel`. Tạo tài khoản không gửi `branchId`/`areaId` |
| `Pages/Shared/_Scope.cshtml` | **Viết lại.** Không còn ô chọn phạm vi, khu vực, chi nhánh: chủ cửa hàng gửi `Chain`, quản lý gửi `Branch` + mã cửa hàng (ẩn). Hiện dòng "Áp dụng cho: Morning Brew Thủ Đức" |
| `Pages/Index.cshtml` | Dashboard bỏ ô "Chi nhánh" |
| `Program.cs` | Policy `Management` bỏ `AreaManager` |
| `Services/RowDisplay.cs` | Nhãn vai trò: Chủ cửa hàng / Quản lý cửa hàng / Thu ngân |

### App khách (`MorningBrewApp/MauiApp1/MauiApp1/`)

| File | Thay đổi |
|---|---|
| `Services/ShopService.cs` | Danh sách 3 chi nhánh thay bằng `Store` duy nhất, bỏ `SelectBranch`. Thực đơn mẫu **trùng ID với `06_seed.sql`** (10 món), nên yêu thích và phản hồi lưu được cho mọi món |
| `Components/Pages/MenuPage.razor` | Bỏ ô chọn cửa hàng, chỉ hiện tên cửa hàng |
| `wwwroot/app.css` | Thêm style `.store-name` |

### Tài liệu

| File | Thay đổi |
|---|---|
| `README.md` (repo integration) | Cập nhật bảng tài khoản demo |
| `WALKTHROUGH.md` | File này |

## 3. Cố ý giữ nguyên

| Thành phần | Lý do |
|---|---|
| Bảng `Areas`, `Branches`, `AreaManagers`, `StaffBranches` trong schema | Đơn hàng, phản hồi, khảo sát, phân quyền đều tham chiếu `BranchID`. Bảng vẫn còn nhưng chỉ có 1 dòng |
| Hàm `CanBranch`, `CanScope`, trigger vai trò (`02_rules.sql`) | Vẫn đúng với 1 cửa hàng. Đoạn xét `AreaManager` không bao giờ khớp vì không còn vai trò này |
| Vai trò `Staff` (`staff.1@`) | Là **thu ngân POS**, cần để chuyển đơn sang Hoàn tất (sinh điểm và giao dịch). Không phải HRM |
| Nhà cung cấp, Sản phẩm, Ưu đãi, Báo cáo doanh thu | Không thuộc HRM. Nếu nhóm muốn ẩn thêm thì làm ở bước sau (workplan A1) |
| Ô "Chi nhánh" trong `dashboard/overview` của API | Admin không hiển thị nữa. Để lại cho tương thích |

## 4. Chưa cập nhật (cần biết)

| File | Tình trạng | Việc cần làm |
|---|---|---|
| `tests/integration.py` | Vẫn dùng `area.west@`, `/v1/shifts`, `/v1/staff`, nên **sẽ báo lỗi** | Bỏ các bước ca làm và khu vực, đổi tài khoản sang bộ mới |
| `database/tests/verify.py` | Viết theo seed cũ (3 chi nhánh), **có thể báo lỗi** ở các kiểm tra phạm vi | Cập nhật số liệu kỳ vọng |
| `database/tools/seed.py` | Công cụ sinh seed cũ | **Đừng chạy**, sẽ ghi đè `06_seed.sql` mới |
| `docs/`, `database/docs/` | Vẫn mô tả nhiều chi nhánh | Báo cáo: ghi "phiên bản demo dùng 1 cửa hàng" |
| Database `MorningBrewCRM` đang có trên máy | Vẫn là **dữ liệu cũ** (3 chi nhánh, có `StaffShifts`) | Tạo database mới, xem mục 5 |

## 5. Cài database mới

Script `Apply.ps1` từ chối ghi đè database đã có, nên tạo database mới:

```powershell
cd "D:\MorningBrew(1)\MorningBrewCRM-integration-crm-sqlserver\MorningBrewCRM-integration-crm-sqlserver"
.\database\tools\Apply.ps1 -Database MorningBrewCRM_Crm
```

Sau đó đổi `Database=MorningBrewCRM` thành `Database=MorningBrewCRM_Crm` trong `MorningBrew.Api\appsettings.Development.json`.

Muốn giữ tên cũ thì xóa database cũ trước (mất dữ liệu test cũ):

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "DROP DATABASE [MorningBrewCRM]"
.\database\tools\Apply.ps1
```

Cuối `07_demo.sql` sẽ in một dòng tổng kết. **Số liệu mong đợi:**

| Customers | CompletedOrders | Feedbacks | SurveyAnswers |
|---|---|---|---|
| 30 | 24 | 18 | 41 |

## 6. Checklist kiểm tra sau khi sửa

### A. Build

- [ ] `dotnet build MorningBrewCRM.slnx`: 0 lỗi
- [ ] Build `MorningBrewApp` (Windows Machine): 0 lỗi

### B. Database (chạy trong SSMS hoặc sqlcmd trên database mới)

```sql
SELECT COUNT(*) FROM dbo.Branches;                 -- 1
SELECT COUNT(*) FROM dbo.Areas;                    -- 1
SELECT Code FROM dbo.Roles;                        -- Owner, BranchManager, Staff, Customer
SELECT OBJECT_ID('dbo.StaffShifts');               -- NULL
SELECT COUNT(*) FROM dbo.Permissions WHERE Code='Staff.Manage';   -- 0
SELECT COUNT(*) FROM dbo.Customers;                -- 30
-- Nhóm tuổi (dữ liệu cho báo cáo 4.2.5)
SELECT CASE WHEN a<25 THEN '18-24' WHEN a<35 THEN '25-34' WHEN a<45 THEN '35-44' ELSE '45+' END AS Nhom, COUNT(*) AS SoKhach
FROM (SELECT DATEDIFF(year,DateOfBirth,GETDATE()) a FROM dbo.Customers) x GROUP BY CASE WHEN a<25 THEN '18-24' WHEN a<35 THEN '25-34' WHEN a<45 THEN '35-44' ELSE '45+' END;
-- Món được yêu thích: Cà phê sữa dẫn đầu (12 lượt)
SELECT p.Name, COUNT(*) AS LuotThich FROM dbo.CustomerPreferences cp JOIN dbo.Products p ON p.ProductID=cp.ProductID GROUP BY p.Name ORDER BY 2 DESC;
-- Phản hồi theo trạng thái: New 10 · Processing 3 · Resolved 5
SELECT Status, COUNT(*) FROM dbo.Feedbacks GROUP BY Status;
```

### C. API (`http://localhost:5029`)

- [ ] `/health` trả `ready`
- [ ] Swagger không còn `/v1/shifts`, `/v1/staff`
- [ ] `GET /v1/lookups/options` (đăng nhập owner) chỉ còn `branches` (1 dòng), `categories`, `customers`

### D. Web Admin (`https://localhost:7287`)

- [ ] Đăng nhập `owner@`: menu **không có** Lịch làm việc, Nhân sự
- [ ] Tổng quan: 3 ô (Khách hàng 30, Phản hồi cần xử lý 13, Khảo sát đang mở 2)
- [ ] Tài khoản → Thêm tài khoản: không có ô Chi nhánh, Khu vực. Tạo "Thu ngân" mới thành công
- [ ] Khảo sát → Tạo khảo sát: không có ô phạm vi, có dòng "Áp dụng cho: Morning Brew Thủ Đức". Tạo, phát hành, mời Lan thành công (không còn lỗi `Scope denied`)
- [ ] Khảo sát → chọn "Thăm dò món mới mùa đông" → Xem kết quả: khoảng 19 người trả lời, Cacao muối nhiều nhất
- [ ] Đăng nhập `manager.1@`: tạo khảo sát (phạm vi cửa hàng) thành công
- [ ] Phân quyền: chỉ có vai trò Quản lý cửa hàng, không có quyền `Staff.Manage`

### E. App khách

- [ ] Đăng nhập `lan@`: chuông và tab Tài khoản có badge **1** (khảo sát "Thăm dò món mới mùa đông")
- [ ] Thực đơn: không có ô chọn cửa hàng; 10 món; Trà sen vàng và Mousse chanh dây hiện "Tạm hết"
- [ ] ♡ Bạc xỉu → "Đã thêm vào món yêu thích" (trước đây báo "món minh họa")
- [ ] Gửi phản hồi về Cold brew cam sả → thấy ở Admin → Phản hồi
- [ ] Làm khảo sát món mới → Admin → Xem kết quả thấy câu trả lời của Lan

## 7. Hoàn tác

Giải nén `_backup_20261026\truoc_khi_go_HRM.tar` đè lên hai thư mục, rồi đổi lại connection string về `MorningBrewCRM`.

## 8. Đã kiểm tra và chưa kiểm tra

| Đã kiểm tra | Chưa kiểm tra |
|---|---|
| Biên dịch các controller API (thay Microsoft.Data.SqlClient bằng stub) | **Chạy SQL thật**: môi trường sửa không có SQL Server. `06`, `07`, `08` cần chạy `Apply.ps1` trên máy |
| Biên dịch web Admin, gồm toàn bộ trang Razor | Chạy API và Admin với database thật |
| Biên dịch component và service của app MAUI | Build app MAUI thật (Windows hoặc Android) |
| Đối chiếu tham số với chữ ký từng stored procedure (`CreateOrder`, `StartPayment`, `SimulatePayment`, `AdvanceOrder`, `SubmitFeedback`, `HandleFeedback`, `IssueVoucher`, `PublishSurvey`, `InviteSurvey`, `SubmitSurvey`) | |
| So sánh với bản sao lưu: đúng 20 file thay đổi, không file nào khác bị chạm | |

Nếu `Apply.ps1` báo lỗi ở `06_seed.sql` hoặc `07_demo.sql`, gửi nguyên thông báo lỗi (tên file và số dòng) để sửa.
