// File: PromotionsController.cs
// Mô tả: Controller công khai cho khách hàng xem promotion và ví voucher cá nhân.
// Ghi chú: GetPublic trả về các promotion đang hoạt động; GetWallet yêu cầu xác thực.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromotionsController : ControllerBase
    {
        private readonly IPromotionService _promotionService;

        public PromotionsController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        // Lấy danh sách promotion công khai (active & trong thời gian)
        [HttpGet("public")]
        public async Task<IActionResult> GetPublic()
        {
            var list = await _promotionService.GetPublicAsync();
            return Ok(list);
        }

        // Lấy ví voucher của khách (cần xác thực)
        [HttpGet("wallet/{customerId:int}")]
        [Authorize]
        public async Task<IActionResult> GetWallet(int customerId)
        {
            var list = await _promotionService.GetCustomerWalletAsync(customerId, User);
            return Ok(list);
        }

        // Áp dụng voucher (khách hàng) - validate mã và đánh dấu đã sử dụng
        [HttpPost("apply")]
        [Authorize]
        public async Task<IActionResult> Apply([FromQuery] string code, [FromQuery] int orderId)
        {
            var user = HttpContext.User;
            var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var customerId)) return Forbid();

            var res = await _promotionService.ValidateAndUsePromotionAsync(code, customerId, orderId, user);
            return Ok(res);
        }
    }
}
