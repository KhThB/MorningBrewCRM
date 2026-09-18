// File: IPromotionService.cs
// Mô tả: Giao diện dịch vụ cho module Promotions. Bao gồm API tạo/sửa, gán voucher cho khách, validate và sử dụng voucher.
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MorningBrew.Api.DTOs.Promotions;

namespace MorningBrew.Api.Services.Interfaces
{
    public interface IPromotionService
    {
        // Tạo promotion (Admin)
        Task<PromotionResponse> CreateAsync(CreatePromotionRequest request, int createdBy);

        // Cập nhật promotion (Admin)
        Task<PromotionResponse> UpdateAsync(int id, CreatePromotionRequest request);

        // Lấy danh sách promotion công khai (khách hàng thấy)
        Task<IEnumerable<PromotionResponse>> GetPublicAsync();

        // Gán promotion cho danh sách khách hàng (Admin / hệ thống)
        Task AssignPromotionToCustomersAsync(int promotionId, IEnumerable<int> customerIds);

        // Kiểm tra mã voucher và đánh dấu đã sử dụng khi khách áp dụng (trả về thông tin customer promotion)
        Task<CustomerPromotionResponse> ValidateAndUsePromotionAsync(string code, int customerId, int orderId, ClaimsPrincipal? user = null);

        // Lấy ví voucher của khách
        Task<IEnumerable<CustomerPromotionResponse>> GetCustomerWalletAsync(int customerId, ClaimsPrincipal? user = null);
    }
}
