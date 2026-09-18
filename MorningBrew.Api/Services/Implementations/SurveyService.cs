using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Common;
using Entities = MorningBrew.Api.Domain.Entities;
using MorningBrew.Api.DTOs.Surveys;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Services.Implementations;

public class SurveyService : ISurveyService
{
    private readonly DbContext _db;

    public SurveyService(DbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<int> CreateSurveyAsync(CreateSurveyRequest request, ClaimsPrincipal? user = null)
    {
        var createdBy = ParseUserId(user) ?? 0;
        var survey = new Entities.Survey
        {
            Title = request.Title,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status is "Draft" or "Open" or "Closed" ? request.Status : "Draft",
            CreatedBy = createdBy
        };
        _db.Add(survey);
        await _db.SaveChangesAsync();

        foreach (var question in request.Questions ?? Enumerable.Empty<CreateQuestionRequest>())
        {
            _db.Add(new Entities.SurveyQuestion
            {
                SurveyId = survey.Id,
                QuestionText = question.QuestionText,
                QuestionType = question.QuestionType,
                OptionsJSON = question.OptionsJSON,
                DisplayOrder = question.DisplayOrder
            });
        }

        await _db.SaveChangesAsync();
        return survey.Id;
    }

    public async Task SendSurveyToCustomersAsync(int surveyId, IEnumerable<int> customerIds)
    {
        if (!await _db.Set<Entities.Survey>().AnyAsync(x => x.Id == surveyId))
            throw new InvalidOperationException("Survey not found");

        var ids = customerIds.Distinct().ToList();
        var customerCount = await _db.Set<Entities.Customer>()
            .CountAsync(customer => ids.Contains(customer.Id));
        if (customerCount != ids.Count)
            throw new InvalidOperationException("One or more customers were not found");

        var existing = await _db.Set<Entities.SurveyRecipient>()
            .Where(x => x.SurveyId == surveyId && ids.Contains(x.CustomerId))
            .Select(x => x.CustomerId)
            .ToListAsync();
        var existingSet = existing.ToHashSet();

        var recipients = ids.Where(id => !existingSet.Contains(id)).Select(id => new Entities.SurveyRecipient
        {
            SurveyId = surveyId,
            CustomerId = id,
            SentAt = DateTime.UtcNow,
            Status = "Sent"
        });
        _db.AddRange(recipients);
        await _db.SaveChangesAsync();
    }

    public async Task<SurveyAnalyticsResponse> GetSurveyAnalyticsAsync(int surveyId)
    {
        if (!await _db.Set<Entities.Survey>().AnyAsync(survey => survey.Id == surveyId))
            throw new InvalidOperationException("Survey not found");

        var query = _db.Set<Entities.SurveyRecipient>().Where(x => x.SurveyId == surveyId);
        var total = await query.CountAsync();
        var completed = await query.CountAsync(x => x.Status == "Completed");
        return new SurveyAnalyticsResponse(surveyId, total, completed, total == 0 ? 0 : Math.Round((double)completed / total * 100, 2));
    }

    public async Task<IEnumerable<SurveyResponse>> GetActiveSurveysAsync(int customerId, ClaimsPrincipal? user = null)
    {
        var authenticatedCustomerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user);
        if (authenticatedCustomerId.HasValue)
            customerId = authenticatedCustomerId.Value;

        var now = DateTime.UtcNow;
        return await (from survey in _db.Set<Entities.Survey>().Include(x => x.Questions)
                      join recipient in _db.Set<Entities.SurveyRecipient>() on survey.Id equals recipient.SurveyId
                            where recipient.CustomerId == customerId && recipient.Status == "Sent"
                            && survey.Status == "Open" && survey.StartDate <= now && survey.EndDate >= now
                      select new SurveyResponse(survey.Id, survey.Title, survey.Description, survey.StartDate,
                          survey.EndDate, survey.Status, survey.Questions.OrderBy(x => x.DisplayOrder)
                              .Select(x => new SurveyQuestionResponse(x.Id, x.QuestionText, x.QuestionType, x.OptionsJSON, x.DisplayOrder))))
            .ToListAsync();
    }

    public async Task SubmitSurveyAsync(int surveyId, SubmitSurveyRequest request, ClaimsPrincipal? user = null)
    {
        var authenticatedCustomerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user);
        var customerId = authenticatedCustomerId ?? request.CustomerId;
        if (authenticatedCustomerId.HasValue && customerId != request.CustomerId)
            throw new UnauthorizedAccessException("The survey can only be submitted for the authenticated customer");

        var recipient = await _db.Set<Entities.SurveyRecipient>()
            .FirstOrDefaultAsync(x => x.SurveyId == surveyId && x.CustomerId == customerId);
        if (recipient == null) throw new InvalidOperationException("Survey not assigned to this customer");
        if (recipient.Status == "Completed") throw new InvalidOperationException("Survey already submitted");

        var questionIds = await _db.Set<Entities.SurveyQuestion>()
            .Where(question => question.SurveyId == surveyId)
            .Select(question => question.Id)
            .ToHashSetAsync();
        var answers = request.Answers?.ToList() ?? new List<SubmitAnswerRequest>();
        if (answers.Count == 0)
            throw new ArgumentException("At least one survey answer is required", nameof(request));
        if (answers.Select(answer => answer.QuestionId).Distinct().Count() != answers.Count)
            throw new ArgumentException("Each survey question may only be answered once", nameof(request));
        if (answers.Any(answer => !questionIds.Contains(answer.QuestionId)))
            throw new ArgumentException("Every answer must belong to the submitted survey", nameof(request));

        foreach (var answer in answers)
        {
            _db.Add(new Entities.SurveyResponse
            {
                QuestionId = answer.QuestionId,
                CustomerId = customerId,
                Answer = answer.Answer
            });
        }

        recipient.Status = "Completed";
        recipient.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static int? ParseUserId(ClaimsPrincipal? user) =>
        int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
