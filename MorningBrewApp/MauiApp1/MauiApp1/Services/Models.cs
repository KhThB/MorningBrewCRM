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
    decimal DiscountValue, int? PointsSpent, DateTimeOffset? ExpiresAt,
    decimal? MaxDiscount = null, decimal MinSubtotal = 0) // [APP-01] điều kiện giảm (khớp dbo.CreateOrder)
{
    /// <summary>[APP-01] Tiền giảm cho một tạm tính, tính giống dbo.CreateOrder (03_orders.sql):
    /// chưa đủ đơn tối thiểu → 0; % → làm tròn xuống rồi chặn ở MaxDiscount; không vượt tạm tính.</summary>
    public decimal DiscountFor(decimal subtotal)
    {
        if (subtotal <= 0 || subtotal < MinSubtotal) return 0;
        var d = DiscountType == "Percent" ? Math.Floor(subtotal * DiscountValue / 100) : DiscountValue;
        if (MaxDiscount is { } cap && d > cap) d = cap;
        return Math.Min(d, subtotal);
    }
}

public record Promotion(
    int PromotionId, string Name, string? Description, string? Scope, string DiscountType,
    decimal DiscountValue, decimal? MaxDiscount, decimal MinSubtotal, int? PointsCost, DateTimeOffset? EndsAt);

// [NOTI] Thông báo trong app (GET v1/me/notifications).
public record NotificationItem(
    int NotificationId, string Title, string Body, int? OrderId, string? OrderCode,
    int? CustomerPromotionId, int? SurveyId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt)
{
    public bool IsUnread => ReadAt is null;
}
public record NotificationList(List<NotificationItem> Items, int Unread);
public record UnreadCount(int Unread);

public record Feedback(
    int FeedbackId, string Type, string Content, int? Rating, string Status,
    string? ResponseContent, DateTimeOffset CreatedAt);

public record SurveyItem(
    int SurveyId, string Title, string? Description, string Status,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? CompletedAt)
{
    public bool IsDone => CompletedAt is not null;
    // [BUG-08] Khảo sát chưa tới ngày bắt đầu là "Sắp mở", không còn bị tính là hết hạn.
    public bool IsUpcoming => Status == "Open" && StartsAt is not null && StartsAt > DateTimeOffset.UtcNow;
    public bool IsExpired => !IsUpcoming && (Status != "Open" || (EndsAt is not null && EndsAt <= DateTimeOffset.UtcNow));
    public bool CanAnswer => !IsDone && !IsExpired && !IsUpcoming; // [BUG-08] chưa tới ngày bắt đầu thì chưa cho trả lời
}

public record SurveyQuestion(int QuestionId, string Text, string Type, bool IsRequired, int? RatingMin, int? RatingMax);
public record SurveyOption(int OptionId, int QuestionId, string Text);
public record SurveyDetail(List<SurveyQuestion> Questions, List<SurveyOption> Options);

public record Preference(int ProductId, string Name);

/// <summary>[APP-05] Địa chỉ giao hàng đã lưu (GET v1/me/addresses).</summary>
public record CustomerAddress(int AddressId, string Label, string RecipientName, string Phone, string AddressText, string? Note, bool IsDefault);

// ===== Thực đơn & đơn hàng =====

// [BUG-01] Thêm phí giao hàng / hình thức nhận hàng của cửa hàng (đọc từ GET v1/me/menu).
public record Branch(int BranchId, string Name, string? Address, decimal DeliveryFee = 0,
    bool AcceptingOrders = true, bool PickupEnabled = true, bool DeliveryEnabled = true);

/// <summary>[BUG-01] Tùy chọn của món (đường, đá, topping). MinSelections &gt; 0 = bắt buộc chọn.</summary>
public record MenuModifier(int ProductId, int GroupId, string GroupName, int MinSelections, int MaxSelections,
    int OptionId, string OptionName, decimal ExtraPrice);

/// <summary>[BUG-01] Kết quả GET v1/me/menu.</summary>
public record MenuResponse(Branch Branch, List<MenuProduct> Items, List<MenuModifier> Modifiers);

public record MenuProduct(
    int ProductId, string Name, string? Description, int CategoryId, string Category,
    int VariantId, string VariantName, decimal Price, bool IsAvailable = true, string? ImageUrl = null); // [CLEAN-02] bỏ IsDemo (không còn thực đơn mẫu)

// [BUG-02] Dòng giỏ hàng có tùy chọn (đường, đá, topping); cùng món + cùng tùy chọn thì gộp số lượng.
public record CartLine(MenuProduct Item, int Quantity, IReadOnlyList<MenuModifier>? Options = null)
{
    public IReadOnlyList<MenuModifier> Picked => Options ?? [];
    public string Key => $"{Item.VariantId}:{string.Join(',', Picked.Select(o => o.OptionId).Order())}";
    public int[] OptionIds => Picked.Select(o => o.OptionId).ToArray();
    public string OptionText => string.Join(", ", Picked.Select(o => o.OptionName));
    public decimal Total => (Item.Price + Picked.Sum(o => o.ExtraPrice)) * Quantity;
}

public record OrderLine(int ProductId, int VariantId, string Name, string VariantName, int Quantity, decimal UnitPrice,
    string? Options = null, decimal LineTotal = 0); // [BUG-02] thêm tùy chọn và thành tiền

public record OrderHistoryItem(string? FromStatus, string ToStatus, string? Reason, DateTimeOffset CreatedAt, string? ChangedByName); // [BUG-02]

/// <summary>[BUG-02] Thông tin khi đặt hàng: nhận tại quầy / giao hàng, trả trước / tiền mặt.</summary>
public record Checkout(string Fulfillment, string PaymentMode, string? ContactName, string? ContactPhone,
    string? DeliveryAddress, string? DeliveryNote, string? Note);

// [BUG-02] Đơn hàng thật từ GET v1/me/orders (trước đây chỉ là đơn mô phỏng trong bộ nhớ app).
public class Order
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string Fulfillment { get; set; } = "Pickup";
    public string PaymentMode { get; set; } = "Prepaid";
    public string? PaymentStatus { get; set; }
    public string? RefundStatus { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Total => TotalAmount;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaymentDueAt { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryStatus { get; set; }
    public string? CourierName { get; set; }
    public string? CourierPhone { get; set; }
    public string? Note { get; set; }
    public string? ItemsText { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public List<OrderHistoryItem> History { get; set; } = [];
    public bool FeedbackSent { get; set; } // [CLEAN-02] bỏ IsDemo (không còn đơn mô phỏng)
    public bool IsPaid => PaymentStatus == "Succeeded";
    public bool CanCancel => Status is "AwaitingPayment" or "Pending";
    public string Summary => Lines.Count > 0 ? string.Join(", ", Lines.Select(l => $"{l.Quantity}× {l.Name}")) : ItemsText ?? "";
}
