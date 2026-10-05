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
    public record ShiftInput(int UserId,int BranchId,DateTimeOffset StartsAt,DateTimeOffset EndsAt);
    [HttpPost("shifts")]
    public async Task<IActionResult> Shift(ShiftInput input)
    {
        var created = await db.Query("""
        SET XACT_ABORT ON; BEGIN TRAN;
        IF dbo.CanBranch(@actor,@branch,'Staff.Manage')=0 THROW 51103,'Scope denied.',1;
        -- Serialize per employee, including assignments to different branches.
        DECLARE @user int;
        SELECT @user=UserID FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@userId AND IsActive=1;
        IF @user IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.StaffBranches WHERE UserID=@user AND BranchID=@branch AND IsActive=1)
            THROW 51100,'Employee is not assigned to this branch.',1;
        IF @end<=@start OR DATEDIFF(hour,@start,@end)>24 THROW 51100,'Invalid shift interval.',1;
        IF EXISTS(SELECT 1 FROM dbo.StaffShifts WHERE UserID=@user AND IsCancelled=0 AND StartsAt<@end AND EndsAt>@start)
            THROW 51109,'Shift overlaps an existing assignment.',1;
        INSERT dbo.StaffShifts(UserID,BranchID,StartsAt,EndsAt,CreatedBy) VALUES(@user,@branch,@start,@end,@actor);
        DECLARE @id int=SCOPE_IDENTITY();
        INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@actor,@branch,'Shift.Create','StaffShifts',@id);
        COMMIT; SELECT @id AS ShiftID;
        """,("@actor",Actor),("@userId",input.UserId),("@branch",input.BranchId),("@start",input.StartsAt.UtcDateTime),("@end",input.EndsAt.UtcDateTime));
        var id = (int)created.Single()["ShiftID"]!;
        return Created($"/v1/shifts/{id}", await ReadResource("shifts", id));
    }
    [HttpPost("shifts/{id:int}:cancel")]
    public async Task<IActionResult> CancelShift(int id)
    {
        await db.Query("""
            UPDATE dbo.StaffShifts SET IsCancelled=1 WHERE ShiftID=@id AND dbo.CanBranch(@actor,BranchID,'Staff.Manage')=1;
            IF @@ROWCOUNT=0 THROW 51103,'Shift outside scope.',1;
            """,("@actor",Actor),("@id",id)); return NoContent();
    }
    public record PermissionInput([property: System.Text.Json.Serialization.JsonRequired] bool Allowed);
    [HttpPut("roles/{role}/permissions/{code}")]
    public async Task<IActionResult> Permission(string role, string code, PermissionInput input)
    {
        await RequireOwner();
        if(role is not ("AreaManager" or "BranchManager") ||
           code is "Security.Manage" or "Organization.Manage" or "Catalog.Manage")
            throw new ApiException(400,"Chỉ sửa quyền vận hành của vai trò quản lý khu vực/chi nhánh.");
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
