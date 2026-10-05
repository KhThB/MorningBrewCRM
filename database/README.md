# Morning Brew CRM — database và tài liệu đồ án

SQL Server 2022+ · **41 bảng / 314 trường lõi**, cộng **2 bảng Admin** trong `08_admin.sql` (Suppliers, StaffShifts): tổng **43 bảng** khi cài bộ tích hợp.

## Mở tài liệu

- [Word cập nhật](docs/MorningBrewCRM_Thiet_ke_database_cap_nhat.docx): phạm vi, quy trình, quan hệ, đối chiếu tài liệu gốc và giải thích từng trường.
- [Từ điển dữ liệu](docs/data-dictionary.md): tra cứu nhanh toàn bộ trường, lý do, nguồn ghi, ví dụ và ràng buộc.
- [Phân tích nghiệp vụ](docs/architecture.md): các lựa chọn thiết kế và điều kiện tích hợp.
- [ERD Draw.io](docs/MorningBrewCRM.drawio): sơ đồ chỉnh sửa được, đủ 41 bảng. Mở bằng diagrams.net và zoom theo cụm bảng.
- [ERD Mermaid](docs/MorningBrewCRM.mmd): bản sơ đồ bằng văn bản.
- [Kết quả kiểm thử](docs/verification.md): các kịch bản đã chạy trên SQL Server thực.

API và web Admin hiện đã nối database chung. POS vẫn là project khung. Xem [hướng dẫn chạy](../README.md) và [bản ghi tích hợp](../docs/integration.md). ERD/Word gốc mô tả 41 bảng lõi; hai bảng Admin được mô tả trực tiếp trong script bổ sung.

## Database trên máy hiện tại

Server: `(localdb)\MSSQLLocalDB` · Database bàn giao: `MorningBrewCRM`.

Kết nối bằng SSMS với Windows Authentication, hoặc connection string cho backend local:

```text
Server=(localdb)\MSSQLLocalDB;Database=MorningBrewCRM;Integrated Security=True;Encrypt=True;TrustServerCertificate=True
```

`TrustServerCertificate=True` phục vụ môi trường local; khi triển khai dùng chứng chỉ và tài khoản kết nối phù hợp. LocalDB thuộc tài khoản Windows đang chạy, không phải SQL server dùng chung để app di động truy cập trực tiếp. App gọi API backend.

## Cài mới an toàn

Cần PowerShell, `sqlcmd`, SQL Server/LocalDB đang hoạt động. Từ thư mục gốc repo:

```powershell
./database/tools/Apply.ps1
```

Installer dừng nếu `MorningBrewCRM` đã tồn tại. Để tạo một bản mới:

```powershell
./database/tools/Apply.ps1 -Database MorningBrewCRM_Practice
```

Để tạo database trống chưa có seed: thêm `-NoSeed`. Khi dùng SQL Server ngoài LocalDB, `-Server` chọn instance; đường dẫn `.local` phải tồn tại trên máy server và tài khoản dịch vụ SQL cần quyền đọc/ghi. Với server từ xa nên tạo database trong SSMS bằng thư mục dữ liệu của server rồi chạy script theo thứ tự.

File MDF/LDF được đặt trong `database/.local/` và loại khỏi Git. Không di chuyển/xóa MDF/LDF khi database đang attach. Installer không xóa database hoặc dữ liệu có sẵn. Nếu cài đặt lỗi giữa chừng, đọc thông báo và kiểm tra database mới trước khi thử tiếp; không dùng script cài mới làm migration cho database có dữ liệu.

Thứ tự script:

| Script | Nội dung |
|---|---|
| `01_schema.sql` | 41 bảng, CHECK, UNIQUE, FK đơn/ghép, index; yêu cầu database trống. |
| `02_rules.sql` | Hàm quyền và trigger bảo vệ vai trò, thời hạn chính sách, dữ liệu bất biến, khảo sát. |
| `03_orders.sql` | Đặt đơn, voucher, thanh toán, trạng thái, giao hàng, hủy, hoàn, hết hạn. |
| `04_crm.sql` | Địa chỉ, giá chi nhánh, khảo sát và phản hồi. |
| `05_access.sql` | Đọc dữ liệu theo quyền, phân nhóm, doanh thu, đối chiếu và database role. |
| `06_seed.sql` | Vai trò, quyền, 11 tài khoản, 2 khu vực, 3 quán, menu và chính sách. |
| `07_demo.sql` | Chạy quy trình để tạo 5 đơn mẫu, phản hồi và khảo sát thực tế. |

Nếu chạy thủ công bằng `sqlcmd`, dùng `-I -b -f 65001`: bật quoted identifiers cho filtered index, dừng khi lỗi và đọc tiếng Việt UTF-8. Các script nghiệp vụ hỗ trợ CREATE OR ALTER; script schema/seed không dùng để chạy lặp trên database đã có dữ liệu.

## Tài khoản mẫu

Mật khẩu của tất cả tài khoản mẫu: **`MorningBrew!2026`**. Đây là mật khẩu công khai của dữ liệu demo, được lưu dưới dạng ASP.NET PasswordHasher V3; không sử dụng bộ seed này cho người dùng thật.

| UserID | Email | Vai trò và phạm vi |
|---:|---|---|
| 1 | owner@morningbrew.test | Chủ chuỗi |
| 2 | area.east@morningbrew.test | Quản lý khu Đông, quán 1 và 2 |
| 3 | manager.1@morningbrew.test | Quản lý quán 1 |
| 4 | staff.1@morningbrew.test | Nhân viên quán 1 |
| 5 | lan@morningbrew.test | Khách hàng CustomerID=1 |
| 6 | minh@morningbrew.test | Khách hàng CustomerID=2 |
| 7 | manager.2@morningbrew.test | Quản lý quán 2 |
| 8 | staff.2@morningbrew.test | Nhân viên quán 2 |
| 9 | area.west@morningbrew.test | Quản lý khu Tây, quán 3 |
| 10 | manager.3@morningbrew.test | Quản lý quán 3 |
| 11 | staff.3@morningbrew.test | Nhân viên quán 3 |

Các quán mẫu mở 00:00–23:59:59 để demo không phụ thuộc giờ làm việc. Hai khách có 100 điểm tặng ban đầu, có bút toán Adjust để khớp sổ điểm. Giá, phí giao, mức điểm và voucher là số liệu minh họa. App thực tế phải dùng ID nhận từ database, không hardcode ID seed.

## Chạy thử trong SSMS

Menu chi nhánh và tài khoản của Lan:

```sql
USE MorningBrewCRM;
EXEC dbo.GetMenu @BranchID=1;
EXEC dbo.GetCustomerAccount @ActorID=5,@CustomerID=1;
EXEC dbo.ListOrders @ActorID=4,@BranchID=1;
EXEC dbo.GetOrder @ActorID=5,@OrderID=1;
```

Tạo đơn pickup trả trước, chọn 2 ly cà phê M, 50% đường, 50% đá và trân châu:

```sql
DECLARE @key uniqueidentifier=NEWID();
EXEC dbo.CreateOrder
 @ActorID=5,@BranchID=1,@Source='App',@Fulfillment='Pickup',
 @PaymentMode='Prepaid',@CustomerID=1,@RequestKey=@key,
 @Items=N'[{"variantId":1,"quantity":2,"optionIds":[1,3,5],"note":"Mang đi"}]';
-- Procedure trả OrderID. Giữ nguyên @key và payload nếu cần retry cùng yêu cầu.
```

Ví dụ dưới đây tự tạo một đơn mới, nhận ID và chạy trọn luồng thanh toán đến hoàn tất; có thể chạy nguyên khối trong SSMS:

```sql
DECLARE @result TABLE(ID int);
DECLARE @order int,@payment int,@key uniqueidentifier=NEWID();
INSERT @result EXEC dbo.CreateOrder
 @ActorID=5,@BranchID=1,@Source='App',@Fulfillment='Pickup',
 @PaymentMode='Prepaid',@CustomerID=1,@RequestKey=@key,
 @Items=N'[{"variantId":4,"quantity":1}]';
SELECT @order=ID FROM @result;
DELETE @result;
SET @key=NEWID();
INSERT @result EXEC dbo.StartPayment @ActorID=5,@OrderID=@order,@RequestKey=@key;
SELECT @payment=ID FROM @result;
EXEC dbo.SimulatePayment @ActorID=5,@PaymentID=@payment,@Outcome='Succeeded';
EXEC dbo.AdvanceOrder @ActorID=4,@OrderID=@order,@NextStatus='Confirmed';
EXEC dbo.AdvanceOrder @ActorID=4,@OrderID=@order,@NextStatus='Preparing';
EXEC dbo.AdvanceOrder @ActorID=4,@OrderID=@order,@NextStatus='Ready';
EXEC dbo.AdvanceOrder @ActorID=4,@OrderID=@order,@NextStatus='Completed';
EXEC dbo.GetOrder @ActorID=5,@OrderID=@order;
```

Để chạy demo không cần thay ID bằng tay, đọc `07_demo.sql`: script nhận ID trả về bằng `INSERT ... EXEC` và tự truyền sang bước tiếp theo. Script này chỉ dùng trong cài mới, vì phần dữ liệu tham chiếu dùng ID seed cố định.

Đơn COD giao hàng số 2 trong dữ liệu mẫu đang ở Delivering. Nhân viên 4 có thể gọi StartPayment và SimulatePayment để xác nhận thu tiền, sau đó AdvanceOrder sang Completed. Khách hàng không được tự xác nhận đã thu COD.

## Hợp đồng stored procedure

`ActorID` luôn lấy từ identity đã xác thực ở backend; tuyệt đối không lấy từ body do client tùy chọn. `CustomerID` và quyền chi nhánh được kiểm tra bên trong các procedure đã cung cấp.

| Nhóm | Procedure | Đầu vào/đầu ra chính |
|---|---|---|
| Đặt đơn | `CreateOrder` | Danh sách món JSON, chi nhánh, khách, hình thức nhận/trả; trả OrderID. |
| Voucher | `IssueVoucher` | Khách, chương trình, RequestKey; trừ điểm nếu có và trả CustomerPromotionID. |
| Thanh toán | `StartPayment`, `SimulatePayment` | Bắt đầu một lần thử; mô phỏng Pending/Failed/Succeeded. |
| Vận hành | `AdvanceOrder`, `UpdateDelivery` | Chuyển trạng thái hợp lệ; giao thất bại/giao lại. |
| Hủy/hoàn | `CancelOrder`, `RequestRefund`, `SimulateRefund` | Hủy hoặc tạo/hoàn toàn bộ một giao dịch. |
| Job | `ExpireUnpaidOrders` | Gọi mỗi phút với tài khoản dịch vụ có quyền toàn chuỗi; không tự có lịch chạy. |
| Địa chỉ/menu | `SaveAddress`, `SetBranchPrice` | Kiểm tra chủ sở hữu/phạm vi; một địa chỉ mặc định. |
| Khảo sát | `PublishSurvey`, `InviteSurvey`, `SubmitSurvey` | Công bố, mời, gửi cả form trong một transaction. |
| Phản hồi | `SubmitFeedback`, `HandleFeedback` | Khách gửi; quản lý đúng phạm vi xử lý. |
| Đọc | `GetOrder`, `ListOrders`, `GetMenu`, `GetCustomerAccount`, `LookupCustomer` | GetOrder có nhiều result set; ListOrders phân trang tối đa 100 dòng. |
| CRM/báo cáo | `SegmentCustomers`, `RevenueReport` | Bộ lọc trong phạm vi; doanh thu theo UTC, trừ hoàn theo ngày hoàn. |

JSON dòng món: `[{"variantId":1,"quantity":2,"optionIds":[1,3],"note":"..."}]`. Không gửi giá. Đường/đá bắt buộc chọn theo MinSelections; topping khác nhau cần tách dòng món.

JSON khảo sát: `[{"questionId":1,"optionIds":[1,2]},{"questionId":2,"rating":5},{"questionId":3,"text":"Góp ý"}]`. Câu tùy chọn không trả lời thì bỏ khỏi mảng; một khách gửi form một lần.

Mã lỗi: `51100` sai nghiệp vụ, `51103` không có quyền, `51109` xung đột/yêu cầu lặp không hợp lệ. Trigger dùng `51001`; schema dùng lỗi CHECK/FK/UNIQUE chuẩn của SQL Server. Backend dịch sang thông báo tiếng Việt và HTTP phù hợp, không hiển thị nguyên lỗi SQL cho người dùng cuối.

Một số procedure gọi procedure khác, nên không dùng `INSERT ... EXEC` lồng nhiều cấp. Procedure quản trị cùng dữ liệu chủ như tạo tài khoản, sửa vai trò, tạo sản phẩm, soạn khảo sát/voucher chưa được xây thành một CRUD API; backend quản trị phải bổ sung kiểm tra quyền và audit đúng tài liệu. Bộ procedure runtime không cấp quyền sửa raw table.

## Kiểm thử lại

Bộ kiểm thử làm thay đổi dữ liệu và chỉ chấp nhận database có `Verify`/`Test` trong tên. Tạo một database mới cho mỗi lần chạy:

```powershell
./database/tools/Apply.ps1 -Database MorningBrewCRM_VerifyNew
python database/tests/verify.py --database MorningBrewCRM_VerifyNew
```

Script kiểm thử chỉ cần Python standard library và `sqlcmd`. Nếu máy không có `python` trên PATH, thay bằng đường dẫn Python đã cài. Trên máy tạo tài liệu, Python runtime của Codex được sử dụng. Kết quả JSON nằm ở `database/qa/test-results.json`, nằm ngoài Git để tránh đưa dữ liệu chạy thử vào nguồn.

Các phép thử concurrent sử dụng hai kết nối sqlcmd thực. Database bàn giao được tạo riêng bằng seed ban đầu nên không chứa biến đổi của test. Các database `MorningBrewCRM_Verify*` là dữ liệu kiểm thử riêng, không dùng làm database của app.

## Chỉnh thiết kế và sinh lại tài liệu

Nguồn chuẩn các bảng/trường: `tools/schema.py`. Chạy script để sinh lại `01_schema.sql` và `docs/schema.json`. `tools/seed.py` sinh hash tài khoản và `06_seed.sql`. `tools/document.py` sinh Word, từ điển và ERD từ metadata; cần python-docx và Pillow. Script nghiệp vụ 02–05 được viết riêng và cần điều chỉnh khi schema thay đổi.

Không sửa riêng Word rồi coi đó là schema mới. Sửa metadata và SQL nghiệp vụ, chạy kiểm thử trên database mới, rồi sinh lại tài liệu. Với database đã có dữ liệu thật, viết migration riêng có kế hoạch bảo toàn dữ liệu; bộ script cài mới không thực hiện migration từ mô hình 21 bảng.
