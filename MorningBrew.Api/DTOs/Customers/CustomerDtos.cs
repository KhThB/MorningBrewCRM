// File: CustomerDtos.cs
// Mô tả: DTOs (records) cho module Customers và Loyalty. Sử dụng C# record theo yêu cầu.
// Ghi chú: Các DTO này dùng cho Request/Response trong API.
using System;
using System.Collections.Generic;

namespace MorningBrew.Api.DTOs.Customers
{
    // Thông tin hồ sơ khách hàng trả về cho client
    public record CustomerProfileDto(int Id, string? FullName, string? Email, string? Phone, DateTime? DateOfBirth, string? Gender, string? Address, int LoyaltyPoints, DateTime CreatedAt);

    // Dữ liệu tạo hồ sơ CRM. UserId phải trỏ tới tài khoản có Role = Customer.
    public record CreateCustomerProfileRequest(int UserId, DateTime? DateOfBirth, string? Gender, string? Address);

    // Request để cập nhật hồ sơ khách hàng
    public record UpdateCustomerProfileRequest(string? FullName, string? Email, string? Phone, DateTime? DateOfBirth, string? Gender, string? Address);

    // Preferences dạng key/value (ví dụ: sở thích đồ uống)
    public record CustomerPreferencesDto(int CustomerId, Dictionary<string,string> Preferences);

    // Request tìm kiếm/lọc khách hàng
    public record CustomerSearchRequest(string? Query, int? MinPoints, int? MaxPoints, DateTime? RegisteredFrom, DateTime? RegisteredTo, int Page = 1, int PageSize = 20);

    // Một item kết quả tìm kiếm
    public record CustomerSearchItemDto(int Id, string? FullName, string? Email, string? Phone, DateTime? DateOfBirth, string? Gender, string? Address, int LoyaltyPoints, DateTime CreatedAt);

    // Response paged cho tìm kiếm khách hàng
    public record CustomerSearchResponse(IEnumerable<CustomerSearchItemDto> Items, int TotalCount, int Page, int PageSize);

    // Kiểu giao dịch loyalty
    public enum LoyaltyTransactionType { Earn, Redeem, Adjust }

    // DTO cho 1 giao dịch loyalty
    public record LoyaltyTransactionDto(int Id, int CustomerId, LoyaltyTransactionType Type, int Points, string? Description, DateTime CreatedAt);

    // Response lịch sử điểm (paged)
    public record LoyaltyHistoryResponse(IEnumerable<LoyaltyTransactionDto> Transactions, int TotalCount, int Page, int PageSize);
}
