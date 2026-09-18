// File: ISurveyService.cs
// Mô tả: Giao diện dịch vụ quản lý khảo sát (tạo, gửi, nộp, analytics)
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MorningBrew.Api.DTOs.Surveys;

namespace MorningBrew.Api.Services.Interfaces
{
    public interface ISurveyService
    {
        Task<int> CreateSurveyAsync(CreateSurveyRequest request, ClaimsPrincipal? user = null);
        Task SendSurveyToCustomersAsync(int surveyId, IEnumerable<int> customerIds);
        Task<SurveyAnalyticsResponse> GetSurveyAnalyticsAsync(int surveyId);
        Task<IEnumerable<SurveyResponse>> GetActiveSurveysAsync(int customerId, ClaimsPrincipal? user = null);
        Task SubmitSurveyAsync(int surveyId, SubmitSurveyRequest request, ClaimsPrincipal? user = null);
    }
}
