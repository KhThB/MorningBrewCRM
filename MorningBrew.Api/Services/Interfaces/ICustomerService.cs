// File: ICustomerService.cs
// Mô tả: Giao diện dịch vụ cho module Customers (đọc/cập nhật profile, tìm kiếm khách hàng).
// Ghi chú: Các phương thức nhận ClaimsPrincipal để có thể áp dụng filtering theo quyền/claims.
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MorningBrew.Api.DTOs.Customers;

namespace MorningBrew.Api.Services.Interfaces
{
    public interface ICustomerService
    {
        // Chỉ tạo hồ sơ cho tài khoản có Role = Customer.
        Task<CustomerProfileDto> CreateProfileAsync(CreateCustomerProfileRequest request, ClaimsPrincipal? user = null);

        // Lấy profile khách hàng theo id. Nếu truyền user thì có thể kiểm tra quyền.
        Task<CustomerProfileDto?> GetProfileAsync(int customerId, ClaimsPrincipal? user = null);

        // Cập nhật thông tin hồ sơ của khách
        Task<CustomerProfileDto> UpdateProfileAsync(int customerId, UpdateCustomerProfileRequest request, ClaimsPrincipal? user = null);

        // Tìm kiếm / lọc khách hàng (dùng paging)
        Task<CustomerSearchResponse> SearchAsync(CustomerSearchRequest request, ClaimsPrincipal? user = null);
    }
}
