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

public class CustomerService : ICustomerService
{
    private readonly DbContext _db;

    public CustomerService(DbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // Business rule: chỉ tạo hồ sơ CRM cho tài khoản có vai trò Customer.
    public async Task<CustomerProfileDto> CreateProfileAsync(CreateCustomerProfileRequest request, ClaimsPrincipal? user = null)
    {
        _ = user;
        var account = await _db.Set<AppUser>().FirstOrDefaultAsync(x => x.Id == request.UserId);
        if (account == null)
            throw new InvalidOperationException("User account not found");

        if (!string.Equals(account.Role, "Customer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only accounts with Customer role can have a customer profile");

        if (await _db.Set<Customer>().AnyAsync(x => x.UserId == request.UserId))
            throw new InvalidOperationException("Customer profile already exists for this account");

        var customer = new Customer
        {
            UserId = request.UserId,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            LoyaltyPoints = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(customer);
        await _db.SaveChangesAsync();
        return ToDto(customer, account);
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(int customerId, ClaimsPrincipal? user = null)
    {
        customerId = await ResolveCustomerIdAsync(customerId, user);
        var result = await (from customer in _db.Set<Customer>()
                            join account in _db.Set<AppUser>() on customer.UserId equals account.Id
                            where customer.Id == customerId
                            select new { Customer = customer, Account = account })
            .FirstOrDefaultAsync();

        return result == null ? null : ToDto(result.Customer, result.Account);
    }

    public async Task<CustomerProfileDto> UpdateProfileAsync(int customerId, UpdateCustomerProfileRequest request, ClaimsPrincipal? user = null)
    {
        customerId = await ResolveCustomerIdAsync(customerId, user);
        var customer = await _db.Set<Customer>().FirstOrDefaultAsync(x => x.Id == customerId)
            ?? throw new InvalidOperationException("Customer not found");
        var account = await _db.Set<AppUser>().FirstOrDefaultAsync(x => x.Id == customer.UserId)
            ?? throw new InvalidOperationException("User account not found");

        if (request.FullName != null) account.FullName = request.FullName;
        if (request.Email != null) account.Email = request.Email;
        if (request.Phone != null) account.Phone = request.Phone;
        if (request.DateOfBirth.HasValue) customer.DateOfBirth = request.DateOfBirth;
        if (request.Gender != null) customer.Gender = request.Gender;
        if (request.Address != null) customer.Address = request.Address;

        await _db.SaveChangesAsync();
        return ToDto(customer, account);
    }

    public async Task<CustomerSearchResponse> SearchAsync(CustomerSearchRequest request, ClaimsPrincipal? user = null)
    {
        _ = user;
        var query = from customer in _db.Set<Customer>()
                    join account in _db.Set<AppUser>() on customer.UserId equals account.Id
                    select new { Customer = customer, Account = account };

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var value = request.Query.Trim();
            query = query.Where(x => EF.Functions.Like(x.Account.FullName, $"%{value}%")
                || EF.Functions.Like(x.Account.Email, $"%{value}%")
                || EF.Functions.Like(x.Account.Phone, $"%{value}%"));
        }

        if (request.MinPoints.HasValue) query = query.Where(x => x.Customer.LoyaltyPoints >= request.MinPoints.Value);
        if (request.MaxPoints.HasValue) query = query.Where(x => x.Customer.LoyaltyPoints <= request.MaxPoints.Value);
        if (request.RegisteredFrom.HasValue) query = query.Where(x => x.Customer.CreatedAt >= request.RegisteredFrom.Value);
        if (request.RegisteredTo.HasValue) query = query.Where(x => x.Customer.CreatedAt <= request.RegisteredTo.Value);

        var total = await query.CountAsync();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var items = await query.OrderByDescending(x => x.Customer.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CustomerSearchItemDto(x.Customer.Id, x.Account.FullName, x.Account.Email, x.Account.Phone,
                x.Customer.DateOfBirth, x.Customer.Gender, x.Customer.Address, x.Customer.LoyaltyPoints, x.Customer.CreatedAt))
            .ToListAsync();

        return new CustomerSearchResponse(items, total, page, pageSize);
    }

    private static CustomerProfileDto ToDto(Customer customer, AppUser account) =>
        new(customer.Id, account.FullName, account.Email, account.Phone, customer.DateOfBirth,
            customer.Gender, customer.Address, customer.LoyaltyPoints, customer.CreatedAt);

    private async Task<int> ResolveCustomerIdAsync(int requestedId, ClaimsPrincipal? user)
    {
        if (user is null)
            return requestedId;

        var customerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user);
        return customerId ?? 0;
    }
}
