// File: ICrmStatsService.cs
// Mô tả: Giao diện dịch vụ thống kê CRM (dashboard overview)
using System.Security.Claims;
using System.Threading.Tasks;
using MorningBrew.Api.DTOs.Surveys;

namespace MorningBrew.Api.Services.Interfaces
{
    public interface ICrmStatsService
    {
        Task<CrmOverviewStatsResponse> GetOverviewAsync(ClaimsPrincipal? user = null, int? branchId = null);
    }
}
