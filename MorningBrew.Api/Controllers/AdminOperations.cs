using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;
public sealed partial class AdminController
{
    public record SupplierInput([Required,MaxLength(30)] string Code,[Required,MaxLength(150)] string Name,
        [MaxLength(20)] string? Phone,[EmailAddress,MaxLength(254)] string? Email,[MaxLength(150)] string? ProductGroup);
    // [WEB-10] Nhà cung cấp: chủ cửa hàng và quản lý có quyền Inventory.Manage (cần khi nhập kho).
    // Trước đây chỉ chủ cửa hàng; trùng mã báo lỗi rõ ràng thay vì lỗi UNIQUE; có ghi nhật ký.
    [HttpPost("suppliers")]
    public async Task<IActionResult> Supplier(SupplierInput input)
    {
        await RequirePermission("Inventory.Manage");
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF EXISTS(SELECT 1 FROM dbo.Suppliers WITH(UPDLOCK,HOLDLOCK) WHERE Code=@code) THROW 51120,N'Mã nhà cung cấp đã tồn tại.',1;
            INSERT dbo.Suppliers(Code,Name,Phone,Email,ProductGroup) VALUES(@code,@name,@phone,@email,@group);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Supplier.Create','Suppliers',@id,@name);
            COMMIT; SELECT @id AS SupplierID;
            """, ("@code",input.Code.Trim()),("@name",input.Name.Trim()),("@phone",Blank(input.Phone)),("@email",Blank(input.Email)),("@group",Blank(input.ProductGroup)),("@actor",Actor));
        var id = (int)created.Single()["SupplierID"]!;
        return Created($"/v1/suppliers/{id}", await ReadResource("suppliers", id));
    }
    // [WEB-12] Sửa thông tin nhà cung cấp.
    [HttpPut("suppliers/{id:int}")]
    public async Task<Dictionary<string,object?>> UpdateSupplier(int id,SupplierInput input)
    {
        await RequirePermission("Inventory.Manage");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF NOT EXISTS(SELECT 1 FROM dbo.Suppliers WITH(UPDLOCK,HOLDLOCK) WHERE SupplierID=@id) THROW 51104,N'Không tìm thấy nhà cung cấp.',1;
            IF EXISTS(SELECT 1 FROM dbo.Suppliers WHERE Code=@code AND SupplierID<>@id) THROW 51120,N'Mã nhà cung cấp đã tồn tại.',1;
            UPDATE dbo.Suppliers SET Code=@code,Name=@name,Phone=@phone,Email=@email,ProductGroup=@group WHERE SupplierID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Supplier.Update','Suppliers',@id,@name);
            COMMIT;
            """, ("@id",id),("@code",input.Code.Trim()),("@name",input.Name.Trim()),("@phone",Blank(input.Phone)),("@email",Blank(input.Email)),("@group",Blank(input.ProductGroup)),("@actor",Actor));
        return await ReadResource("suppliers", id);
    }
    private static string? Blank(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    [HttpPut("suppliers/{id:int}/active")]
    public async Task<IActionResult> SupplierActive(int id,ActiveInput input)
    {
        await RequirePermission("Inventory.Manage"); // [WEB-10]
        await db.Query("""
            UPDATE dbo.Suppliers SET IsActive=@active WHERE SupplierID=@id; IF @@ROWCOUNT=0 THROW 51104,'Supplier not found.',1;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Supplier.Status','Suppliers',@id,CASE WHEN @active=1 THEN 'Active' ELSE 'Inactive' END);
            """,("@active",input.IsActive),("@id",id),("@actor",Actor));
        return NoContent();
    }
    public record PermissionInput([property: System.Text.Json.Serialization.JsonRequired] bool Allowed);
    [HttpPut("roles/{role}/permissions/{code}")]
    public async Task<IActionResult> Permission(string role, string code, PermissionInput input)
    {
        await RequireOwner();
        if(role is not ("BranchManager" or "Staff") || ProtectedPermissions.Contains(code))
            throw new ApiException(400,"Chỉ sửa quyền vận hành của vai trò quản lý cửa hàng hoặc thu ngân.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @role int=(SELECT RoleID FROM dbo.Roles WHERE Code=@roleCode),@permission int=(SELECT PermissionID FROM dbo.Permissions WHERE Code=@code);
            IF @permission IS NULL THROW 51100,'Permission not found.',1;
            IF @allowed=1 BEGIN
                IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WITH(UPDLOCK,HOLDLOCK) WHERE RoleID=@role AND PermissionID=@permission)
                    INSERT dbo.RolePermissions(RoleID,PermissionID) VALUES(@role,@permission);
            END ELSE DELETE dbo.RolePermissions WHERE RoleID=@role AND PermissionID=@permission;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,Details) VALUES(@actor,'Permission.Change','RolePermissions',CONCAT(@roleCode,':',@code,':',@allowed));
            COMMIT;
            """,("@actor",Actor),("@roleCode",role),("@code",code),("@allowed",input.Allowed));
        return NoContent();
    }
    // Trang tổng quan của giao diện Quản trị: số tài khoản theo vai trò và nhật ký thao tác gần nhất.
    [HttpGet("admin/overview")]
    public async Task<object> AdminOverview()
    {
        await RequireOwner();
        var roles = await db.Query("""
            SELECT r.Code AS Role,COUNT(u.UserID) AS Accounts,
            SUM(CASE WHEN u.IsActive=1 THEN 1 ELSE 0 END) AS ActiveAccounts,
            SUM(CASE WHEN u.UserID IS NOT NULL AND u.IsActive=0 THEN 1 ELSE 0 END) AS LockedAccounts
            FROM dbo.Roles r LEFT JOIN dbo.Users u ON u.RoleID=r.RoleID AND u.DeletedAt IS NULL
            GROUP BY r.RoleID,r.Code ORDER BY r.RoleID
            """);
        var activity = await db.Query("""
            SELECT TOP(15) a.CreatedAt,COALESCE(u.FullName,N'Hệ thống') AS Actor,a.Action,a.EntityType,a.EntityID,a.Details
            FROM dbo.AuditLogs a LEFT JOIN dbo.Users u ON u.UserID=a.ActorID ORDER BY a.AuditID DESC
            """);
        return new { roles, activity };
    }
}
