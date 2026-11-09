# Morning Brew CRM – Tóm tắt cập nhật & hướng dẫn chạy

> Nhánh `fix/backend-single-store`, cập nhật đến **09/11/2026**. So với bản gốc trên nhánh `lmt` (commit `9023017`): hơn 40 commit, ~200 file thay đổi.

---

## 1. Tóm tắt nhanh

| Phần | Trước | Bây giờ |
|---|---|---|
| Mô hình | Nhiều chi nhánh, có quản lý khu vực | **Một cửa hàng**; chủ cửa hàng = Admin, có thể có nhiều quản lý |
| Web | Một giao diện chung + cổng khách hàng | **/Admin** (Quản trị) và **/Manager** (Quản lý CRM); **bỏ cổng khách hàng** |
| Khách hàng | Dùng web hoặc app | **Chỉ dùng app MAUI** |
| Thực đơn, đơn hàng | Dữ liệu mẫu trong app | **Dữ liệu thật** từ database: đặt đơn, thanh toán mô phỏng, theo dõi |
| Thu ngân | Chưa có | **App POS riêng** (`MorningBrewPos`, chạy trên Windows) |
| Giao diện Admin | Form luôn mở cuối trang | **Dashboard doanh thu**, form dạng cửa sổ, nút thao tác trên từng dòng |

---

## 2. Các thay đổi chính

### Backend (API + SQL)
- Chế độ một cửa hàng, quyền `Customers.Manage`, bỏ vai trò AreaManager – **script 11**.
- Phân quyền theo vai trò: đổi vai trò tài khoản, ma trận quyền, nhật ký thao tác.
- Khảo sát 4 loại câu hỏi (đánh giá 1–5, chọn một, chọn nhiều, tự luận), mời hàng loạt theo nhóm tuổi / giới tính / sở thích.
- Đơn hàng thật: `v1/me/menu`, `v1/me/orders`, `v1/orders` (quản lý), `v1/pos` (thu ngân). Đơn trả trước quá 15 phút chưa thanh toán thì API **tự hủy** (chạy mỗi phút).
- Mã đơn ngắn dạng `MB261108-0031`; sửa lỗi khảo sát và phản hồi – **script 12**.
- Mọi thông báo lỗi từ SQL hiển thị bằng tiếng Việt.
- Sao lưu / phục hồi database ngay trên web (trang Admin → Sao lưu & phục hồi), API tự sao lưu mỗi ngày và giữ 14 bản.
- API mới cho dashboard: `GET v1/dashboard`, `GET v1/customers/{id}/overview`.

### Web Admin
- **/Admin** (chỉ chủ cửa hàng): tài khoản, phân quyền, nhật ký, sao lưu, sản phẩm & giá, nhà cung cấp.
- **/Manager** (quản lý + chủ): Tổng quan, Đơn hàng, Khách hàng, Phản hồi, Khảo sát, Ưu đãi, Báo cáo.
- **Tổng quan**: biểu đồ doanh thu theo tháng (đường cong), doanh thu từng ngày, top 5 sản phẩm, việc cần xử lý ngay.
- Mọi danh sách: nút **"+ Thêm"** mở cửa sổ form, nút thao tác trên từng dòng, bộ lọc dạng tab, thông báo nổi, bấm vào dòng để xem chi tiết ở ngăn bên phải.
- Phản hồi: hai nút **"Trả lời, vẫn xử lý"** / **"Trả lời & đóng"**; phản hồi đã giải quyết bị khóa.

### App khách hàng (MAUI)
- Thực đơn từ database, chọn tùy chọn món (đường, đá, topping).
- Đặt đơn **nhận tại quầy / giao hàng**, trả trước (cổng mô phỏng) hoặc tiền mặt; theo dõi trạng thái; hủy đơn.
- Đăng ký có ngày sinh, giới tính; đổi mật khẩu; tài khoản bị khóa có thông báo riêng.
- Khảo sát chưa tới ngày mở hiện "Sắp mở"; phản hồi không còn mặc định 5 sao.
- Token đăng nhập trên Windows được mã hóa (DPAPI); danh sách tải đủ, không dừng ở 50 dòng.
- Đăng xuất thì xóa giỏ hàng của tài khoản cũ; ô "Giao hàng" ở trang chủ chọn sẵn hình thức giao hàng trong giỏ.

### Lỗi đã sửa
BUG-01 → BUG-17, chi tiết từng lỗi (vị trí sửa, commit, test case liên quan) ở `docs/BugReport_MorningBrew_App-Admin.xlsx`. Trong code, mỗi chỗ sửa có ghi chú `[BUG-xx]` hoặc `[UI-xx]` ngay cạnh.

---

## 3. Cách chạy

### 3.1 Cần cài
- Visual Studio 2026 (hoặc .NET SDK 10) + workload **.NET MAUI**
- SQL Server LocalDB + `sqlcmd`

### 3.2 Chuẩn bị database

**Máy chưa có database** – tạo mới đủ scripts 01–12 kèm dữ liệu mẫu:
```powershell
./database/tools/Apply.ps1 -Database MorningBrewCRM_Crm
```

**Máy đã có database cũ** – chạy thêm 2 script theo đúng thứ tự (bắt buộc, nếu không API sẽ báo lỗi quyền / lỗi khảo sát):
```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\11_single_store.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\12_bugfix.sql
```

### 3.3 Chạy trong Visual Studio
1. Mở **`MorningBrewCRM.slnx`**.
2. Chuột phải Solution → **Configure Startup Projects** → **Multiple startup projects**, đặt **Start** cho:
   - `MorningBrew.Api`
   - `MorningBrew.Admin`
   - `MorningBrewwApp` (app khách) – chọn **Windows Machine**
   - `MorningBrewPos` (app thu ngân) – nếu cần
3. **Build → Rebuild Solution** → bấm **Start**.

| Thành phần | Địa chỉ |
|---|---|
| API | http://localhost:5029 · Swagger: `/swagger` · kiểm tra: `/health` |
| Web Admin | http://localhost:5191 (chủ cửa hàng vào `/Admin`, quản lý vào `/Manager`) |

> ⚠️ Trước khi Rebuild nhớ **đóng hết cửa sổ app khách / POS đang chạy**, nếu không build sẽ báo lỗi "file is locked by MorningBrewwApp".

### 3.4 Tài khoản mẫu
Mật khẩu chung: **`MorningBrew!2026`**

| Vai trò | Email | Dùng ở |
|---|---|---|
| Chủ cửa hàng | `owner@morningbrew.test` | Web `/Admin` + `/Manager` |
| Quản lý | `manager.1@morningbrew.test` | Web `/Manager` |
| Thu ngân | `staff.1@morningbrew.test` | App POS |
| Khách hàng | `lan@morningbrew.test` | App khách |

---

## 4. Kiểm thử
- **Test case** (17 nhóm, 183 case): `docs/TestCases_MorningBrew_Admin_GD1-4.xlsx` – điền kết quả vào các ô nền vàng; sheet "Tong hop" tự đếm.
- Nhóm **15–17** là các đợt sửa mới nhất (BUG-08…17 và giao diện Admin mới), đang chờ test tay.
- **Báo cáo lỗi**: `docs/BugReport_MorningBrew_App-Admin.xlsx`.

---

## 5. Lưu ý
- Thời gian lưu trong database là **UTC**; web và app tự đổi sang giờ máy. Dashboard và mã đơn tính theo giờ Việt Nam.
- Biểu đồ dùng Chart.js đặt sẵn trong `MorningBrew.Admin/wwwroot/lib/chartjs`, chạy được không cần mạng.
- `tests/integration.py` và `tests/api_contract.py` còn theo mô hình nhiều chi nhánh cũ, chưa cập nhật.
- Không đưa `bin/`, `obj/`, `.vs/`, `*.user`, file database (MDF/LDF) hay bản backup lên GitHub.
