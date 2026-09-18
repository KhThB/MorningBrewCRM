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
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin,Owner,AreaManager,BranchManager")]
    public class AdminFeedbacksController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;

        public AdminFeedbacksController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        [HttpPost("list")]
        public async Task<IActionResult> List([FromBody] FeedbackFilterRequest filter)
        {
            var user = HttpContext.User;
            var (items, total) = await _feedbackService.GetFeedbacksAsync(filter, user);
            return Ok(new { items, total });
        }

        /// <summary>
        /// Reply to a feedback: update Status, ResponseContent, HandledBy and HandledAt.
        /// The service will record the current admin (from ClaimsPrincipal) as HandledBy.
        /// </summary>
        [HttpPut("{id:int}/reply")]
        public async Task<IActionResult> Reply(int id, [FromBody] ReplyFeedbackRequest request)
        {
            // Current admin user performing the reply
            var user = HttpContext.User;

            // Perform reply/update via service
            var res = await _feedbackService.ReplyFeedbackAsync(id, request, user);

            // Return updated feedback
            return Ok(res);
        }
    }
}
