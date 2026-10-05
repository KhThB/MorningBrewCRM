// File: IFeedbackService.cs
// Mô tả: Giao diện dịch vụ Feedbacks xử lý gửi feedback bởi khách và các thao tác quản trị.
// Ghi chú: GetFeedbacksAsync cần áp dụng data filtering dựa trên ClaimsPrincipal (Owner/AreaManager/BranchManager).
using System.Security.Claims;
using System.Threading.Tasks;
using MorningBrew.Api.DTOs.Feedbacks;

namespace MorningBrew.Api.Services.Interfaces
{
    public interface IFeedbackService
    {
        // Khách gửi feedback
        Task<FeedbackResponse> CreateFeedbackAsync(CreateFeedbackRequest request, ClaimsPrincipal? user = null);

        // Admin lấy danh sách feedbacks với filter và data filtering theo claims/role
        Task<(FeedbackResponse[] Items, int Total)> GetFeedbacksAsync(FeedbackFilterRequest filter, ClaimsPrincipal? user = null);

        // Admin trả lời/ xử lý feedback (cập nhật Status, ResponseContent, HandledBy, HandledAt)
        Task<FeedbackResponse> ReplyFeedbackAsync(int feedbackId, ReplyFeedbackRequest request, ClaimsPrincipal? user = null);
    }
}
