# Hợp đồng API v1 — MorningBrew CRM

## Chạy và đăng nhập

1. Chạy `MorningBrew.Api` và `MorningBrew.Admin`, profile `https`.
2. Mở [Swagger](https://localhost:7132/swagger).
3. Gọi `POST /v1/auth/login` với email/mật khẩu demo:

   ```json
   {"username":"owner@morningbrew.test","password":"MorningBrew!2026"}
   ```

4. Sao chép `token` trong kết quả vào **Authorize**. Chỉ dán token; Swagger thêm `Bearer`.
5. Dùng `lan@morningbrew.test` để kiểm tra các API `/v1/me/...`.

Xem thêm các request có thể chạy trực tiếp trong Visual Studio: `MorningBrew.Api/MorningBrew.Api.http`.

## Quy ước

| Mục | Hợp đồng |
|---|---|
| Phiên bản | `/v1/...`; routes `/api/...` cũ đã được thay thế. Admin trong repo đã chuyển đồng bộ. |
| JSON | `snake_case`; từ chối trường không được khai báo trong request DTO. |
| Query | `pageSize`, `pageToken`, `q`, `fromTime`, `toTime`, `branchId`. |
| Định danh | `name`, ví dụ `accounts/12`; tên hiển thị là `display_name` hoặc `full_name`. ID số vẫn có để chọn quan hệ. |
| Thời gian | `create_time`, `start_time`, `end_time`, `expire_time`, `complete_time`; ISO 8601 UTC có `Z`. Request timestamp phải có `Z` hoặc offset. |
| Ngày sinh | `date_of_birth`: `yyyy-MM-dd`, không chuyển múi giờ. |
| Tạo tài nguyên | `201 Created`, body chứa tài nguyên, `Location` trỏ tới GET chi tiết. |
| Thao tác nghiệp vụ | `POST /v1/surveys/{id}:publish`, `:invite`, `:close`... |
| Thành công không có nội dung | `204 No Content`: đăng xuất, thay trạng thái, gửi khảo sát... |
| Xác thực | `Authorization: Bearer <token>`; phiên ngẫu nhiên có hạn 8 giờ, kiểm tra quyền hiện tại mỗi request. |
| Cache | `Cache-Control: no-store`; ETag hồ sơ phục vụ kiểm soát cập nhật. |

Chỉ xuất các mốc thời gian thực sự có trong database; không tự tạo `update_time` giả cho bảng chưa lưu thời điểm cập nhật.

## Danh sách và phân trang

```http
GET /v1/customers?pageSize=20&q=Lan
Authorization: Bearer <token>
```

```json
{
  "items": [{"name":"customers/1","customer_id":1,"full_name":"Lan"}],
  "next_page_token": "<opaque-token>"
}
```

Ví dụ đã rút gọn các trường. Trang tiếp theo giữ nguyên `q`, thêm `pageToken` nhận được. `pageSize` mặc định 50, từ 1–100. `next_page_token: null` nghĩa là hết trang.

Token được mã hóa và xác thực bằng ASP.NET Data Protection, hết hạn sau 2 giờ, gắn với tài khoản/tài nguyên/bộ lọc. Mỗi trang vẫn kiểm tra quyền từ SQL. Dùng keyset theo khóa duy nhất nên thêm bản ghi mới ở đầu danh sách không làm lặp bản ghi trang sau. Đây không phải ảnh chụp bất biến: thay đổi quyền, dữ liệu hoặc bộ lọc vẫn ảnh hưởng kết quả.

Áp dụng cho danh sách quản trị, lịch sử điểm/voucher/phản hồi/khảo sát/sở thích và kết quả khảo sát. Báo cáo doanh thu trả cùng envelope nhưng không phân trang, vì là dữ liệu tổng hợp theo chi nhánh. `lookups/options` là dữ liệu hỗ trợ chọn trong form, không phải endpoint xuất toàn bộ dữ liệu; danh sách khách ở đó giới hạn 200.

## Endpoint chính

| Chức năng | Endpoint |
|---|---|
| Xác thực | `POST /v1/auth/login`, `GET /v1/auth/me`, `POST /v1/auth/logout` |
| Khách hàng, tài khoản | `GET /v1/customers`, `GET/POST /v1/accounts`, `GET /v1/accounts/{id}` |
| Danh mục chung | `GET/POST /v1/products`, `GET /v1/products/{id}`, `GET /v1/variants`, `GET /v1/variants/{id}` |
| Giá chung | `GET/PUT /v1/variants/{id}/price` |
| Nhà cung cấp, ca làm | `GET/POST /v1/suppliers`, `/v1/shifts`; GET chi tiết theo ID |
| Khảo sát | `GET/POST /v1/surveys`, `GET /v1/surveys/{id}`, `POST ...:publish`, `...:close`, `...:invite` |
| Kết quả khảo sát | `GET /v1/surveys/{id}/results?pageSize=50&pageToken=...` |
| Khuyến mãi | `GET/POST /v1/promotions`, `GET /v1/promotions/{id}`, `POST ...:issue` |
| Voucher | `GET /v1/vouchers/{id}`; cấp/đổi voucher trả tài nguyên này, kể cả khi retry cùng `request_key` |
| Phản hồi | `GET /v1/feedbacks`, `POST /v1/feedbacks/{id}:reply` |
| Điểm | `POST /v1/customers/{id}:adjustPoints` |
| Trạng thái | `PUT /v1/{accounts|products|promotions|suppliers}/{id}/active`, body `{"is_active":false}` |
| Phân quyền | `GET /v1/permissions`, `PUT /v1/roles/{role}/permissions/{code}`, body `{"allowed":true}` |
| Dashboard, báo cáo | `GET /v1/dashboard/overview`, `GET /v1/reports/revenue?fromTime=...&toTime=...&branchId=...` |
| Hồ sơ bản thân | `GET/PATCH /v1/me/profile` |
| Dữ liệu bản thân | `GET /v1/me/{loyalty|wallet|promotions|feedbacks|surveys|preferences}` |
| Gửi phản hồi | `POST /v1/me/feedbacks`, `GET /v1/me/feedbacks/{id}` |
| Đổi voucher | `POST /v1/me/promotions/{id}:redeem` |
| Trả lời khảo sát | `GET /v1/me/surveys/{id}`, `POST /v1/me/surveys/{id}:submit` |
| Thay bộ sở thích | `PUT /v1/me/preferences`, body `{"product_ids":[1,2]}` |

`products` trả từng sản phẩm; `variants` trả từng kích cỡ kèm giá chung. Thao tác giá chỉ dành cho Owner. Không có API menu hay bảng giá riêng cho chi nhánh. Chi nhánh vẫn có ý nghĩa khi phân quyền, nhận đơn và kiểm tra tình trạng có bán.

## PATCH hồ sơ và chống ghi đè

1. GET `/v1/me/profile`, giữ header `ETag` (cũng có trong trường `etag`).
2. Gửi PATCH kèm `If-Match` chính xác, bao gồm dấu ngoặc kép:

```http
PATCH /v1/me/profile
Authorization: Bearer <customer-token>
If-Match: "0000000000000123.0000000000000456"
Content-Type: application/json

{"phone":null}
```

Chỉ trường có mặt được sửa. `null` xóa điện thoại/ngày sinh/giới tính; không được xóa họ tên. Không nhận điểm, email, vai trò hoặc mã khách trong PATCH. Chỉ cho phép `full_name`, `phone`, `date_of_birth`, `gender`.

ETag kiểm tra đồng thời phiên bản `Users` và `Customers` trong transaction. Thiếu If-Match trả 428; dữ liệu đã đổi trả 412. Khi nhận 412, tải lại và để người dùng xem thay đổi trước khi gửi lại. Những endpoint nghiệp vụ khác vẫn dùng transaction/quyền và `request_key` theo chức năng; không tuyên bố tất cả cập nhật đã hỗ trợ If-Match.

## Lỗi

```json
{
  "error": {
    "code": 400,
    "message": "pageSize phải từ 1 đến 100.",
    "status": "INVALID_ARGUMENT",
    "details": [{"reason":"OUT_OF_RANGE","field":"pageSize"}]
  }
}
```

| HTTP | Status | Xử lý |
|---|---|---|
| 400 / 415 | `INVALID_ARGUMENT` | Kiểm tra JSON, kiểu dữ liệu, trường và Content-Type. |
| 401 | `UNAUTHENTICATED` | Đăng nhập lại; response có `WWW-Authenticate: Bearer`. |
| 403 | `PERMISSION_DENIED` | Thiếu quyền hoặc ngoài phạm vi. |
| 404 | `NOT_FOUND` | Không có tài nguyên hoặc không thấy trong phạm vi được cấp. |
| 405 | `METHOD_NOT_ALLOWED` | Sai HTTP method. |
| 409 | `ALREADY_EXISTS` / `ABORTED` | Trùng dữ liệu hoặc xung đột nghiệp vụ/request key. |
| 412 / 428 | `FAILED_PRECONDITION` | ETag cũ hoặc thiếu If-Match. |
| 429 | `RESOURCE_EXHAUSTED` | Chờ theo `Retry-After`. |
| 500 / 503 | `INTERNAL` / `UNAVAILABLE` | Lỗi server hoặc database; chi tiết kỹ thuật ghi trong log. |

Lỗi validation, JSON sai, xác thực, routing và rate limit dùng cùng envelope. Không gửi SQL stack trace cho client.

## Nâng cấp database đã có

Sau schema `01`–`08`, chạy:

```powershell
./database/tools/Upgrade-SharedPrices.ps1 -Database MorningBrewCRM
```

Script lưu định nghĩa procedure cũ vào `database/.local/shared-prices-before-.../restore-procedures.sql`, rồi áp dụng `09_shared_prices.sql` và so sánh số liệu tài chính trước/sau. Migration chỉ thay procedure: đơn mới/menu nội bộ lấy `ProductVariants.BasePrice` và `ModifierOptions.ExtraPrice`. Các hàng cũ về tình trạng có bán vẫn được giữ, nhưng giá riêng bị bỏ qua. `SetBranchPrice` cũ từ chối thay giá. **Không sửa giá snapshot trên đơn hàng đã tạo.** Script cài mới `Apply.ps1` đã chạy cả `09`.

## Cấu hình và kiểm thử

- Browser frontend khác origin: cấu hình danh sách chính xác ở `Cors:AllowedOrigins`; mặc định không cho phép CORS. Admin hiện gọi API từ server nên không cần bật CORS.
- Giữ Data Protection keys ổn định khi triển khai; nhiều instance phải dùng chung keys/application name. Mất keys làm token phân trang cũ hết hiệu lực.
- Chạy test trên hai database mới riêng biệt vì bộ SQL và bộ API đều thay đổi dữ liệu demo:

```powershell
dotnet build MorningBrewCRM.slnx
./database/tools/Apply.ps1 -Database MorningBrewCRM_VerifyV1
python database/tests/verify.py --database MorningBrewCRM_VerifyV1
./database/tools/Apply.ps1 -Database MorningBrewCRM_TestApiV1
python tests/integration.py --database MorningBrewCRM_TestApiV1
```

Các kiểm tra gồm phân quyền, CSRF, lưu phiên, mã HTTP, JSON lỗi, phân trang có chèn mới và khóa ghép, PATCH đồng thời, giá chung, Swagger, CORS và rate limit.

Đợt này chuẩn hóa các chức năng đang có. API đặt đơn/thanh toán, đăng ký, địa chỉ, thông báo và phân nhóm CRM tiếp tục thuộc giai đoạn bổ sung nghiệp vụ.
