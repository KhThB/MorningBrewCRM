// File: CustomerController.cs
// Mô tả: Controller cho các endpoint liên quan đến khách hàng (profile, cập nhật, tìm kiếm).
// Ghi chú: Endpoints 'me' yêu cầu xác thực; endpoint 'search' chỉ cho Admin/Staff.
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.DTOs.Customers;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        // Service xử lý logic liên quan đến khách hàng
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Owner,AreaManager,BranchManager")]
        public async Task<IActionResult> CreateProfile([FromBody] CreateCustomerProfileRequest request)
        {
            var profile = await _customerService.CreateProfileAsync(request, User);
            return CreatedAtAction(nameof(GetMyProfile), profile);
        }

        // Lấy profile của user hiện tại (dựa trên claim NameIdentifier)
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var user = HttpContext.User;
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var customerId)) return Forbid();

            var profile = await _customerService.GetProfileAsync(customerId, user);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        // Cập nhật profile của user hiện tại
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateCustomerProfileRequest request)
        {
            var user = HttpContext.User;
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var customerId)) return Forbid();

            var updated = await _customerService.UpdateProfileAsync(customerId, request, user);
            return Ok(updated);
        }

        // Tìm kiếm khách hàng (Admin/Staff)
        [HttpPost("search")]
        [Authorize(Roles = "Admin,Owner,AreaManager,BranchManager")]
        public async Task<IActionResult> Search([FromBody] CustomerSearchRequest request)
        {
            var user = HttpContext.User;
            var result = await _customerService.SearchAsync(request, user);
            return Ok(result);
        }
    }
}
