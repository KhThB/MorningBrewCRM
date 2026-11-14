namespace MorningBrew.Pos.Services;

// ===== Dữ liệu trả về từ MorningBrew.Api (/v1) =====
// Tên thuộc tính PascalCase được ApiNamingPolicy đổi sang snake_case khi đọc JSON.

public record ApiPage<T>(List<T> Items, string? NextPageToken);

public record IdentityInfo(int Id, string Email, string FullName, string Role);
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, IdentityInfo User);

// [CLEAN-04] Bỏ các kiểu chỉ app khách dùng (Profile, Voucher, Promotion, Survey…, Feedback, Preference, Checkout, MenuData, OrderHistoryItem).

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
    int VariantId, string VariantName, decimal Price, bool IsAvailable = true, string? ImageUrl = null);

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

// [BUG-02] Đơn hàng thật từ GET v1/me/orders (trước đây chỉ là đơn mô phỏng trong bộ nhớ app).
public class Order
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public int BranchId { get; set; }
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
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? CourierName { get; set; }
    public string? CourierPhone { get; set; }
    public string? Note { get; set; }
    public string? ItemsText { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public bool IsPaid => PaymentStatus == "Succeeded";
    public bool CanCancel => Status is "AwaitingPayment" or "Pending";
    public string Summary => Lines.Count > 0 ? string.Join(", ", Lines.Select(l => $"{l.Quantity}× {l.Name}")) : ItemsText ?? "";
}
