using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// Giai đoạn 2 – yêu cầu II.2 "Admin thêm, xóa, phân quyền cho account":
// lọc tài khoản, đổi vai trò, ma trận quyền theo vai trò, nhật ký thao tác.
public sealed partial class AdminController
{
    // Quyền quản trị luôn chỉ thuộc chủ cửa hàng, không sửa qua API.
    private static readonly HashSet<string> ProtectedPermissions = ["Security.Manage", "Organization.Manage", "Catalog.Manage"];
    private static readonly string[] InternalRoles = ["Owner", "BranchManager", "Staff"];

    [HttpGet("accounts")]
    public async Task<ApiPage> ListAccounts([FromQuery] PageRequest page, string? q = null, string? role = null, bool? active = null)
    {
        await RequireOwner();
        if (role is not (null or "" or "Owner" or "BranchManager" or "Staff" or "Customer"))
            throw new ApiException(400, "Vai trò lọc không hợp lệ.");
        const string sql = """
            SELECT u.UserID,u.FullName,u.Email,u.Phone,r.Code AS Role,u.IsActive,u.CreatedAt
            FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.DeletedAt IS NULL
            AND (@q='' OR u.FullName LIKE @search OR u.Email LIKE @search OR u.Phone LIKE @search)
            AND (@role='' OR r.Code=@role) AND (@active IS NULL OR u.IsActive=@active)
            """;
        return await pages.Read(sql, ["UserID"], true, page,
            JsonSerializer.Serialize(new { Actor, section = "accounts", q = q ?? "", role = role ?? "", active }),
            [("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%"), ("@role", role ?? ""), ("@active", active)],
            "accounts", "UserID");
    }

    public record RoleInput([Required] string Role);
    [HttpPut("accounts/{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, RoleInput input)
    {
        await RequireOwner();
        if (id == Actor) throw new ApiException(400, "Không thể tự đổi vai trò của tài khoản đang đăng nhập.");
        if (!InternalRoles.Contains(input.Role))
            throw new ApiException(400, "Chỉ đổi giữa các vai trò nội bộ: chủ cửa hàng, quản lý, thu ngân.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @old varchar(30),@newRole int=(SELECT RoleID FROM dbo.Roles WHERE Code=@code);
            DECLARE @branch int=(SELECT TOP(1) BranchID FROM dbo.Branches WHERE IsActive=1 ORDER BY BranchID);
            SELECT @old=r.Code FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Roles r ON r.RoleID=u.RoleID
            WHERE u.UserID=@id AND u.DeletedAt IS NULL;
            IF @old IS NULL THROW 51104,'Account not found.',1;
            IF @newRole IS NULL THROW 51100,N'Vai trò không tồn tại.',1;
            IF @old='Customer' THROW 51120,N'Tài khoản khách hàng không chuyển thành nhân sự nội bộ (lịch sử mua hàng gắn với hồ sơ khách).',1;
            IF @old=@code THROW 51120,N'Tài khoản đã có vai trò này.',1;
            IF @old='Owner' AND NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
                WHERE r.Code='Owner' AND u.UserID<>@id AND u.IsActive=1 AND u.DeletedAt IS NULL)
                THROW 51122,N'Phải còn ít nhất một chủ cửa hàng đang hoạt động.',1;
            IF @code IN ('Staff','BranchManager') AND @branch IS NULL THROW 51100,N'Chưa có cửa hàng đang hoạt động.',1;
            -- Gỡ phân công cũ trước khi đổi vai trò (TR_Users_Role chặn nếu còn phân công).
            UPDATE dbo.StaffBranches SET IsActive=0 WHERE UserID=@id AND IsActive=1;
            UPDATE b SET ManagerID=(SELECT TOP(1) s.UserID FROM dbo.StaffBranches s JOIN dbo.Users u ON u.UserID=s.UserID
                JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE s.BranchID=b.BranchID AND s.IsActive=1 AND r.Code='BranchManager'
                AND u.IsActive=1 AND u.UserID<>@id ORDER BY s.AssignedAt)
            FROM dbo.Branches b WHERE b.ManagerID=@id;
            UPDATE dbo.Users SET RoleID=@newRole WHERE UserID=@id;
            IF @code IN ('Staff','BranchManager')
            BEGIN
                IF EXISTS(SELECT 1 FROM dbo.StaffBranches WHERE UserID=@id AND BranchID=@branch)
                    UPDATE dbo.StaffBranches SET IsActive=1,AssignedAt=SYSUTCDATETIME() WHERE UserID=@id AND BranchID=@branch;
                ELSE INSERT dbo.StaffBranches(UserID,BranchID) VALUES(@id,@branch);
            END;
            IF @code='BranchManager' UPDATE dbo.Branches SET ManagerID=@id WHERE BranchID=@branch AND ManagerID IS NULL;
            -- Quyền thay đổi: buộc đăng nhập lại để nhận vai trò mới.
            UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@id AND RevokedAt IS NULL;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Account.Role','Users',@id,CONCAT(@old,'->',@code));
            COMMIT;
            """, ("@id", id), ("@code", input.Role), ("@actor", Actor));
        return Ok(await ReadResource("accounts", id));
    }

    // [BUG-05] Chủ cửa hàng đặt lại mật khẩu cho tài khoản khác (khách quên mật khẩu, nhân viên mới…).
    // Thu hồi mọi phiên của tài khoản đó; tự đổi mật khẩu của mình dùng POST v1/auth/password.
    public record ResetPasswordInput([Required, StringLength(200, MinimumLength = 10)] string NewPassword);
    [HttpPost("accounts/{id:int}:resetPassword")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordInput input)
    {
        await RequireOwner();
        if (id == Actor) throw new ApiException(400, "Tự đổi mật khẩu của mình ở mục Đổi mật khẩu, không dùng đặt lại.");
        var email = (await db.Query("SELECT Email FROM dbo.Users WHERE UserID=@id AND DeletedAt IS NULL", ("@id", id))).SingleOrDefault()?["Email"] as string
            ?? throw new ApiException(404, "Không tìm thấy tài khoản.");
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.Users SET PasswordHash=@hash WHERE UserID=@id;
            UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@id AND RevokedAt IS NULL;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Account.ResetPassword','Users',@id);
            COMMIT;
            """, ("@id", id), ("@hash", new Microsoft.AspNetCore.Identity.PasswordHasher<string>().HashPassword(email, input.NewPassword)), ("@actor", Actor));
        return NoContent();
    }

    // Ma trận vai trò × quyền: mỗi quyền một dòng, mỗi vai trò nội bộ một cột true/false.
    [HttpGet("permissions/matrix")]
    public async Task<object> PermissionMatrix()
    {
        await RequireOwner();
        var rows = await db.Query("""
            SELECT p.Code AS Permission,p.Description,
            MAX(CASE WHEN r.Code='Owner' THEN 1 ELSE 0 END) AS Owner,
            MAX(CASE WHEN r.Code='BranchManager' THEN 1 ELSE 0 END) AS BranchManager,
            MAX(CASE WHEN r.Code='Staff' THEN 1 ELSE 0 END) AS Staff
            FROM dbo.Permissions p LEFT JOIN dbo.RolePermissions rp ON rp.PermissionID=p.PermissionID
            LEFT JOIN dbo.Roles r ON r.RoleID=rp.RoleID
            GROUP BY p.PermissionID,p.Code,p.Description ORDER BY p.PermissionID
            """);
        foreach (var row in rows) row["Locked"] = ProtectedPermissions.Contains((string)row["Permission"]!);
        return new { roles = new[] { "Owner", "BranchManager", "Staff" }, editableRoles = new[] { "BranchManager", "Staff" }, permissions = rows };
    }

    [HttpGet("audit-logs")]
    public async Task<ApiPage> AuditLogs([FromQuery] PageRequest page, string? q = null)
    {
        await RequireOwner();
        const string sql = """
            SELECT a.AuditID,a.CreatedAt,COALESCE(u.FullName,N'Hệ thống') AS Actor,a.Action,a.EntityType,a.EntityID,a.Details
            FROM dbo.AuditLogs a LEFT JOIN dbo.Users u ON u.UserID=a.ActorID
            WHERE (@q='' OR a.Action LIKE @search OR a.EntityType LIKE @search OR u.FullName LIKE @search OR a.Details LIKE @search)
            """;
        return await pages.Read(sql, ["AuditID"], true, page,
            JsonSerializer.Serialize(new { Actor, section = "audit", q = q ?? "" }),
            [("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%")]);
    }
}
