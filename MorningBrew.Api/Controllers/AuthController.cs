// File: AuthController.cs
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Common;
using MorningBrew.Api.DTOs.Auth;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // AuthController: endpoint đăng nhập và trả về JWT token cho client
    // Lưu ý: Login hiện dùng mật khẩu demo; ở production cần validate bằng hash
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST api/auth/login
        // Body: LoginRequest { username, password }
        // Response: ApiResponse<LoginResponse> chứa token, expiresAt và user info
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);
            return Ok(ApiResponse<LoginResponse>.Success(response, "Login successful"));
        }
    }
}
