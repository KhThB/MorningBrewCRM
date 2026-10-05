// File: PromotionDtos.cs
// Mô tả: DTOs cho module Promotions và CustomerPromotions.
// Ghi chú: Record dùng cho create/update (Admin) và view (customer wallet).
using System;
using System.Collections.Generic;

namespace MorningBrew.Api.DTOs.Promotions
{
    // Request từ Admin để tạo/sửa Promotion
    public record CreatePromotionRequest(string Code, string Title, string? Description, string DiscountType, decimal DiscountValue, DateTime StartAt, DateTime EndAt, bool IsActive = true, int? BranchId = null);

    // Response trả cho client thông tin Promotion
    public record PromotionResponse(int Id, string Code, string Title, string? Description, string DiscountType, decimal DiscountValue, DateTime StartAt, DateTime EndAt, bool IsActive, int? BranchId, int CreatedBy, DateTime CreatedAt);

    // Trạng thái voucher trong ví khách hàng
    public enum CustomerPromotionStatus { Available, Used, Expired }

    // Thông tin voucher của khách hàng
    public record CustomerPromotionResponse(int Id, int PromotionId, string Code, string Title, CustomerPromotionStatus Status, DateTime? UsedAt, DateTime AssignedAt);
}
