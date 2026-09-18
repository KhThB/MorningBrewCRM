// File: SurveyDtos.cs
// Mô tả: DTOs cho module Surveys (Admin tạo khảo sát, khách nộp trả lời, thống kê analytics).
using System;
using System.Collections.Generic;

namespace MorningBrew.Api.DTOs.Surveys
{
    // Request để tạo khảo sát cùng danh sách câu hỏi
    public record CreateQuestionRequest(string QuestionText, string QuestionType, string? OptionsJSON, int DisplayOrder);
    public record CreateSurveyRequest(string Title, string Description, DateTime StartDate, DateTime EndDate, IEnumerable<CreateQuestionRequest> Questions, string Status = "Draft");

    // Response trả về khảo sát cho khách hàng (thay vì dùng nhầm CreateSurveyRequest)
    public record SurveyQuestionResponse(int Id, string QuestionText, string QuestionType, string? OptionsJSON, int DisplayOrder);
    public record SurveyResponse(int Id, string Title, string Description, DateTime StartDate, DateTime EndDate, string Status, IEnumerable<SurveyQuestionResponse> Questions);

    // Request nộp khảo sát bởi khách
    public record SubmitAnswerRequest(int QuestionId, string Answer);

    public record SubmitSurveyRequest(int CustomerId, IEnumerable<SubmitAnswerRequest> Answers);

    // Response analytics cho Admin
    public record SurveyAnalyticsResponse(int SurveyId, int TotalRecipients, int CompletedCount, double CompletionRate);

    // Response cho dashboard CRM
    public record CrmOverviewStatsResponse(
        long TotalCustomers,
        long NewCustomersThisMonth,
        double AverageFeedbackRating,
        long PendingFeedbacks,
        long ActiveSurveys,
        long TotalLoyaltyPoints,
        long SurveyResponses,
        IReadOnlyDictionary<string, long> FeedbacksByStatus);
}
