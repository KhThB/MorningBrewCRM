# API cần bổ sung cho app khách hàng (MorningBrewApp)

App đã gọi đầy đủ các API `/v1/me/...` có sẵn: hồ sơ, điểm, ví voucher, ưu đãi, phản hồi, khảo sát, sở thích.
Còn **3 nhóm** chưa có trên `MorningBrew.Api`. Khi API trả **404**, app tự chuyển sang **dữ liệu mẫu** và hiện nhãn "Dữ liệu mẫu", nên giao diện vẫn demo được. Thêm các endpoint dưới đây vào backend thì app tự dùng dữ liệu thật, **không phải sửa code app**.

| Ưu tiên | Endpoint | Lý do |
|---|---|---|
| ⭐⭐⭐ | `POST /v1/auth/register` | Yêu cầu chấm điểm 4.3.1 — Đăng ký tài khoản khách |
| ⭐⭐ | `GET /v1/me/menu?branchId=` | Thực đơn theo chi nhánh (trang Thực đơn, Yêu thích, chọn món khi phản hồi) |
| ⭐ | `POST /v1/me/orders`, `POST /v1/me/orders/{id}:pay`, `GET /v1/me/orders`, `GET /v1/me/orders/{id}` | Luồng đặt hàng → đánh giá đơn |

Quy ước JSON giữ nguyên như `docs/api-v1.md`: snake_case, `Name` → `display_name`, `CreatedAt` → `create_time`.

---

## 1. Đăng ký — `POST /v1/auth/register` (bắt buộc)

Thêm vào `MorningBrew.Api/Controllers/AuthController.cs`:

```csharp
public record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [MaxLength(20)] string? Phone,
    [Required, StringLength(200, MinimumLength = 10)] string Password);

[HttpPost("register"), EnableRateLimiting("login")]
public async Task<IActionResult> Register(RegisterRequest input)
{
    var created = await db.Query("""
        SET XACT_ABORT ON; BEGIN TRAN;
        IF EXISTS(SELECT 1 FROM dbo.Users WHERE NormalizedEmail=UPPER(LTRIM(RTRIM(@email))))
            THROW 51109,'Email đã được sử dụng.',1;
        DECLARE @role int=(SELECT RoleID FROM dbo.Roles WHERE Code='Customer');
        INSERT dbo.Users(RoleID,Email,NormalizedEmail,FullName,Phone,PasswordHash)
        VALUES(@role,@email,UPPER(LTRIM(RTRIM(@email))),@name,@phone,@hash);
        DECLARE @id int=SCOPE_IDENTITY();
        INSERT dbo.Customers(UserID) VALUES(@id);
        INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@id,'Customer.Register','Users',@id);
        COMMIT; SELECT @id AS UserID;
        """, ("@email", input.Email.Trim()), ("@name", input.FullName.Trim()),
        ("@phone", string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim()),
        ("@hash", new PasswordHasher<string>().HashPassword(input.Email.Trim(), input.Password)));
    var id = (int)created.Single()["UserID"]!;
    return Created($"/v1/accounts/{id}", new { id, email = input.Email.Trim(), fullName = input.FullName.Trim() });
}
```

- Lỗi `51109` đang được `SqlStore` đổi thành **409** và thông điệp chung. Muốn hiện đúng câu "Email đã được sử dụng." thì dùng một mã trong khoảng 51000–51999 chưa được map riêng (ví dụ `51120`), vì các mã đó trả 400 kèm nguyên thông điệp.
- Sau khi đăng ký thành công, app tự gọi `/v1/auth/login`.

## 2. Thực đơn — `GET /v1/me/menu?branchId=1`

Thêm vào `CustomerController` (đã có `[Authorize(Roles="Customer")]`):

```csharp
[HttpGet("menu")]
public async Task<ApiPage> Menu([FromQuery] int branchId) => new(await db.Query("""
    SELECT p.ProductID,p.Name,p.Description,p.CategoryID,c.Name AS Category,
           v.VariantID,v.Name AS VariantName,v.BasePrice AS Price,
           CAST(CASE WHEN bp.IsAvailable=1 AND COALESCE(bv.IsAvailable,1)=1 THEN 1 ELSE 0 END AS bit) AS IsAvailable
    FROM dbo.BranchProducts bp
    JOIN dbo.Branches b ON b.BranchID=bp.BranchID AND b.IsActive=1
    JOIN dbo.Products p ON p.ProductID=bp.ProductID AND p.IsActive=1
    JOIN dbo.Categories c ON c.CategoryID=p.CategoryID AND c.IsActive=1
    JOIN dbo.ProductVariants v ON v.ProductID=p.ProductID AND v.IsActive=1
    LEFT JOIN dbo.BranchProductVariants bv ON bv.BranchID=bp.BranchID AND bv.VariantID=v.VariantID
    WHERE bp.BranchID=@branch ORDER BY c.CategoryID,p.ProductID,v.DisplayOrder
    """, ("@branch", branchId)), null);
```

Mỗi phần tử app cần có: `product_id, display_name, description, category_id, category, variant_id, variant_name, price, is_available`.
Món tạm hết vẫn trả về (`is_available=false`) để app làm mờ món đó, giống trong Figma.

## 3. Đơn hàng (tùy chọn — làm nếu còn thời gian)

Gọi lại các stored procedure đã có trong `03_orders.sql`: `CreateOrder`, `StartPayment`, `SimulatePayment`, `ListOrders`, `GetOrder`.

| App gọi | Body | Gợi ý cài đặt |
|---|---|---|
| `POST /v1/me/orders` | `{ branch_id, fulfillment:"Pickup", items:[{variant_id, quantity}], customer_promotion_id, request_key }` | `dbo.CreateOrder` với `@Source='App'`, `@PaymentMode='Prepaid'`, `@CustomerID` lấy từ phiên |
| `POST /v1/me/orders/{id}:pay` | `{ request_key }` | `dbo.StartPayment` rồi `dbo.SimulatePayment @Outcome='Succeeded'` |
| `GET /v1/me/orders` | — | `ApiPage` các đơn của khách |
| `GET /v1/me/orders/{id}` | — | Một đơn kèm danh sách món |

JSON của một đơn mà app đọc được:

```json
{
  "order_id": 12, "order_code": "MB01-000012", "branch_id": 1, "branch_name": "Morning Brew Thủ Đức",
  "status": "Preparing", "subtotal": 95000, "discount": 5000, "create_time": "2026-10-14T09:00:00Z",
  "lines": [ { "product_id": 1, "variant_id": 1, "display_name": "Cà phê sữa", "variant_name": "M", "quantity": 2, "unit_price": 30000 } ]
}
```

- Ràng buộc trong `01_schema.sql`: `Source IN ('App','POS')`, `PaymentMode IN ('Prepaid','COD')`, `Fulfillment IN ('Pickup','Delivery')`.
- Để đơn chuyển sang **Completed** (được tích điểm và hiện nút "Đánh giá đơn"), cần thêm nút "Hoàn tất đơn" bên web quản trị, gọi `dbo.AdvanceOrder`, vì POS chưa có giao diện.
- Phản hồi gửi từ đơn thật sẽ kèm `order_id` và `product_id`. `SubmitFeedback` đã kiểm tra món có nằm trong đơn hay không.
