using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

[ApiController, Route("v1"), Authorize(Roles = "Owner,BranchManager")]
public sealed partial class AdminController(SqlStore db, PageReader pages) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    // A customer belongs to a manager's CRM scope after a completed purchase at a managed branch.
    private const string CustomerScope = """
        (dbo.CanScope(@actor,'Chain',NULL,NULL,'Customers.Read')=1 OR EXISTS
        (SELECT 1 FROM dbo.Orders o WHERE o.CustomerID=c.CustomerID AND o.Status='Completed'
        AND dbo.CanBranch(@actor,o.BranchID,'Customers.Read')=1))
        """;
    private async Task RequireOwner()
    {
        if (!User.IsInRole("Owner")) throw new ApiException(403, "Chỉ chủ chuỗi được quản lý dữ liệu dùng chung.");
        var allowed = await db.Query("SELECT 1 AS OK WHERE dbo.CanScope(@actor,'Chain',NULL,NULL,'Security.Manage')=1", ("@actor", Actor));
        if (allowed.Count == 0) throw new ApiException(403, "Không có quyền quản trị.");
    }
    // Quyền nghiệp vụ trên phạm vi chung (một cửa hàng: chủ cửa hàng và quản lý được cấp quyền).
    private async Task RequirePermission(string code)
    {
        var allowed = await db.Query("SELECT 1 AS OK WHERE dbo.CanScope(@actor,'Chain',NULL,NULL,@code)=1", ("@actor", Actor), ("@code", code));
        if (allowed.Count == 0) throw new ApiException(403, "Bạn không có quyền thực hiện thao tác này.");
    }

    // [CRM-02] GET v1/customers chuyển sang AdminCrmInsights.cs (thêm thống kê mua hàng và lọc theo nhóm hành vi).
    [HttpGet("surveys")]
    public Task<ApiPage> ListSurveys([FromQuery] PageRequest page) => List("surveys", page);
    [HttpGet("promotions")]
    public Task<ApiPage> ListPromotions([FromQuery] PageRequest page, string? q = null) => List("promotions", page, q); // [WEB-11] tìm ưu đãi
    [HttpGet("products")]
    public Task<ApiPage> ListProducts([FromQuery] PageRequest page) => pages.Read(
        "SELECT ProductID,CategoryID,Name,Description,ImageURL,IsActive FROM dbo.Products",
        ["ProductID"],true,page,$"{Actor}:productResources",[],"products","ProductID");
    [HttpGet("variants")]
    public Task<ApiPage> ListVariants([FromQuery] PageRequest page, string? q = null) => List("products", page, q); // [WEB-13] tìm theo tên món / danh mục / size
    [HttpGet("permissions")]
    public Task<ApiPage> ListPermissions([FromQuery] PageRequest page) => List("permissions", page);
    [HttpGet("suppliers")]
    public Task<ApiPage> ListSuppliers([FromQuery] PageRequest page, string? q = null) => List("suppliers", page, q); // [WEB-12] tìm nhà cung cấp

    private async Task<ApiPage> List(string section, PageRequest page, string? q = null)
    {
        string sql;
        switch (section.ToLowerInvariant())
        {
            case "suppliers":
                await RequirePermission("Inventory.Manage"); // [WEB-10] quản lý có quyền kho cũng xem / thêm nhà cung cấp (trước đây chỉ chủ cửa hàng)
                sql = """
                    SELECT SupplierID,Code,Name,Phone,Email,ProductGroup,IsActive FROM dbo.Suppliers
                    WHERE (@q='' OR Code LIKE @search OR Name LIKE @search OR ProductGroup LIKE @search) -- [WEB-12]

                    """;
                break;
            case "feedbacks":
                sql = """
                    SELECT f.FeedbackID,u.FullName,f.BranchID,f.Type,f.Content,f.Rating,f.Status,f.ResponseContent,f.CreatedAt
                    FROM dbo.Feedbacks f JOIN dbo.Customers c ON c.CustomerID=f.CustomerID JOIN dbo.Users u ON u.UserID=c.UserID
                    WHERE (f.BranchID IS NULL AND dbo.CanScope(@actor,'Chain',NULL,NULL,'Feedbacks.Manage')=1)
                    OR dbo.CanBranch(@actor,f.BranchID,'Feedbacks.Manage')=1

                    """; break;
            case "surveys":
                sql = """
                    SELECT s.SurveyID,s.Title,s.Scope,s.AreaID,s.BranchID,s.StartsAt,s.EndsAt,s.Status,
                    (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID) AS Invited,
                    (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID AND r.CompletedAt IS NOT NULL) AS Completed
                    FROM dbo.Surveys s WHERE dbo.CanScope(@actor,s.Scope,s.AreaID,s.BranchID,'Surveys.Manage')=1

                    """; break;
            case "promotions":
                sql = """
                    SELECT PromotionID,Name,Scope,AreaID,BranchID,DiscountType,DiscountValue,PointsCost,StartsAt,EndsAt,IsActive,
                    Description,MaxDiscount,MinSubtotal,TotalLimit,PerCustomerLimit, -- [WEB-11] để mở form Sửa ưu đãi
                    (SELECT COUNT(*) FROM dbo.CustomerPromotions cp WHERE cp.PromotionID=p.PromotionID) AS Issued, -- [WEB-11] đã cấp
                    (SELECT COUNT(*) FROM dbo.CustomerPromotions cp WHERE cp.PromotionID=p.PromotionID AND cp.Status='Used') AS Used -- [CRM-04] đã dùng
                    FROM dbo.Promotions p WHERE dbo.CanScope(@actor,Scope,AreaID,BranchID,'Promotions.Manage')=1
                    AND (@q='' OR p.Name LIKE @search OR p.Description LIKE @search) -- [WEB-11]

                    """; break;
            case "products":
                sql = """
                    SELECT p.ProductID,p.Name,c.Name AS Category,v.VariantID,v.Name AS Size,v.BasePrice,p.IsActive,p.ImageURL, -- [UI2-IMG]
                    CAST(COALESCE((SELECT MIN(CONVERT(int,bp.IsAvailable)) FROM dbo.BranchProducts bp WHERE bp.ProductID=p.ProductID),0) AS bit) AS InStock, -- [UI2-A4a] còn hàng / tạm hết
                    p.CategoryID,p.Description,v.IsActive AS SizeActive, -- [WEB-03] để mở form Sửa sản phẩm / ẩn size
                    CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ProductModifierGroups pg JOIN dbo.ModifierGroups g ON g.ModifierGroupID=pg.ModifierGroupID
                        WHERE pg.ProductID=p.ProductID AND g.Kind='Ice' AND g.IsActive=1) THEN 1 ELSE 0 END AS bit) AS HasTemperature -- [WEB-02] có chọn Đá / Nóng
                    FROM dbo.Products p JOIN dbo.Categories c ON c.CategoryID=p.CategoryID JOIN dbo.ProductVariants v ON v.ProductID=p.ProductID
                    WHERE (@q='' OR p.Name LIKE @search OR c.Name LIKE @search OR v.Name LIKE @search) -- [WEB-13]

                    """; break;
            case "accounts":
                await RequireOwner();
                sql = """
                    SELECT u.UserID,u.FullName,u.Email,u.Phone,r.Code AS Role,u.IsActive,u.CreatedAt
                    FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.DeletedAt IS NULL

                    """; break;
            case "permissions":
                await RequireOwner();
                sql = """
                    SELECT r.Code AS Role,p.Code AS Permission,p.Description
                    FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.RoleID=rp.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID

                    """; break;
            default: throw new ApiException(404, "Không tìm thấy chức năng.");
        }
        var keys = section switch
        {
            "suppliers" => new[] { "SupplierID" },
            "feedbacks" => ["FeedbackID"], "surveys" => ["SurveyID"], "promotions" => ["PromotionID"],
            "products" => ["VariantID"], "accounts" => ["UserID"], "permissions" => ["Role", "Permission"],
            _ => throw new ApiException(404, "Resource not found.")
        };
        return await pages.Read(sql, keys, section is not "permissions", page,
            JsonSerializer.Serialize(new { Actor, section, q = q ?? "" }),
            [("@actor", Actor), ("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%")],
            section is "permissions" ? null : section == "products" ? "variants" : section, keys[0]);
    }

    [HttpGet("dashboard/overview")]
    public async Task<object> Overview() => (await db.Query($"""
        SELECT (SELECT COUNT(*) FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE u.DeletedAt IS NULL AND {CustomerScope}) AS Customers,
        (SELECT COUNT(*) FROM dbo.Feedbacks f WHERE f.Status<>'Resolved' AND
        ((f.BranchID IS NULL AND dbo.CanScope(@actor,'Chain',NULL,NULL,'Feedbacks.Manage')=1)
        OR dbo.CanBranch(@actor,f.BranchID,'Feedbacks.Manage')=1)) AS PendingFeedbacks,
        (SELECT COUNT(*) FROM dbo.Surveys s WHERE s.Status='Open' AND s.StartsAt<=SYSUTCDATETIME() AND s.EndsAt>SYSUTCDATETIME()
        AND dbo.CanScope(@actor,s.Scope,s.AreaID,s.BranchID,'Surveys.Manage')=1) AS OpenSurveys,
        (SELECT COUNT(*) FROM dbo.Branches b WHERE dbo.CanBranch(@actor,b.BranchID,'Reports.Read')=1) AS Branches
        """, ("@actor", Actor))).Single();

    [HttpGet("reports/revenue")]
    public async Task<ApiPage> Revenue(DateTimeOffset? fromTime = null, DateTimeOffset? toTime = null, int? branchId = null)
        => new(await db.Procedure("dbo.RevenueReport", ("@ActorID", Actor), ("@FromUTC", fromTime?.UtcDateTime ?? DateTime.UtcNow.Date.AddDays(-30)),
            ("@ToUTC", toTime?.UtcDateTime ?? DateTime.UtcNow.Date.AddDays(1)), ("@BranchID", branchId)), null);

    // [WEB-09] Doanh thu theo từng ngày (giờ Việt Nam) trong kỳ do người xem chọn; cùng cách tính với dbo.RevenueReport:
    // doanh thu ghi vào ngày đơn hoàn tất, khoản hoàn tiền ghi vào ngày hoàn tiền thành công. Kỳ tối đa 366 ngày.
    [HttpGet("reports/revenue/daily")]
    public async Task<ApiPage> RevenueDaily(DateTimeOffset fromTime, DateTimeOffset toTime)
    {
        if (toTime <= fromTime) throw new ApiException(400, "Ngày kết thúc phải sau ngày bắt đầu.");
        if (toTime - fromTime > TimeSpan.FromDays(366)) throw new ApiException(400, "Kỳ báo cáo tối đa 366 ngày.");
        await RequirePermission("Reports.Read");
        return new(await db.Query("""
            WITH ledger AS (
                SELECT o.CompletedAt AS EventAt,1 AS CompletedOrders,o.TotalAmount AS Gross,CONVERT(decimal(18,2),0) AS Refunded
                FROM dbo.Orders o WHERE o.CompletedAt IS NOT NULL
                UNION ALL
                SELECT r.CompletedAt,0,0,r.Amount FROM dbo.Refunds r JOIN dbo.Orders o ON o.OrderID=r.OrderID
                WHERE r.Status='Succeeded' AND o.CompletedAt IS NOT NULL)
            SELECT CONVERT(date,DATEADD(hour,7,EventAt)) AS Day,SUM(CompletedOrders) AS CompletedOrders,
                SUM(Gross) AS GrossRevenue,SUM(Refunded) AS RefundedRevenue,SUM(Gross-Refunded) AS NetRevenue
            FROM ledger WHERE EventAt>=@from AND EventAt<@to
            GROUP BY CONVERT(date,DATEADD(hour,7,EventAt)) ORDER BY Day
            """, ("@from", fromTime.UtcDateTime), ("@to", toTime.UtcDateTime)), null);
    }

    [HttpGet("lookups/options")]
    // [BUG-12] customerQ: tìm khách theo tên / email / SĐT để chọn trong các ô Mời khảo sát, Cấp voucher
    // (trước đây chỉ có 200 khách mới nhất, không tìm được khách cũ hơn).
    public async Task<object> Options(string? customerQ = null) => new
    {
        branches = await db.Query("SELECT BranchID AS Id,Name FROM dbo.Branches WHERE dbo.CanBranch(@actor,BranchID,'Orders.Read')=1 ORDER BY BranchID", ("@actor", Actor)),
        categories = await db.Query("SELECT CategoryID AS Id,Name FROM dbo.Categories WHERE IsActive=1 ORDER BY CategoryID"),
        products = await db.Query("SELECT ProductID AS Id,Name FROM dbo.Products WHERE IsActive=1 ORDER BY Name"),
        // [CRM-09] quyền của người đang đăng nhập – web ẩn / hiện nút theo quyền (vd Điều chỉnh điểm)
        permissions = await db.Query("SELECT Code AS Id,Code AS Name FROM dbo.Permissions WHERE dbo.CanScope(@actor,'Chain',NULL,NULL,Code)=1", ("@actor", Actor)),
        customers = await db.Query($"""
            SELECT TOP(200) c.CustomerID AS Id,CONCAT(u.FullName,N' · ',COALESCE(u.Phone,u.Email)) AS Name FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
            WHERE u.IsActive=1 AND u.DeletedAt IS NULL AND {CustomerScope}
            AND (@q='' OR u.FullName LIKE @search OR u.Email LIKE @search OR u.Phone LIKE @search) ORDER BY c.CustomerID DESC
            """, ("@actor", Actor), ("@q", customerQ ?? ""), ("@search", "%" + (customerQ ?? "") + "%"))
    };

    public record AccountInput([Required, EmailAddress, MaxLength(254)] string Email,
        [Required, StringLength(120, MinimumLength=2)] string FullName,
        [Required, StringLength(200, MinimumLength=10)] string Password, [MaxLength(20)] string? Phone,
        [Required] string Role = "Customer",
        DateOnly? DateOfBirth = null, string? Gender = null); // [BUG-03] ngày sinh, giới tính (không bắt buộc) khi tạo khách

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount(AccountInput input)
    {
        // Hệ thống chỉ có 1 cửa hàng: quản lý và thu ngân được gán tự động vào cửa hàng đó.
        if (input.Role is not ("Owner" or "BranchManager" or "Staff" or "Customer"))
            throw new ApiException(400, "Vai trò không hợp lệ.");
        // Tài khoản khách: người có quyền Customers.Manage (quản lý). Tài khoản nội bộ: chỉ chủ cửa hàng.
        if (input.Role == "Customer") await RequirePermission("Customers.Manage"); else await RequireOwner();
        // [BUG-03] Kiểm tra ngày sinh / giới tính giống đăng ký (AuthController.Register).
        if (input.DateOfBirth is { } birth && (birth > DateOnly.FromDateTime(DateTime.UtcNow) || birth.Year < 1900))
            throw new ApiException(400, "Ngày sinh không hợp lệ.", [new("FIELD_INVALID", "date_of_birth")]);
        if (input.Gender is not (null or "" or "Male" or "Female" or "Other" or "Undisclosed"))
            throw new ApiException(400, "Giới tính không hợp lệ.", [new("FIELD_INVALID", "gender")]);
        // [WEB-06] Khách tạo từ web phải có số điện thoại giống đăng ký trên app (APP-06): thu ngân tra khách bằng SĐT.
        if (input.Role == "Customer" && string.IsNullOrWhiteSpace(input.Phone))
            throw new ApiException(400, "Cần nhập số điện thoại của khách hàng.", [new("FIELD_REQUIRED", "phone")]);
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @role int=(SELECT RoleID FROM dbo.Roles WHERE Code=@code);
            DECLARE @branch int=(SELECT TOP(1) BranchID FROM dbo.Branches WHERE IsActive=1 ORDER BY BranchID);
            IF @role IS NULL THROW 51100,'Role not found.',1;
            IF @code IN ('Staff','BranchManager') AND @branch IS NULL THROW 51100,'Store not found.',1;
            -- Kiểm tra trước để báo lỗi rõ ràng thay vì để vi phạm UNIQUE (UQ_Users_0) bật lên thành SqlException.
            IF EXISTS(SELECT 1 FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE NormalizedEmail=UPPER(LTRIM(RTRIM(@email))))
                THROW 51120,N'Email đã được sử dụng cho một tài khoản khác.',1;
            INSERT dbo.Users(RoleID,Email,NormalizedEmail,FullName,Phone,PasswordHash)
            VALUES(@role,@email,UPPER(LTRIM(RTRIM(@email))),@name,@phone,@hash);
            DECLARE @id int=SCOPE_IDENTITY();
            IF @code='Customer' INSERT dbo.Customers(UserID,DateOfBirth,Gender) VALUES(@id,@birth,@gender); -- [BUG-03]
            -- Quản lý và thu ngân đều gán qua StaffBranches, nên có thể có nhiều quản lý;
            -- ManagerID chỉ ghi khi cửa hàng chưa có quản lý chính (không thay thế người cũ).
            IF @code IN ('Staff','BranchManager') INSERT dbo.StaffBranches(UserID,BranchID) VALUES(@id,@branch);
            IF @code='BranchManager' UPDATE dbo.Branches SET ManagerID=@id WHERE BranchID=@branch AND ManagerID IS NULL;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Account.Create','Users',@id);
            COMMIT; SELECT @id AS UserID;
            """, ("@code", input.Role), ("@email", input.Email.Trim()), ("@name", input.FullName.Trim()), ("@phone", PhoneRule.Normalize(input.Phone)), // [BUG-04] chuẩn hóa + kiểm tra SĐT
            ("@birth", input.DateOfBirth?.ToDateTime(TimeOnly.MinValue)), ("@gender", string.IsNullOrEmpty(input.Gender) ? (input.Role == "Customer" ? "Undisclosed" : null) : input.Gender), // [BUG-03] [WEB-07] trống = "Không muốn nêu" như app
            ("@hash", new PasswordHasher<string>().HashPassword(input.Email, input.Password)), ("@actor", Actor));
        var id = (int)created.Single()["UserID"]!;
        return Created($"/v1/accounts/{id}", await ReadResource("accounts", id));
    }
    public record ActiveInput([property: System.Text.Json.Serialization.JsonRequired] bool IsActive);
    [HttpPut("accounts/{id:int}/active")]
    public async Task<IActionResult> AccountActive(int id, ActiveInput input)
    {
        await RequireOwner();
        if (id == Actor) throw new ApiException(400, "Không thể tự khóa tài khoản.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.Users SET IsActive=@active WHERE UserID=@id AND DeletedAt IS NULL;
            IF @@ROWCOUNT=0 THROW 51104,'Account not found.',1;
            IF @active=0 UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@id AND RevokedAt IS NULL;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Account.Status','Users',@id);
            COMMIT;
            """, ("@id", id), ("@active", input.IsActive), ("@actor", Actor));
        return NoContent();
    }

    public record ReplyInput([Required] string Status, [MaxLength(4000)] string? ResponseContent);
    [HttpPost("feedbacks/{id:int}:reply")]
    public async Task<IActionResult> Reply(int id, ReplyInput input)
    {
        await db.Procedure("dbo.HandleFeedback", ("@ActorID", Actor), ("@FeedbackID", id), ("@Status", input.Status), ("@Response", input.ResponseContent));
        return NoContent();
    }

    // [WEB-02] Tạo sản phẩm với nhiều size (M, L…) và tùy chọn Đá / Nóng (nhóm "Nhiệt độ") ngay từ đầu.
    // Size/Price (một size) vẫn nhận để tương thích client cũ; có Variants thì dùng Variants.
    public record VariantInput([Required, MaxLength(50)] string Name, [Range(0,100000000)] decimal Price);
    public record ProductInput([Required, MaxLength(150)] string Name, [MaxLength(2000)] string? Description,
        [Range(1,int.MaxValue)] int CategoryId, [MaxLength(50)] string? Size = null, [Range(0,100000000)] decimal? Price = null,
        VariantInput[]? Variants = null, bool HasTemperature = false);
    private static List<VariantInput> CheckVariants(VariantInput[]? variants, string? size = null, decimal? price = null)
    {
        var list = (variants ?? []).Where(v => v is not null && !string.IsNullOrWhiteSpace(v.Name))
            .Select(v => v with { Name = v.Name.Trim() }).ToList();
        if (list.Count == 0 && !string.IsNullOrWhiteSpace(size) && price is not null) list.Add(new(size.Trim(), price.Value));
        if (list.Count == 0) throw new ApiException(400, "Cần ít nhất một size và giá bán.", [new("FIELD_REQUIRED", "variants")]);
        if (list.Count > 5) throw new ApiException(400, "Tối đa 5 size cho một sản phẩm.", [new("FIELD_INVALID", "variants")]);
        if (list.Any(v => v.Name.Length > 50 || v.Price < 0 || v.Price > 100000000))
            throw new ApiException(400, "Tên size tối đa 50 ký tự, giá từ 0 đến 100.000.000.", [new("FIELD_INVALID", "variants")]);
        if (list.Select(v => v.Name.ToUpperInvariant()).Distinct().Count() != list.Count)
            throw new ApiException(400, "Tên size bị trùng.", [new("FIELD_INVALID", "variants")]);
        return list;
    }
    // Nhóm tùy chọn "Nhiệt độ" (Đá / Nóng) do 13_ui_review.sql tạo: bắt buộc chọn đúng 1.
    private const string TemperatureSql = """
        DECLARE @temp int=(SELECT TOP(1) ModifierGroupID FROM dbo.ModifierGroups WHERE Kind='Ice' AND IsActive=1 ORDER BY ModifierGroupID);
        IF @hasTemp=1 AND @temp IS NULL THROW 51100,N'Chưa có nhóm tùy chọn Nhiệt độ (Đá / Nóng). Chạy database/sql/13_ui_review.sql.',1;
        IF @hasTemp=1 AND NOT EXISTS(SELECT 1 FROM dbo.ProductModifierGroups WHERE ProductID=@id AND ModifierGroupID=@temp)
            INSERT dbo.ProductModifierGroups(ProductID,ModifierGroupID,MinSelections,MaxSelections) VALUES(@id,@temp,1,1);
        IF @hasTemp=0 AND @temp IS NOT NULL DELETE dbo.ProductModifierGroups WHERE ProductID=@id AND ModifierGroupID=@temp;
        """;
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(ProductInput input)
    {
        await RequireOwner();
        var variants = CheckVariants(input.Variants, input.Size, input.Price); // [WEB-02]
        var created = await db.Query($"""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF NOT EXISTS(SELECT 1 FROM dbo.Categories WHERE CategoryID=@category AND IsActive=1) THROW 51100,N'Danh mục không tồn tại hoặc đã ẩn.',1;
            INSERT dbo.Products(CategoryID,Name,Description) VALUES(@category,@name,@description);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.ProductVariants(ProductID,Name,BasePrice,DisplayOrder)
            SELECT @id,name,price,position+1 FROM OPENJSON(@variants) WITH(name nvarchar(50),price decimal(18,2),position int); -- [WEB-02] nhiều size
            {TemperatureSql}
            INSERT dbo.BranchProducts(BranchID,ProductID) SELECT BranchID,@id FROM dbo.Branches WHERE IsActive=1;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Product.Create','Products',@id);
            COMMIT; SELECT @id AS ProductID;
            """, ("@category",input.CategoryId), ("@name",input.Name.Trim()), ("@description",input.Description),
            ("@variants",JsonSerializer.Serialize(variants.Select((v,i)=>new{name=v.Name,price=v.Price,position=i}))), ("@hasTemp",input.HasTemperature), ("@actor",Actor));
        var id = (int)created.Single()["ProductID"]!;
        return Created($"/v1/products/{id}", await ReadResource("products", id));
    }
    [HttpPut("products/{id:int}/active")]
    public async Task<IActionResult> ProductActive(int id, ActiveInput input)
    {
        await RequireOwner();
        await db.Query("UPDATE dbo.Products SET IsActive=@active WHERE ProductID=@id; IF @@ROWCOUNT=0 THROW 51104,'Product not found.',1;", ("@id",id), ("@active",input.IsActive));
        return NoContent();
    }
    // [UI2-IMG] Tải ảnh sản phẩm từ máy lên (JPG / PNG / WEBP, tối đa 2 MB). File lưu ở wwwroot/uploads/products của API,
    // Products.ImageURL = "/uploads/products/<tên file>"; app khách ghép với địa chỉ API để hiển thị (ưu tiên hơn ảnh có sẵn trong app).
    [HttpPost("products/{id:int}/image"), RequestSizeLimit(3_000_000)]
    public async Task<object> UploadProductImage(int id, IFormFile file)
    {
        await RequireOwner();
        if (file is null || file.Length == 0) throw new ApiException(400, "Vui lòng chọn file ảnh.");
        if (file.Length > 2 * 1024 * 1024) throw new ApiException(400, "Ảnh tối đa 2 MB.");
        var head = new byte[12];
        await using (var s = file.OpenReadStream()) { _ = await s.ReadAsync(head); }
        // Nhận dạng theo nội dung file (không tin phần đuôi tên file).
        var ext = head[0] == 0xFF && head[1] == 0xD8 ? ".jpg"
            : head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 ? ".png"
            : head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P' ? ".webp"
            : throw new ApiException(400, "Chỉ nhận ảnh JPG, PNG hoặc WEBP.");
        var old = (await db.Query("SELECT ImageURL FROM dbo.Products WHERE ProductID=@id", ("@id", id))).SingleOrDefault()
            ?? throw new ApiException(404, "Không tìm thấy sản phẩm.");
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var folder = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", "products");
        Directory.CreateDirectory(folder);
        var name = $"product-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        await using (var target = System.IO.File.Create(Path.Combine(folder, name))) { await file.CopyToAsync(target); }
        var url = "/uploads/products/" + name;
        await db.Query("""
            UPDATE dbo.Products SET ImageURL=@url WHERE ProductID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Product.Image','Products',@id,@url);
            """, ("@id", id), ("@url", url), ("@actor", Actor));
        // Xóa ảnh tải lên trước đó của sản phẩm này (không đụng tới ảnh mẫu).
        if (old["ImageURL"] is string prev && prev.StartsWith("/uploads/products/"))
        {
            var prevPath = Path.Combine(folder, Path.GetFileName(prev));
            if (System.IO.File.Exists(prevPath)) System.IO.File.Delete(prevPath);
        }
        return new { imageUrl = url };
    }

    // [UI2-A4a] "Tạm hết / Còn hàng" (BranchProducts.IsAvailable) – khác với "Ngừng bán" (Products.IsActive, ẩn khỏi thực đơn).
    // App hiện nhãn "Tạm hết" và không cho đặt khi IsAvailable = 0. Trước đây Admin không chỉnh được trạng thái này.
    [HttpPut("products/{id:int}/availability")]
    public async Task<IActionResult> ProductAvailability(int id, AvailabilityInput input)
    {
        await RequireOwner();
        await db.Query("""
            UPDATE dbo.BranchProducts SET IsAvailable=@available WHERE ProductID=@id;
            IF @@ROWCOUNT=0 THROW 51104,'Product not found.',1;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Product.Availability','Products',@id,CASE WHEN @available=1 THEN 'InStock' ELSE 'OutOfStock' END);
            """, ("@id", id), ("@available", input.IsAvailable), ("@actor", Actor));
        return NoContent();
    }
    public record AvailabilityInput([property: System.Text.Json.Serialization.JsonRequired] bool IsAvailable);
    public record PriceInput([property: System.Text.Json.Serialization.JsonRequired] [Range(0,100000000)] decimal Price);
    [HttpGet("variants/{id:int}/price")]
    public async Task<object> Price(int id)
    {
        var row = (await db.Query("SELECT VariantID,BasePrice AS Price FROM dbo.ProductVariants WHERE VariantID=@id",("@id",id))).SingleOrDefault()
            ?? throw new ApiException(404,"Không tìm thấy biến thể sản phẩm.");
        row["ResourceName"] = $"variants/{id}/price";
        return row;
    }
    [HttpPut("variants/{id:int}/price")]
    public async Task<object> Price(int id, PriceInput input)
    {
        await RequireOwner();
        var rows = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.ProductVariants SET BasePrice=@price WHERE VariantID=@id;
            IF @@ROWCOUNT=0 THROW 51104,'Variant not found.',1;
            UPDATE dbo.BranchProductVariants SET SellingPrice=@price WHERE VariantID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details)
            VALUES(@actor,'Variant.Price','ProductVariants',@id,CONVERT(nvarchar(50),@price));
            COMMIT; SELECT VariantID,BasePrice AS Price FROM dbo.ProductVariants WHERE VariantID=@id;
            """, ("@actor",Actor), ("@id",id), ("@price",input.Price));
        var row = rows.Single(); row["ResourceName"] = $"variants/{id}/price";
        return row;
    }
}
