using System.Threading.Tasks;
using System.Security.Claims;
using MorningBrew.Api.DTOs.Customers;

namespace MorningBrew.Api.Services.Interfaces;

public interface ILoyaltyService
{
    Task<LoyaltyTransactionDto> EarnPointsAsync(int customerId, int points, string? description = null);
    Task<LoyaltyTransactionDto> RedeemPointsAsync(int customerId, int points, string? description = null);
    Task<LoyaltyTransactionDto> AdjustPointsAsync(int customerId, int delta, string? description = null);
    Task<LoyaltyHistoryResponse> GetHistoryAsync(int customerId, int page = 1, int pageSize = 50, ClaimsPrincipal? user = null);
}
