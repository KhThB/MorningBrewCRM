using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = "Owner,AreaManager,BranchManager")]
public sealed partial class AdminController(SqlStore db) : ControllerBase
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

    [HttpGet("{section}")]
    public async Task<object> List(string section, string? q = null, int skip = 0)
    {
        skip = Math.Clamp(skip, 0, 1000000);
        string sql;
        switch (section.ToLowerInvariant())
        {
            case "suppliers":
                await RequireOwner();
                sql = "SELECT SupplierID,Code,Name,Phone,Email,ProductGroup,IsActive FROM dbo.Suppliers ORDER BY SupplierID DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY";
                break;
            case "shifts":
                sql = """
                    SELECT s.ShiftID,s.UserID,u.FullName,s.BranchID,b.Name AS Branch,s.StartsAt,s.EndsAt,
                    CAST(DATEDIFF(minute,s.StartsAt,s.EndsAt)/60.0 AS decimal(5,2)) AS Hours,s.IsCancelled
                    FROM dbo.StaffShifts s JOIN dbo.Users u ON u.UserID=s.UserID JOIN dbo.Branches b ON b.BranchID=s.BranchID
                    WHERE dbo.CanBranch(@actor,s.BranchID,'Staff.Manage')=1
                    ORDER BY s.StartsAt DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "customers":
                sql = $"""
                    SELECT c.CustomerID,u.FullName,u.Email,u.Phone,c.DateOfBirth,c.Gender,c.LoyaltyPoints,u.IsActive
                    FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE {CustomerScope}
                    AND (@q='' OR u.FullName LIKE @search OR u.Email LIKE @search OR u.Phone LIKE @search)
                    ORDER BY c.CustomerID DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "feedbacks":
                sql = """
                    SELECT f.FeedbackID,u.FullName,f.BranchID,f.Type,f.Content,f.Rating,f.Status,f.ResponseContent,f.CreatedAt
                    FROM dbo.Feedbacks f JOIN dbo.Customers c ON c.CustomerID=f.CustomerID JOIN dbo.Users u ON u.UserID=c.UserID
                    WHERE (f.BranchID IS NULL AND dbo.CanScope(@actor,'Chain',NULL,NULL,'Feedbacks.Manage')=1)
                    OR dbo.CanBranch(@actor,f.BranchID,'Feedbacks.Manage')=1
                    ORDER BY f.FeedbackID DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "surveys":
                sql = """
                    SELECT s.SurveyID,s.Title,s.Scope,s.AreaID,s.BranchID,s.StartsAt,s.EndsAt,s.Status,
                    (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID) AS Invited,
                    (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID AND r.CompletedAt IS NOT NULL) AS Completed
                    FROM dbo.Surveys s WHERE dbo.CanScope(@actor,s.Scope,s.AreaID,s.BranchID,'Surveys.Manage')=1
                    ORDER BY s.SurveyID DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "promotions":
                sql = """
                    SELECT PromotionID,Name,Scope,AreaID,BranchID,DiscountType,DiscountValue,PointsCost,StartsAt,EndsAt,IsActive
                    FROM dbo.Promotions WHERE dbo.CanScope(@actor,Scope,AreaID,BranchID,'Promotions.Manage')=1
                    ORDER BY PromotionID DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "products":
                sql = """
                    SELECT p.ProductID,p.Name,c.Name AS Category,v.VariantID,v.Name AS Size,v.BasePrice,p.IsActive
                    FROM dbo.Products p JOIN dbo.Categories c ON c.CategoryID=p.CategoryID JOIN dbo.ProductVariants v ON v.ProductID=p.ProductID
                    ORDER BY p.ProductID,v.DisplayOrder OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "accounts":
                await RequireOwner();
                sql = """
                    SELECT u.UserID,u.FullName,u.Email,u.Phone,r.Code AS Role,u.IsActive,u.CreatedAt
                    FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
                    ORDER BY u.UserID DESC OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "permissions":
                await RequireOwner();
                sql = """
                    SELECT r.Code AS Role,p.Code AS Permission,p.Description
                    FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.RoleID=rp.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID
                    ORDER BY r.RoleID,p.Code OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            case "staff":
                sql = """
                    SELECT u.UserID,u.FullName,u.Email,b.BranchID,b.Name AS Branch,'Staff' AS Role,s.IsActive
                    FROM dbo.StaffBranches s JOIN dbo.Users u ON u.UserID=s.UserID JOIN dbo.Branches b ON b.BranchID=s.BranchID
                    WHERE dbo.CanBranch(@actor,b.BranchID,'Customers.Read')=1
                    UNION ALL SELECT u.UserID,u.FullName,u.Email,b.BranchID,b.Name,'BranchManager',u.IsActive
                    FROM dbo.Branches b JOIN dbo.Users u ON u.UserID=b.ManagerID WHERE dbo.CanBranch(@actor,b.BranchID,'Customers.Read')=1
                    ORDER BY BranchID,UserID OFFSET @skip ROWS FETCH NEXT 50 ROWS ONLY
                    """; break;
            default: throw new ApiException(404, "Không tìm thấy chức năng.");
        }
        return await db.Query(sql, ("@actor", Actor), ("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%"), ("@skip", skip));
    }

    [HttpGet("dashboard/overview")]
    public async Task<object> Overview() => (await db.Query($"""
        SELECT (SELECT COUNT(*) FROM dbo.Customers c WHERE {CustomerScope}) AS Customers,
        (SELECT COUNT(*) FROM dbo.Feedbacks f WHERE f.Status<>'Resolved' AND
        ((f.BranchID IS NULL AND dbo.CanScope(@actor,'Chain',NULL,NULL,'Feedbacks.Manage')=1)
        OR dbo.CanBranch(@actor,f.BranchID,'Feedbacks.Manage')=1)) AS PendingFeedbacks,
        (SELECT COUNT(*) FROM dbo.Surveys s WHERE s.Status='Open' AND s.StartsAt<=SYSUTCDATETIME() AND s.EndsAt>SYSUTCDATETIME()
        AND dbo.CanScope(@actor,s.Scope,s.AreaID,s.BranchID,'Surveys.Manage')=1) AS OpenSurveys,
        (SELECT COUNT(*) FROM dbo.Branches b WHERE dbo.CanBranch(@actor,b.BranchID,'Reports.Read')=1) AS Branches
        """, ("@actor", Actor))).Single();

    [HttpGet("reports/revenue")]
    public Task<List<Dictionary<string, object?>>> Revenue(DateTime? from = null, DateTime? to = null, int? branchId = null)
        => db.Procedure("dbo.RevenueReport", ("@ActorID", Actor), ("@FromUTC", from ?? DateTime.UtcNow.Date.AddDays(-30)),
            ("@ToUTC", to ?? DateTime.UtcNow.Date.AddDays(1)), ("@BranchID", branchId));

    [HttpGet("lookups/options")]
    public async Task<object> Options() => new
    {
        branches = await db.Query("SELECT BranchID AS Id,Name FROM dbo.Branches WHERE dbo.CanBranch(@actor,BranchID,'Orders.Read')=1 ORDER BY BranchID", ("@actor", Actor)),
        areas = await db.Query("SELECT AreaID AS Id,Name FROM dbo.Areas WHERE dbo.CanScope(@actor,'Area',AreaID,NULL,'Surveys.Manage')=1 ORDER BY AreaID", ("@actor", Actor)),
        categories = await db.Query("SELECT CategoryID AS Id,Name FROM dbo.Categories WHERE IsActive=1 ORDER BY CategoryID"),
        staff = await db.Query("SELECT u.UserID AS Id,CONCAT(u.FullName,' · ',b.Name) AS Name FROM dbo.StaffBranches s JOIN dbo.Users u ON u.UserID=s.UserID JOIN dbo.Branches b ON b.BranchID=s.BranchID WHERE u.IsActive=1 AND s.IsActive=1 AND dbo.CanBranch(@actor,b.BranchID,'Staff.Manage')=1 ORDER BY u.UserID",("@actor",Actor)),
        customers = await db.Query($"SELECT TOP(200) c.CustomerID AS Id,u.FullName AS Name FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE u.IsActive=1 AND {CustomerScope} ORDER BY c.CustomerID DESC", ("@actor", Actor))
    };

    public record AccountInput([Required, EmailAddress, MaxLength(254)] string Email,
        [Required, StringLength(120, MinimumLength=2)] string FullName,
        [Required, StringLength(200, MinimumLength=10)] string Password, [MaxLength(20)] string? Phone,
        [Required] string Role = "Customer", int? BranchId = null, int? AreaId = null);

    [HttpPost("accounts")]
    public async Task<object> CreateAccount(AccountInput input)
    {
        await RequireOwner();
        if (input.Role is not ("Owner" or "AreaManager" or "BranchManager" or "Staff" or "Customer"))
            throw new ApiException(400, "Vai trò không hợp lệ.");
        if ((input.Role == "Staff" && input.BranchId is null) || (input.Role == "AreaManager" && input.AreaId is null))
            throw new ApiException(400, "Cần chọn chi nhánh hoặc khu vực phụ trách.");
        return await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @role int=(SELECT RoleID FROM dbo.Roles WHERE Code=@code);
            INSERT dbo.Users(RoleID,Email,NormalizedEmail,FullName,Phone,PasswordHash)
            VALUES(@role,@email,UPPER(LTRIM(RTRIM(@email))),@name,@phone,@hash);
            DECLARE @id int=SCOPE_IDENTITY();
            IF @code='Customer' INSERT dbo.Customers(UserID) VALUES(@id);
            IF @code='Staff' INSERT dbo.StaffBranches(UserID,BranchID) VALUES(@id,@branch);
            IF @code='AreaManager' INSERT dbo.AreaManagers(UserID,AreaID) VALUES(@id,@area);
            IF @code='BranchManager' AND @branch IS NOT NULL BEGIN
                UPDATE dbo.Branches SET ManagerID=@id WHERE BranchID=@branch;
                IF @@ROWCOUNT=0 THROW 51100,'Branch not found.',1;
            END;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Account.Create','Users',@id);
            COMMIT; SELECT @id AS UserID;
            """, ("@code", input.Role), ("@email", input.Email.Trim()), ("@name", input.FullName.Trim()), ("@phone", input.Phone),
            ("@hash", new PasswordHasher<string>().HashPassword(input.Email, input.Password)), ("@branch", input.BranchId), ("@area", input.AreaId), ("@actor", Actor));
    }
    public record ActiveInput(bool IsActive);
    [HttpPut("accounts/{id:int}/active")]
    public async Task<IActionResult> AccountActive(int id, ActiveInput input)
    {
        await RequireOwner();
        if (id == Actor) throw new ApiException(400, "Không thể tự khóa tài khoản.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.Users SET IsActive=@active WHERE UserID=@id;
            IF @@ROWCOUNT=0 THROW 51100,'Account not found.',1;
            IF @active=0 UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@id AND RevokedAt IS NULL;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Account.Status','Users',@id);
            COMMIT;
            """, ("@id", id), ("@active", input.IsActive), ("@actor", Actor));
        return NoContent();
    }

    public record ReplyInput([Required] string Status, [MaxLength(4000)] string? ResponseContent);
    [HttpPost("feedbacks/{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, ReplyInput input)
    {
        await db.Procedure("dbo.HandleFeedback", ("@ActorID", Actor), ("@FeedbackID", id), ("@Status", input.Status), ("@Response", input.ResponseContent));
        return NoContent();
    }

    public record ProductInput([Required, MaxLength(150)] string Name, [MaxLength(2000)] string? Description,
        [Range(1,int.MaxValue)] int CategoryId, [Required, MaxLength(60)] string Size, [Range(0,100000000)] decimal Price);
    [HttpPost("products")]
    public async Task<object> CreateProduct(ProductInput input)
    {
        await RequireOwner();
        return await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            INSERT dbo.Products(CategoryID,Name,Description) VALUES(@category,@name,@description);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.ProductVariants(ProductID,Name,BasePrice,DisplayOrder) VALUES(@id,@size,@price,0);
            INSERT dbo.BranchProducts(BranchID,ProductID) SELECT BranchID,@id FROM dbo.Branches WHERE IsActive=1;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Product.Create','Products',@id);
            COMMIT; SELECT @id AS ProductID;
            """, ("@category",input.CategoryId), ("@name",input.Name), ("@description",input.Description), ("@size",input.Size), ("@price",input.Price), ("@actor",Actor));
    }
    [HttpPut("products/{id:int}/active")]
    public async Task<IActionResult> ProductActive(int id, ActiveInput input)
    {
        await RequireOwner();
        await db.Query("UPDATE dbo.Products SET IsActive=@active WHERE ProductID=@id", ("@id",id), ("@active",input.IsActive));
        return NoContent();
    }
    public record PriceInput(int BranchId,int VariantId,[Range(0,100000000)] decimal Price,bool IsAvailable=true);
    [HttpPost("products/price")]
    public async Task<IActionResult> Price(PriceInput input)
    {
        await db.Procedure("dbo.SetBranchPrice", ("@ActorID",Actor), ("@BranchID",input.BranchId), ("@VariantID",input.VariantId), ("@Price",input.Price), ("@IsAvailable",input.IsAvailable));
        return NoContent();
    }
}
