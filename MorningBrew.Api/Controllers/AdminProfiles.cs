using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [WEB-04] Sửa thông tin khách hàng (quản lý có quyền Customers.Manage) và tài khoản nội bộ (chủ cửa hàng).
// Email là tên đăng nhập nên không sửa ở đây; mật khẩu dùng "Đặt lại mật khẩu" / "Đổi mật khẩu".
public sealed partial class AdminController
{
    public record CustomerProfileInput([Required, StringLength(120, MinimumLength = 2)] string FullName,
        [Required, MaxLength(20)] string Phone, DateOnly? DateOfBirth = null, string? Gender = null);
    [HttpPut("customers/{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, CustomerProfileInput input)
    {
        await RequirePermission("Customers.Manage");
        if (input.DateOfBirth is { } birth && (birth > DateOnly.FromDateTime(DateTime.UtcNow) || birth.Year < 1900))
            throw new ApiException(400, "Ngày sinh không hợp lệ.", [new("FIELD_INVALID", "date_of_birth")]);
        if (input.Gender is not (null or "" or "Male" or "Female" or "Other" or "Undisclosed"))
            throw new ApiException(400, "Giới tính không hợp lệ.", [new("FIELD_INVALID", "gender")]);
        var phone = PhoneRule.Normalize(input.Phone) ?? throw new ApiException(400, "Cần nhập số điện thoại của khách hàng.", [new("FIELD_REQUIRED", "phone")]);
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @user int=(SELECT c.UserID FROM dbo.Customers c WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Users u ON u.UserID=c.UserID
                WHERE c.CustomerID=@id AND u.DeletedAt IS NULL);
            IF @user IS NULL THROW 51104,N'Không tìm thấy khách hàng.',1;
            UPDATE dbo.Users SET FullName=@name,Phone=@phone WHERE UserID=@user;
            UPDATE dbo.Customers SET DateOfBirth=@birth,Gender=@gender WHERE CustomerID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Customer.Update','Users',@user);
            COMMIT;
            """, ("@id", id), ("@name", input.FullName.Trim()), ("@phone", phone),
            ("@birth", input.DateOfBirth?.ToDateTime(TimeOnly.MinValue)), ("@gender", string.IsNullOrEmpty(input.Gender) ? "Undisclosed" : input.Gender),
            ("@actor", Actor));
        return NoContent();
    }

    public record AccountProfileInput([Required, StringLength(120, MinimumLength = 2)] string FullName, [MaxLength(20)] string? Phone);
    [HttpPut("accounts/{id:int}")]
    public async Task<Dictionary<string, object?>> UpdateAccount(int id, AccountProfileInput input)
    {
        await RequireOwner();
        var phone = PhoneRule.Normalize(input.Phone);
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @role varchar(30)=(SELECT r.Code FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.UserID=@id AND u.DeletedAt IS NULL);
            IF @role IS NULL THROW 51104,N'Không tìm thấy tài khoản.',1;
            IF @role='Customer' AND @phone IS NULL THROW 51120,N'Tài khoản khách hàng phải có số điện thoại.',1;
            UPDATE dbo.Users SET FullName=@name,Phone=@phone WHERE UserID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@actor,'Account.Update','Users',@id);
            COMMIT;
            """, ("@id", id), ("@name", input.FullName.Trim()), ("@phone", phone), ("@actor", Actor));
        return await ReadResource("accounts", id);
    }
}
