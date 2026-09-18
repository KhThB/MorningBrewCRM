// File: AuthService.cs
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MorningBrew.Api.Common;
using MorningBrew.Api.Domain.Entities;
using MorningBrew.Api.DTOs.Auth;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly DbContext _db;
        private readonly JwtSettings _jwtSettings;

        public AuthService(DbContext db, IOptions<JwtSettings> jwtSettings)
        {
            _db = db;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _db.Set<AppUser>().FirstOrDefaultAsync(u => u.UserName == request.Username);
            
            // Demo purposes: Simple password check (Hash should be used in production)
            if (user == null || user.PasswordHash != request.Password)
            {
                throw new UnauthorizedAccessException("Invalid username or password.");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.BranchId.HasValue)
            {
                claims.Add(new Claim("BranchID", user.BranchId.Value.ToString()));
            }
            
            if (user.AreaId.HasValue)
            {
                claims.Add(new Claim("AreaID", user.AreaId.Value.ToString()));
            }

            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            var userInfo = new UserInfoDto(user.Id, user.UserName, user.Email, user.Role, user.BranchId, user.AreaId);
            return new LoginResponse(tokenString, expiresAt, userInfo);
        }
    }
}
