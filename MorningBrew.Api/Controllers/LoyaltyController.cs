// File: LoyaltyController.cs
// Mô tả: Controller cung cấp endpoint để tác động lên điểm Loyalty (earn/redeem) và xem lịch sử.
// Ghi chú: Các thao tác thay đổi điểm (earn/redeem) yêu cầu role Admin hoặc Staff.
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
    public class LoyaltyController : ControllerBase
    {
        // Service xử lý logic Loyalty
        private readonly ILoyaltyService _loyaltyService;

        public LoyaltyController(ILoyaltyService loyaltyService)
        {
            _loyaltyService = loyaltyService;
        }

        // Tích điểm cho khách (Admin/Staff gọi sau khi đặt hàng hoàn tất)
        [HttpPost("earn/{customerId:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Earn(int customerId, [FromQuery] int points, [FromQuery] string? description)
        {
            var tx = await _loyaltyService.EarnPointsAsync(customerId, points, description);
            return Ok(tx);
        }

        [HttpPost("adjust/{customerId:int}")]
        [Authorize(Roles = "Admin,Owner,AreaManager,BranchManager,Staff")]
        public async Task<IActionResult> Adjust(int customerId, [FromQuery] int delta, [FromQuery] string? description)
        {
            var tx = await _loyaltyService.AdjustPointsAsync(customerId, delta, description);
            return Ok(tx);
        }

        // Trừ điểm khi khách quy đổi (Admin/Staff)
        [HttpPost("redeem/{customerId:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Redeem(int customerId, [FromQuery] int points, [FromQuery] string? description)
        {
            var tx = await _loyaltyService.RedeemPointsAsync(customerId, points, description);
            return Ok(tx);
        }

        // Lấy lịch sử điểm của khách (cần xác thực)
        [HttpGet("history/{customerId:int}")]
        [Authorize]
        public async Task<IActionResult> GetHistory(int customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _loyaltyService.GetHistoryAsync(customerId, page, pageSize, User);
            return Ok(result);
        }
    }
}
