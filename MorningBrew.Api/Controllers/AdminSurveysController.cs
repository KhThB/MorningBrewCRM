// File: AdminSurveysController.cs
// Mô tả: Controller cho Admin quản lý khảo sát: tạo, gửi và xem analytics.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.DTOs.Surveys;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin,Owner")]
    public class AdminSurveysController : ControllerBase
    {
        private readonly ISurveyService _surveyService;

        public AdminSurveysController(ISurveyService surveyService)
        {
            _surveyService = surveyService;
        }

        // Tạo khảo sát cùng câu hỏi
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSurveyRequest request)
        {
            var id = await _surveyService.CreateSurveyAsync(request, HttpContext.User);
            return Ok(new { SurveyId = id });
        }

        // Gửi khảo sát cho danh sách khách
        [HttpPost("send/{surveyId:int}")]
        public async Task<IActionResult> Send(int surveyId, [FromBody] IEnumerable<int> customerIds)
        {
            await _surveyService.SendSurveyToCustomersAsync(surveyId, customerIds);
            return NoContent();
        }

        // Xem analytics của khảo sát
        [HttpGet("{id:int}/analytics")]
        public async Task<IActionResult> Analytics(int id)
        {
            var res = await _surveyService.GetSurveyAnalyticsAsync(id);
            return Ok(res);
        }
    }
}
