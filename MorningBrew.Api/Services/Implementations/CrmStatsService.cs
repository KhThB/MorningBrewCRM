// File: CrmStatsService.cs
// Mô tả: Cài đặt ICrmStatsService - tổng hợp các chỉ số CRM cho dashboard với data filtering theo claims/role.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Services.Interfaces;
using MorningBrew.Api.DTOs.Surveys;

namespace MorningBrew.Api.Services.Implementations
{
    public class CrmStatsService : ICrmStatsService
    {
        private readonly DbContext _db;

        public CrmStatsService(DbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // Lấy overview thống kê CRM (áp dụng data filtering theo Role/Claims)
        public async Task<CrmOverviewStatsResponse> GetOverviewAsync(ClaimsPrincipal? user = null, int? branchId = null)
        {
            // Base queries
            var customers = _db.Set<MorningBrew.Api.Domain.Entities.Customer>().AsQueryable();
            var feedbacks = _db.Set<MorningBrew.Api.Domain.Entities.Feedback>().AsQueryable();
            var surveyRecipients = _db.Set<MorningBrew.Api.Domain.Entities.SurveyRecipient>().AsQueryable();

            // Apply role-based filtering
            if (user != null && !user.IsInRole("Owner") && !user.IsInRole("Admin"))
            {
                if (user.IsInRole("AreaManager"))
                {
                    var areaClaim = user.FindFirst("AreaID")?.Value ?? user.FindFirst("areaid")?.Value;
                    if (int.TryParse(areaClaim, out var areaId))
                    {
                        customers = customers.Where(c => _db.Set<MorningBrew.Api.Domain.Entities.AppUser>()
                            .Any(a => a.Id == c.UserId && a.AreaId == areaId));
                        feedbacks = feedbacks.Where(feedback => customers.Any(customer => customer.Id == feedback.CustomerId));
                        surveyRecipients = surveyRecipients.Where(recipient => customers.Any(customer => customer.Id == recipient.CustomerId));
                    }
                }
                else if (user.IsInRole("BranchManager"))
                {
                    var branchClaim = user.FindFirst("BranchID")?.Value ?? user.FindFirst("branchid")?.Value;
                    if (int.TryParse(branchClaim, out var claimBranchId))
                    {
                        var effectiveBranchId = branchId ?? claimBranchId;
                        customers = customers.Where(c => _db.Set<MorningBrew.Api.Domain.Entities.AppUser>()
                            .Any(a => a.Id == c.UserId && a.BranchId == effectiveBranchId));
                        feedbacks = feedbacks.Where(f => f.BranchId == effectiveBranchId);
                        surveyRecipients = surveyRecipients.Where(recipient => customers.Any(customer => customer.Id == recipient.CustomerId));
                    }
                }

            if (branchId.HasValue && (user == null || user.IsInRole("Owner") || user.IsInRole("Admin")))
            {
                customers = customers.Where(c => _db.Set<MorningBrew.Api.Domain.Entities.AppUser>()
                    .Any(a => a.Id == c.UserId && a.BranchId == branchId.Value));
                feedbacks = feedbacks.Where(f => f.BranchId == branchId.Value);
            }
                else
                {
                    // non-admin: restrict to none
                    customers = customers.Where(c => false);
                    feedbacks = feedbacks.Where(f => false);
                    surveyRecipients = surveyRecipients.Where(sr => false);
                }
            }

            // Total customers
            var totalCustomers = await customers.LongCountAsync();

            // New customers in current month
            var now = DateTime.UtcNow;
            var firstOfMonth = new DateTime(now.Year, now.Month, 1);
            var newCustomers = await customers.Where(c => c.CreatedAt >= firstOfMonth).LongCountAsync();

            // Average feedback rating
            double avgRating = 0.0;
            if (await feedbacks.AnyAsync())
            {
                avgRating = await feedbacks.AverageAsync(f => (double)(f.Rating ?? 0));
            }

            // Pending feedbacks (Status = New or Processing)
            var pending = await feedbacks.Where(f => f.Status == "New" || f.Status == "Processing").LongCountAsync();

            // Active surveys: count distinct surveys that are active now
            var surveys = _db.Set<MorningBrew.Api.Domain.Entities.Survey>().AsQueryable();
            var activeSurveysQuery = surveys.Where(s => s.Status == "Active" && s.StartDate <= now && s.EndDate >= now);

            // If role is AreaManager/BranchManager, try to count only surveys that were sent to customers in scope
            long activeSurveys = 0;
            if (user != null && (user.IsInRole("AreaManager") || user.IsInRole("BranchManager")))
            {
                // join recipients with customers to filter by branch/area
                var joined = from sr in surveyRecipients
                             join c in customers on sr.CustomerId equals c.Id
                             join s in activeSurveysQuery on sr.SurveyId equals s.Id
                             select s.Id;

                activeSurveys = await joined.Distinct().LongCountAsync();
            }
            else
            {
                activeSurveys = await activeSurveysQuery.LongCountAsync();
            }

            var totalLoyaltyPoints = await customers.SumAsync(c => (long)c.LoyaltyPoints);

            var surveyResponses = await (from response in _db.Set<MorningBrew.Api.Domain.Entities.SurveyResponse>()
                                         join question in _db.Set<MorningBrew.Api.Domain.Entities.SurveyQuestion>()
                                             on response.QuestionId equals question.Id
                                         join recipient in surveyRecipients
                                             on new { SurveyId = question.SurveyId, response.CustomerId }
                                             equals new { recipient.SurveyId, recipient.CustomerId }
                                         select response.Id).LongCountAsync();

            var feedbacksByStatus = await feedbacks
                .GroupBy(f => f.Status)
                .Select(g => new { Status = g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            return new CrmOverviewStatsResponse(
                totalCustomers,
                newCustomers,
                Math.Round(avgRating, 2),
                pending,
                activeSurveys,
                totalLoyaltyPoints,
                surveyResponses,
                feedbacksByStatus);
        }
    }
}
