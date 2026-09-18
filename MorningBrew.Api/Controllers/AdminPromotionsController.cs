// File: AdminPromotionsController.cs
// Mô tả: Controller cho Admin/Owner/BranchManager quản lý Promotions (tạo, cập nhật, gán cho khách).
// Ghi chú: Endpoints yêu cầu role tương ứng; CreatedBy lấy từ claim NameIdentifier.
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.DTOs.Promotions;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin,Owner,BranchManager")]
    public class AdminPromotionsController : ControllerBase
    {
        private readonly IPromotionService _promotionService;

        public AdminPromotionsController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePromotionRequest request)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var userId)) return Forbid();

            var res = await _promotionService.CreateAsync(request, userId);
            return Ok(res);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreatePromotionRequest request)
        {
            var res = await _promotionService.UpdateAsync(id, request);
            return Ok(res);
        }

        [HttpPost("assign/{promotionId:int}")]
        public async Task<IActionResult> Assign(int promotionId, [FromBody] IEnumerable<int> customerIds)
        {
            await _promotionService.AssignPromotionToCustomersAsync(promotionId, customerIds);
            return NoContent();
        }
    }
}
