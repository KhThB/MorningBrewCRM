// File: FeedbackDtos.cs
// Mô tả: DTOs cho module Feedbacks (gửi feedback bởi khách và quản lý bởi admin).
// Ghi chú: Sử dụng enums và record để mô tả request/response.
using System;

namespace MorningBrew.Api.DTOs.Feedbacks
{
    // Trạng thái feedback
    public enum FeedbackStatus { New, Processing, Resolved }

    // Request khách gửi feedback
    public record CreateFeedbackRequest(int? BranchId, int? ProductId, int Rating, string Content, string FeedbackType);

    // Request lọc khi admin lấy danh sách
    public record FeedbackFilterRequest(FeedbackStatus? Status = null, int? Rating = null, int? BranchId = null, string? FeedbackType = null, int Page = 1, int PageSize = 20);

    // DTO phản hồi feedback cho client (admin/customer)
    public record FeedbackResponse(int Id, int CustomerId, int? BranchId, int? ProductId, int Rating, string Content, string FeedbackType, FeedbackStatus Status, string? ResponseContent, int? HandledBy, DateTime? HandledAt, DateTime CreatedAt);

    // Request admin trả lời/ xử lý feedback
    public record ReplyFeedbackRequest(FeedbackStatus Status, string? ResponseContent);
}
