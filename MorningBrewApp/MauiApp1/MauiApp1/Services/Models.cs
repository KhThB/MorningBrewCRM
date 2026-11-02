namespace MauiApp1.Services;

// ===== Dữ liệu trả về từ MorningBrew.Api (/v1) =====
// Tên thuộc tính PascalCase được ApiNamingPolicy đổi sang snake_case khi đọc JSON.

public record ApiPage<T>(List<T> Items, string? NextPageToken);

public record IdentityInfo(int Id, string Email, string FullName, string Role);
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, IdentityInfo User);

public record Profile(
    int CustomerId, string FullName, string Email, string? Phone, string? DateOfBirth,
    string? Gender, int LoyaltyPoints, DateTimeOffset? CreatedAt, string Etag)
{
    public DateOnly? Birthday =>
        DateOfBirth is { Length: >= 10 } && DateOnly.TryParse(DateOfBirth[..10], out var d) ? d : null;
}

public record LoyaltyTransaction(int LoyaltyTransactionId, string Kind, int Points, string? Note, DateTimeOffset CreatedAt);

public record Voucher(
    int CustomerPromotionId, string Name, string? Code, string Status, string DiscountType,
    decimal DiscountValue, int? PointsSpent, DateTimeOffset? ExpiresAt);

public record Promotion(
    int PromotionId, string Name, string? Description, string? Scope, string DiscountType,
    decimal DiscountValue, decimal? MaxDiscount, decimal MinSubtotal, int? PointsCost, DateTimeOffset? EndsAt);

public record Feedback(
    int FeedbackId, string Type, string Content, int? Rating, string Status,
    string? ResponseContent, DateTimeOffset CreatedAt);

public record SurveyItem(
    int SurveyId, string Title, string? Description, string Status,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? CompletedAt)
{
    public bool IsDone => CompletedAt is not null;
    public bool IsExpired => Status != "Open" || (EndsAt is not null && EndsAt <= DateTimeOffset.UtcNow)
                             || (StartsAt is not null && StartsAt > DateTimeOffset.UtcNow);
    public bool CanAnswer => !IsDone && !IsExpired;
}

public record SurveyQuestion(int QuestionId, string Text, string Type, bool IsRequired, int? RatingMin, int? RatingMax);
public record SurveyOption(int OptionId, int QuestionId, string Text);
public record SurveyDetail(List<SurveyQuestion> Questions, List<SurveyOption> Options);

public record Preference(int ProductId, string Name);

// ===== Thực đơn & đơn hàng (API khách chưa có — xem API_CAN_BO_SUNG.md) =====

public record Branch(int BranchId, string Name, string? Address);

public record MenuProduct(
    int ProductId, string Name, string? Description, int CategoryId, string Category,
    int VariantId, string VariantName, decimal Price, bool IsAvailable = true, bool IsDemo = false);

public record MenuData(Branch Branch, List<MenuProduct> Items, bool IsDemo);

public record CartLine(MenuProduct Item, int Quantity)
{
    public decimal Total => Item.Price * Quantity;
}

public record OrderLine(int ProductId, int VariantId, string Name, string VariantName, int Quantity, decimal UnitPrice);

public class Order
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total => Math.Max(0, Subtotal - Discount);
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<OrderLine> Lines { get; set; } = [];
    public bool IsDemo { get; set; }
    public bool FeedbackSent { get; set; }
}
