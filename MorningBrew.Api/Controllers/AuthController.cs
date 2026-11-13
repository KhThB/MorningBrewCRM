using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

[ApiController, Route("v1/auth")]
public sealed class AuthController(SqlStore db) : ControllerBase
{
    public record LoginRequest([Required, EmailAddress, MaxLength(254)] string Username, [Required, MaxLength(200)] string Password);
    public record IdentityInfo(int Id, string Email, string FullName, string Role);
    public record LoginResponse(string Token, DateTime ExpiresAt, IdentityInfo User);
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<LoginResponse> Login(LoginRequest request)
    {
        var users = await db.Query("""
            SELECT u.UserID,u.Email,u.FullName,u.PasswordHash,u.IsActive,r.Code AS Role
            FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.NormalizedEmail=UPPER(LTRIM(RTRIM(@email)))
            """, ("@email", request.Username));
        var user = users.FirstOrDefault();
        // Hash verification also runs for unknown accounts.
        const string dummy = "AQAAAAIAAYagAAAAEE8G2F5hjA72TxlV9NrR092YRGzNf1+QMFGndUjRlWxuS/DMviEjHdzD5daFtHJW2w==";
        var hash = user?["PasswordHash"]?.ToString() ?? dummy;
        var valid = new PasswordHasher<string>().VerifyHashedPassword(request.Username, hash, request.Password);
        if (user is null || valid == PasswordVerificationResult.Failed)
            throw new ApiException(401, "Email hoặc mật khẩu không đúng.");
        // [BUG-11] Chỉ báo "bị khóa" khi mật khẩu đúng, để người ngoài không dò được tài khoản nào tồn tại.
        if (!(bool)user["IsActive"]!)
            throw new ApiException(403, "Tài khoản đã bị khóa. Vui lòng liên hệ cửa hàng để được hỗ trợ.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.AddHours(8);
        await db.Query("INSERT dbo.UserSessions(UserID,TokenHash,ExpiresAt,DeviceName) VALUES(@id,@hash,@expires,'MorningBrew');",
            ("@id", user["UserID"]), ("@hash", SessionAuthentication.Hash(token)), ("@expires", expiresAt));
        return new(token, expiresAt, new((int)user["UserID"]!, (string)user["Email"]!, (string)user["FullName"]!, (string)user["Role"]!));
    }
    // Yêu cầu 4.3: khách hàng tự đăng ký. Email duy nhất (không phân biệt hoa thường), mật khẩu ≥ 10 ký tự.
    public record RegisterRequest(
        [Required, EmailAddress, MaxLength(254)] string Email,
        [Required, StringLength(120, MinimumLength = 2)] string FullName,
        [MaxLength(20)] string? Phone, // [BUG-04] kiểm tra bằng PhoneRule (cho phép khoảng trắng/dấu chấm, chuẩn hóa trước khi lưu)
        [Required, StringLength(200, MinimumLength = 10)] string Password,
        DateOnly? DateOfBirth = null,
        [RegularExpression("^(Male|Female|Other|Undisclosed)$")] string? Gender = null);
    [HttpPost("register"), EnableRateLimiting("login")]
    public async Task<IActionResult> Register(RegisterRequest input)
    {
        var email = input.Email.Trim();
        var phone = PhoneRule.Normalize(input.Phone); // [BUG-04]
        if (input.DateOfBirth is { } birth && (birth > DateOnly.FromDateTime(DateTime.UtcNow) || birth.Year < 1900))
            throw new ApiException(400, "Ngày sinh không hợp lệ.", [new("FIELD_INVALID", "date_of_birth")]);
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF EXISTS(SELECT 1 FROM dbo.Users WITH(UPDLOCK,HOLDLOCK) WHERE NormalizedEmail=UPPER(LTRIM(RTRIM(@email))))
                THROW 51120,N'Email đã được sử dụng.',1;
            DECLARE @role int=(SELECT RoleID FROM dbo.Roles WHERE Code='Customer');
            INSERT dbo.Users(RoleID,Email,NormalizedEmail,FullName,Phone,PasswordHash)
            VALUES(@role,@email,UPPER(LTRIM(RTRIM(@email))),@name,@phone,@hash);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.Customers(UserID,DateOfBirth,Gender) VALUES(@id,@birth,@gender);
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@id,'Customer.Register','Users',@id);
            COMMIT; SELECT @id AS UserID;
            """, ("@email", email), ("@name", input.FullName.Trim()),
            ("@phone", phone), // [BUG-04] số đã chuẩn hóa
            ("@hash", new PasswordHasher<string>().HashPassword(email, input.Password)),
            ("@birth", input.DateOfBirth?.ToDateTime(TimeOnly.MinValue)), ("@gender", input.Gender));
        var id = (int)created.Single()["UserID"]!;
        return Created($"/v1/accounts/{id}", new { id, email, fullName = input.FullName.Trim(), role = "Customer" });
    }
    [Authorize, HttpGet("me")]
    public async Task<object> Me() => new {
        id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), fullName = User.Identity!.Name,
        email = User.FindFirstValue(ClaimTypes.Email), role = User.FindFirstValue(ClaimTypes.Role),
        branches = await db.Query("SELECT BranchID,Name,AreaID FROM dbo.Branches WHERE dbo.CanBranch(@actor,BranchID,'Orders.Read')=1 ORDER BY BranchID", ("@actor", User.FindFirstValue(ClaimTypes.NameIdentifier))) };
    // [BUG-05] Người dùng đang đăng nhập tự đổi mật khẩu (app khách, sau này POS). Phải nhập đúng mật khẩu cũ;
    // các phiên đăng nhập khác của tài khoản bị thu hồi, phiên hiện tại giữ nguyên.
    public record ChangePasswordRequest([Required, MaxLength(200)] string CurrentPassword, [Required, StringLength(200, MinimumLength = 10)] string NewPassword);
    [Authorize, HttpPost("password"), EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest input)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var row = (await db.Query("SELECT Email,PasswordHash FROM dbo.Users WHERE UserID=@id AND IsActive=1 AND DeletedAt IS NULL", ("@id", id))).SingleOrDefault()
            ?? throw new ApiException(401, "Phiên đăng nhập không còn hiệu lực.");
        var hasher = new PasswordHasher<string>();
        if (hasher.VerifyHashedPassword((string)row["Email"]!, (string)row["PasswordHash"]!, input.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new ApiException(400, "Mật khẩu hiện tại không đúng.", [new("FIELD_INVALID", "current_password")]);
        if (input.NewPassword == input.CurrentPassword)
            throw new ApiException(400, "Mật khẩu mới phải khác mật khẩu hiện tại.", [new("FIELD_INVALID", "new_password")]);
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.Users SET PasswordHash=@hash WHERE UserID=@id;
            UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@id AND RevokedAt IS NULL AND SessionID<>@session;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID) VALUES(@id,'Account.ChangePassword','Users',@id);
            COMMIT;
            """, ("@id", id), ("@hash", hasher.HashPassword((string)row["Email"]!, input.NewPassword)), ("@session", User.FindFirstValue("SessionID")));
        return NoContent();
    }
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await db.Query("UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE SessionID=@id AND RevokedAt IS NULL", ("@id", User.FindFirstValue("SessionID")));
        return NoContent();
    }
}
