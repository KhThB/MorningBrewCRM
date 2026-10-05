// File: SurveysController.cs
// Mô tả: Controller cho khách hàng lấy khảo sát đang hoạt động và nộp khảo sát.
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.DTOs.Surveys;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SurveysController : ControllerBase
    {
        private readonly ISurveyService _surveyService;

        public SurveysController(ISurveyService surveyService)
        {
            _surveyService = surveyService;
        }

        // Lấy danh sách khảo sát đang mở (active)
        [HttpGet("active")]
        [Authorize]
        public async Task<IActionResult> GetActive()
        {
            var user = HttpContext.User;
            var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idClaim, out var customerId)) return Forbid();

            var list = await _surveyService.GetActiveSurveysAsync(customerId, user);
            return Ok(list);
        }

        // Khách nộp khảo sát
        [HttpPost("{id:int}/submit")]
        [Authorize]
        public async Task<IActionResult> Submit(int id, [FromBody] SubmitSurveyRequest request)
        {
            var user = HttpContext.User;
            await _surveyService.SubmitSurveyAsync(id, request, user);
            return NoContent();
        }
    }
}
