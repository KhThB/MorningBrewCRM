using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace MorningBrew.Api.Infrastructure;

// Only a token hash is stored. Current roles and active status are checked on every request.
public sealed class SessionAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, SqlStore db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var token = header[7..].Trim();
        if (token.Length != 64) return AuthenticateResult.Fail("Invalid session.");
        var rows = await db.Query("""
            SELECT u.UserID, u.FullName, u.Email, r.Code AS Role, s.SessionID
            FROM dbo.UserSessions s JOIN dbo.Users u ON u.UserID=s.UserID JOIN dbo.Roles r ON r.RoleID=u.RoleID
            WHERE s.TokenHash=@hash AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME() AND u.IsActive=1
            """, ("@hash", Hash(token)));
        if (rows.Count == 0) return AuthenticateResult.Fail("Session expired or account disabled.");
        var row = rows[0];
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, row["UserID"]!.ToString()!),
            new Claim(ClaimTypes.Name, row["FullName"]!.ToString()!),
            new Claim(ClaimTypes.Email, row["Email"]!.ToString()!),
            new Claim(ClaimTypes.Role, row["Role"]!.ToString()!),
            new Claim("SessionID", row["SessionID"]!.ToString()!) };
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}
