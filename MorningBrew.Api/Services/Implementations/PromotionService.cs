// File: PromotionService.cs
// Mô tả: Cài đặt IPromotionService. Chịu trách nhiệm tạo, cập nhật, gán voucher, kiểm tra và sử dụng voucher.
// Ghi chú: Các phương thức này tương tác trực tiếp với DbContext; cần đảm bảo DbSet tương ứng tồn tại.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Common;
using MorningBrew.Api.Domain.Entities;
using MorningBrew.Api.DTOs.Promotions;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Services.Implementations
{
    public class PromotionService : IPromotionService
    {
        private readonly DbContext _db;

        public PromotionService(DbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<PromotionResponse> CreateAsync(CreatePromotionRequest request, int createdBy)
        {
            var p = new Promotion
            {
                Code = request.Code,
                Title = request.Title,
                Description = request.Description ?? string.Empty,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                IsActive = request.IsActive,
                BranchId = request.BranchId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _db.Add(p);
            await _db.SaveChangesAsync();

            return new PromotionResponse(p.Id, p.Code, p.Title, p.Description, p.DiscountType, p.DiscountValue, p.StartAt, p.EndAt, p.IsActive, p.BranchId, p.CreatedBy, p.CreatedAt);
        }

        public async Task<PromotionResponse> UpdateAsync(int id, CreatePromotionRequest request)
        {
            var p = await _db.Set<Promotion>().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) throw new InvalidOperationException("Promotion not found");

            p.Code = request.Code;
            p.Title = request.Title;
            p.Description = request.Description ?? string.Empty;
            p.DiscountType = request.DiscountType;
            p.DiscountValue = request.DiscountValue;
            p.StartAt = request.StartAt;
            p.EndAt = request.EndAt;
            p.IsActive = request.IsActive;
            p.BranchId = request.BranchId;

            await _db.SaveChangesAsync();

            return new PromotionResponse(p.Id, p.Code, p.Title, p.Description, p.DiscountType, p.DiscountValue, p.StartAt, p.EndAt, p.IsActive, p.BranchId, p.CreatedBy, p.CreatedAt);
        }

        public async Task<IEnumerable<PromotionResponse>> GetPublicAsync()
        {
            var now = DateTime.UtcNow;
            var q = await _db.Set<Promotion>().Where(p => p.IsActive && p.StartAt <= now && p.EndAt >= now).ToListAsync();
            return q.Select(p => new PromotionResponse(p.Id, p.Code, p.Title, p.Description, p.DiscountType, p.DiscountValue, p.StartAt, p.EndAt, p.IsActive, p.BranchId, p.CreatedBy, p.CreatedAt));
        }

        // Gán promotion cho một danh sách khách hàng (Admin hoặc hệ thống sử dụng)
        public async Task AssignPromotionToCustomersAsync(int promotionId, IEnumerable<int> customerIds)
        {
            var exists = await _db.Set<Promotion>().AnyAsync(p => p.Id == promotionId);
            if (!exists) throw new InvalidOperationException("Promotion not found");

            var ids = customerIds.Distinct().ToList();
            var existingCustomers = await _db.Set<Customer>()
                .Where(customer => ids.Contains(customer.Id))
                .Select(customer => customer.Id)
                .ToListAsync();
            if (existingCustomers.Count != ids.Count)
                throw new InvalidOperationException("One or more customers were not found");

            var existingAssignments = await _db.Set<CustomerPromotion>()
                .Where(assignment => assignment.PromotionId == promotionId && ids.Contains(assignment.CustomerId))
                .Select(assignment => assignment.CustomerId)
                .ToListAsync();
            var existingSet = existingAssignments.ToHashSet();
            var list = ids.Where(id => !existingSet.Contains(id))
                .Select(cid => new CustomerPromotion { PromotionId = promotionId, CustomerId = cid, SentAt = DateTime.UtcNow })
                .ToList();
            if (list.Count == 0)
                return;

            _db.AddRange(list);
            await _db.SaveChangesAsync();
        }

        // Validate mã voucher, kiểm tra đã được gán cho khách hay chưa, và đánh dấu đã dùng
        public async Task<CustomerPromotionResponse> ValidateAndUsePromotionAsync(string code, int customerId, int orderId, ClaimsPrincipal? user = null)
        {
            var authenticatedCustomerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user);
            if (authenticatedCustomerId.HasValue)
                customerId = authenticatedCustomerId.Value;
            if (orderId <= 0)
                throw new ArgumentOutOfRangeException(nameof(orderId), "Order ID must be positive");

            var now = DateTime.UtcNow;
            var p = await _db.Set<Promotion>().FirstOrDefaultAsync(x => x.Code == code && x.IsActive && x.StartAt <= now && x.EndAt >= now);
            if (p == null) throw new InvalidOperationException("Promotion invalid or expired");

            var cp = await _db.Set<CustomerPromotion>().FirstOrDefaultAsync(x => x.PromotionId == p.Id && x.CustomerId == customerId);
            if (cp == null) throw new InvalidOperationException("Promotion not assigned to customer");
            if (cp.Status == "Used") throw new InvalidOperationException("Promotion already used");

            cp.Status = "Used";
            cp.UsedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new CustomerPromotionResponse(cp.Id, cp.PromotionId, p.Code, p.Title, CustomerPromotionStatus.Used, cp.UsedAt, cp.AssignedAt);
        }

        // Lấy ví voucher của khách
        public async Task<IEnumerable<CustomerPromotionResponse>> GetCustomerWalletAsync(int customerId, ClaimsPrincipal? user = null)
        {
            var authenticatedCustomerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user);
            if (authenticatedCustomerId.HasValue)
                customerId = authenticatedCustomerId.Value;

            var q = from cp in _db.Set<CustomerPromotion>()
                    join p in _db.Set<Promotion>() on cp.PromotionId equals p.Id
                    where cp.CustomerId == customerId
                    select new { cp, p };

            var list = await q.ToListAsync();
            return list.Select(x => new CustomerPromotionResponse(x.cp.Id, x.p.Id, x.p.Code, x.p.Title, x.cp.Status == "Used" ? CustomerPromotionStatus.Used : CustomerPromotionStatus.Available, x.cp.UsedAt, x.cp.AssignedAt));
        }
    }
}
