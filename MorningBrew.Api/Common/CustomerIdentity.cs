using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Domain.Entities;

namespace MorningBrew.Api.Common;

public static class CustomerIdentity
{
    public static async Task<int?> ResolveCustomerIdAsync(
        DbContext db,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        var claim = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId))
            return null;

        return await db.Set<Customer>()
            .Where(customer => customer.UserId == userId)
            .Select(customer => (int?)customer.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
