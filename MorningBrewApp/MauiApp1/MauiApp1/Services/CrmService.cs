namespace MauiApp1.Services;

/// <summary>Các chức năng CRM của khách — dùng API /v1/me/... đã có sẵn.</summary>
public sealed class CrmService(ApiClient api)
{
    public Profile? CachedProfile { get; private set; }
    public int PendingSurveys { get; private set; }
    public event Action? Changed;

    // ----- Hồ sơ -----
    public async Task<Profile> GetProfileAsync()
    {
        CachedProfile = await api.Get<Profile>("v1/me/profile");
        Changed?.Invoke();
        return CachedProfile;
    }

    /// <summary>PATCH hồ sơ kèm If-Match (ETag) để không ghi đè cập nhật ở nơi khác.</summary>
    public async Task<Profile> UpdateProfileAsync(string etag, string fullName, string? phone, DateOnly? birthday, string gender)
    {
        var body = new Dictionary<string, object?>
        {
            ["full_name"] = fullName.Trim(),
            ["phone"] = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            ["date_of_birth"] = birthday?.ToString("yyyy-MM-dd"),
            ["gender"] = gender
        };
        CachedProfile = await api.Send<Profile>(HttpMethod.Patch, "v1/me/profile", body, etag);
        Changed?.Invoke();
        return CachedProfile;
    }

    // ----- Điểm, ưu đãi, voucher -----
    public async Task<List<LoyaltyTransaction>> GetLoyaltyAsync() =>
        (await api.Page<LoyaltyTransaction>("v1/me/loyalty")).Items.OrderByDescending(x => x.CreatedAt).ToList();

    public async Task<List<Voucher>> GetWalletAsync() =>
        (await api.Page<Voucher>("v1/me/wallet")).Items;

    public async Task<List<Promotion>> GetPromotionsAsync() =>
        (await api.Page<Promotion>("v1/me/promotions")).Items;

    public async Task RedeemAsync(int promotionId, Guid requestKey)
    {
        await api.Send(HttpMethod.Post, $"v1/me/promotions/{promotionId}:redeem", new { RequestKey = requestKey });
        await GetProfileAsync(); // cập nhật số điểm
    }

    // ----- Phản hồi -----
    public async Task<List<Feedback>> GetFeedbacksAsync() =>
        (await api.Page<Feedback>("v1/me/feedbacks")).Items.OrderByDescending(x => x.CreatedAt).ToList();

    public Task SendFeedbackAsync(string type, int? rating, string content, int? productId, int? branchId, int? orderId) =>
        api.Send(HttpMethod.Post, "v1/me/feedbacks", new
        {
            Content = content.Trim(), Type = type, Rating = rating,
            OrderId = orderId, BranchId = branchId, ProductId = productId
        });

    // ----- Khảo sát -----
    public async Task<List<SurveyItem>> GetSurveysAsync()
    {
        var items = (await api.Page<SurveyItem>("v1/me/surveys")).Items;
        PendingSurveys = items.Count(x => x.CanAnswer);
        Changed?.Invoke();
        return items;
    }

    public Task<SurveyDetail> GetSurveyAsync(int id) => api.Get<SurveyDetail>($"v1/me/surveys/{id}");

    public record Answer(int QuestionId, string? Text, int? Rating, int[]? OptionIds);

    public async Task SubmitSurveyAsync(int id, List<Answer> answers)
    {
        await api.Send(HttpMethod.Post, $"v1/me/surveys/{id}:submit", new { Answers = answers });
        PendingSurveys = Math.Max(0, PendingSurveys - 1);
        Changed?.Invoke();
    }

    // ----- Sở thích (món yêu thích) -----
    public async Task<HashSet<int>> GetFavoritesAsync() =>
        (await api.Page<Preference>("v1/me/preferences", 100)).Items.Select(x => x.ProductId).ToHashSet();

    public Task SaveFavoritesAsync(IEnumerable<int> productIds) =>
        api.Send(HttpMethod.Put, "v1/me/preferences", new { ProductIds = productIds.Distinct().ToArray() });

    public void Reset() { CachedProfile = null; PendingSurveys = 0; Changed?.Invoke(); }
}
