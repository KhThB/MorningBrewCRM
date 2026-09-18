using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Common;
using MorningBrew.Api.Domain.Entities;
using MorningBrew.Api.DTOs.Customers;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Services.Implementations;

public class LoyaltyService : ILoyaltyService
{
    private readonly DbContext _db;

    public LoyaltyService(DbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<LoyaltyTransactionDto> EarnPointsAsync(int customerId, int points, string? description = null)
    {
        if (points <= 0) throw new ArgumentException("Points must be positive", nameof(points));
        return ApplyTransactionAsync(customerId, points, LoyaltyTransactionType.Earn, description);
    }

    public Task<LoyaltyTransactionDto> RedeemPointsAsync(int customerId, int points, string? description = null)
    {
        if (points <= 0) throw new ArgumentException("Points must be positive", nameof(points));
        return ApplyTransactionAsync(customerId, -points, LoyaltyTransactionType.Redeem, description);
    }

    // Adjust nhận delta có thể dương hoặc âm và luôn được ghi nhận rõ là Adjust.
    public Task<LoyaltyTransactionDto> AdjustPointsAsync(int customerId, int delta, string? description = null)
    {
        if (delta == 0) throw new ArgumentException("Adjustment must not be zero", nameof(delta));
        return ApplyTransactionAsync(customerId, delta, LoyaltyTransactionType.Adjust, description);
    }

    private async Task<LoyaltyTransactionDto> ApplyTransactionAsync(
        int customerId, int delta, LoyaltyTransactionType transactionType, string? description)
    {
        var customer = await _db.Set<Customer>().FirstOrDefaultAsync(x => x.Id == customerId)
            ?? throw new InvalidOperationException("Customer not found");

        var newBalance = customer.LoyaltyPoints + delta;
        if (newBalance < 0)
            throw new InvalidOperationException("Insufficient points");

        customer.LoyaltyPoints = newBalance;
        var transaction = new LoyaltyTransaction
        {
            CustomerId = customerId,
            Type = transactionType.ToString(),
            Points = Math.Abs(delta),
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(transaction);
        await _db.SaveChangesAsync();
        return new LoyaltyTransactionDto(transaction.Id, transaction.CustomerId, transactionType,
            transaction.Points, description, transaction.CreatedAt);
    }

    public async Task<LoyaltyHistoryResponse> GetHistoryAsync(int customerId, int page = 1, int pageSize = 50, ClaimsPrincipal? user = null)
    {
        var authenticatedCustomerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user);
        if (authenticatedCustomerId.HasValue)
            customerId = authenticatedCustomerId.Value;

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Set<LoyaltyTransaction>()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var dtoItems = items.Select(x => new LoyaltyTransactionDto(
            x.Id, x.CustomerId,
            Enum.TryParse<LoyaltyTransactionType>(x.Type, out var type) ? type : LoyaltyTransactionType.Adjust,
            x.Points, null, x.CreatedAt));

        return new LoyaltyHistoryResponse(dtoItems, total, page, pageSize);
    }
}
