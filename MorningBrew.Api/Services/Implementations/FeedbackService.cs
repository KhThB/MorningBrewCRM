using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MorningBrew.Api.Common;
using MorningBrew.Api.Domain.Entities;
using MorningBrew.Api.DTOs.Feedbacks;
using MorningBrew.Api.Services.Interfaces;

namespace MorningBrew.Api.Services.Implementations;

public class FeedbackService : IFeedbackService
{
    private readonly DbContext _db;

    public FeedbackService(DbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<FeedbackResponse> CreateFeedbackAsync(CreateFeedbackRequest request, ClaimsPrincipal? user = null)
    {
        if (request.BranchId is null && request.ProductId is null)
            throw new ArgumentException("Feedback must reference a branch, a product, or both");
        if (request.Rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(request.Rating), "Rating must be between 1 and 5");

        var customerId = await CustomerIdentity.ResolveCustomerIdAsync(_db, user)
            ?? throw new UnauthorizedAccessException("Customer identity is required");
        var feedback = new Feedback
        {
            CustomerId = customerId,
            BranchId = request.BranchId,
            ProductId = request.ProductId,
            Rating = (byte)request.Rating,
            Content = request.Content,
            FeedbackType = request.FeedbackType,
            Status = FeedbackStatus.New.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Add(feedback);
        await _db.SaveChangesAsync();
        return ToResponse(feedback);
    }

    public async Task<(FeedbackResponse[] Items, int Total)> GetFeedbacksAsync(FeedbackFilterRequest filter, ClaimsPrincipal? user = null)
    {
        var query = _db.Set<Feedback>().AsQueryable();
        if (user == null || !user.IsInRole("Owner") && !user.IsInRole("Admin")
            && !user.IsInRole("AreaManager") && !user.IsInRole("BranchManager"))
            throw new UnauthorizedAccessException("Only managers can view feedbacks");

        if (user.IsInRole("BranchManager") && int.TryParse(user.FindFirst("BranchID")?.Value, out var branchId))
            query = query.Where(x => x.BranchId == branchId);

        if (user.IsInRole("AreaManager") && int.TryParse(user.FindFirst("AreaID")?.Value, out var areaId))
            query = query.Where(x => _db.Set<Customer>().Any(customer => customer.Id == x.CustomerId
                && _db.Set<AppUser>().Any(account => account.Id == customer.UserId && account.AreaId == areaId)));
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value.ToString());
        if (filter.Rating.HasValue) query = query.Where(x => x.Rating == filter.Rating.Value);
        if (filter.BranchId.HasValue) query = query.Where(x => x.BranchId == filter.BranchId.Value);
        if (!string.IsNullOrWhiteSpace(filter.FeedbackType)) query = query.Where(x => x.FeedbackType == filter.FeedbackType);

        var total = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var feedbacks = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return (feedbacks.Select(ToResponse).ToArray(), total);
    }

    public async Task<FeedbackResponse> ReplyFeedbackAsync(int feedbackId, ReplyFeedbackRequest request, ClaimsPrincipal? user = null)
    {
        if (user == null || !user.IsInRole("Owner") && !user.IsInRole("Admin")
            && !user.IsInRole("AreaManager") && !user.IsInRole("BranchManager"))
            throw new UnauthorizedAccessException("Only managers can update feedback status");

        var feedback = await _db.Set<Feedback>().FirstOrDefaultAsync(x => x.Id == feedbackId)
            ?? throw new InvalidOperationException("Feedback not found");
        feedback.Status = request.Status.ToString();
        feedback.ResponseContent = request.ResponseContent;
        feedback.HandledBy = ParseUserId(user);
        feedback.HandledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToResponse(feedback);
    }

    private static int? ParseUserId(ClaimsPrincipal? user) =>
        int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static FeedbackResponse ToResponse(Feedback feedback) => new(
        feedback.Id, feedback.CustomerId, feedback.BranchId, feedback.ProductId, feedback.Rating ?? 0,
        feedback.Content, feedback.FeedbackType,
        Enum.TryParse<FeedbackStatus>(feedback.Status, out var status) ? status : FeedbackStatus.New,
        feedback.ResponseContent, feedback.HandledBy, feedback.HandledAt, feedback.CreatedAt);
}
