// File: FeedbacksController.cs
// Mô tả: Controller cho khách hàng gửi feedback.
// Ghi chú: Endpoint POST yêu cầu xác thực; service sẽ lưu feedback và trả về FeedbackResponse.
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.DTOs.Feedbacks;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbacksController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        // Khách gửi feedback (cần đăng nhập)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateFeedbackRequest request)
        {
            var user = HttpContext.User;
            var fb = await _feedbackService.CreateFeedbackAsync(request, user);
            return Ok(fb);
        }
    }
}
