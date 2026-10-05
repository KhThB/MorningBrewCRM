// File: StatsCrmController.cs
// Mô tả: Controller cung cấp endpoint thống kê CRM cho Admin với data filtering theo claims/role.
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/admin/stats/crm")]
    [Authorize(Roles = "Admin,Owner,AreaManager,BranchManager")]
    public class StatsCrmController : ControllerBase
    {
        private readonly ICrmStatsService _statsService;

        public StatsCrmController(ICrmStatsService statsService)
        {
            _statsService = statsService;
        }

        // Lấy overview dashboard CRM
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] int? branchId = null)
        {
            var user = HttpContext.User;
            var res = await _statsService.GetOverviewAsync(user, branchId);
            return Ok(res);
        }
    }
}
