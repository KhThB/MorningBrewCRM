using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(SqlStore db) : ControllerBase
{
    public record LoginRequest([Required, EmailAddress, MaxLength(254)] string Username, [Required, MaxLength(200)] string Password);
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<object> Login(LoginRequest request)
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
        if (user is null || !(bool)user["IsActive"]! || valid == PasswordVerificationResult.Failed)
            throw new ApiException(401, "Email hoặc mật khẩu không đúng.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.AddHours(8);
        await db.Query("INSERT dbo.UserSessions(UserID,TokenHash,ExpiresAt,DeviceName) VALUES(@id,@hash,@expires,'MorningBrew');",
            ("@id", user["UserID"]), ("@hash", SessionAuthentication.Hash(token)), ("@expires", expiresAt));
        return new { token, expiresAt, user = new { id = user["UserID"], email = user["Email"], fullName = user["FullName"], role = user["Role"] } };
    }
    [Authorize, HttpGet("me")]
    public async Task<object> Me() => new {
        id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), fullName = User.Identity!.Name,
        email = User.FindFirstValue(ClaimTypes.Email), role = User.FindFirstValue(ClaimTypes.Role),
        branches = await db.Query("SELECT BranchID,Name,AreaID FROM dbo.Branches WHERE dbo.CanBranch(@actor,BranchID,'Orders.Read')=1 ORDER BY BranchID", ("@actor", User.FindFirstValue(ClaimTypes.NameIdentifier))) };
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await db.Query("UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE SessionID=@id AND RevokedAt IS NULL", ("@id", User.FindFirstValue("SessionID")));
        return NoContent();
    }
}
