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
        if(role is not "BranchManager" ||
           code is "Security.Manage" or "Organization.Manage" or "Catalog.Manage")
            throw new ApiException(400,"Chỉ sửa quyền vận hành của vai trò quản lý cửa hàng.");
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
}
