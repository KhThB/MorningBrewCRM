using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;
public sealed partial class AdminController
{
    public record SupplierInput([Required,MaxLength(30)] string Code,[Required,MaxLength(150)] string Name,
        [MaxLength(20)] string? Phone,[EmailAddress,MaxLength(254)] string? Email,[MaxLength(150)] string? ProductGroup);
    [HttpPost("suppliers")]
    public async Task<IActionResult> Supplier(SupplierInput input)
    {
        await RequireOwner();
        var created = await db.Query("INSERT dbo.Suppliers(Code,Name,Phone,Email,ProductGroup) OUTPUT inserted.SupplierID VALUES(@code,@name,@phone,@email,@group)",
            ("@code",input.Code),("@name",input.Name),("@phone",input.Phone),("@email",input.Email),("@group",input.ProductGroup));
        var id = (int)created.Single()["SupplierID"]!;
        return Created($"/v1/suppliers/{id}", await ReadResource("suppliers", id));
    }
    [HttpPut("suppliers/{id:int}/active")]
    public async Task<IActionResult> SupplierActive(int id,ActiveInput input)
    {
        await RequireOwner();
        await db.Query("UPDATE dbo.Suppliers SET IsActive=@active WHERE SupplierID=@id; IF @@ROWCOUNT=0 THROW 51104,'Supplier not found.',1;",("@active",input.IsActive),("@id",id));
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
