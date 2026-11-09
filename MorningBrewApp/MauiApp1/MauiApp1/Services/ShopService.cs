namespace MauiApp1.Services;

/// <summary>
/// Thực đơn, giỏ hàng và đơn hàng.
/// Backend chưa có API đặt hàng cho khách (chỉ có stored procedure). Khi API trả 404,
/// app chuyển sang DỮ LIỆU MẪU và hiện nhãn "Dữ liệu mẫu" để không nhầm với dữ liệu thật.
/// </summary>
public sealed class ShopService(ApiClient api)
{
    // Hệ thống chỉ có 1 cửa hàng (06_seed.sql, mã MB01).
    public static readonly Branch Store = new(1, "Morning Brew Thủ Đức", "12 Võ Văn Ngân, TP.HCM");

    public Branch CurrentBranch => menuCache?.Branch ?? Store; // [BUG-01] cửa hàng lấy từ API, Store chỉ để dự phòng
    public List<CartLine> Cart { get; } = [];
    public int? VoucherId { get; set; }
    /// <summary>[BUG-17] Hình thức nhận khách đã chọn (ô "Nhận tại quán" / "Giao hàng" ở trang chủ hoặc trong giỏ).
    /// Giỏ hàng mở ra với lựa chọn này thay vì luôn là "Nhận tại quầy".</summary>
    public string? PreferredFulfillment { get; set; } // [UI2-A2b] null = chưa chọn → Thực đơn hỏi khách khi mở

    /// <summary>[BUG-16] Xóa dữ liệu của phiên trước khi đăng xuất / phiên bị thu hồi: giỏ hàng, voucher đã chọn,
    /// hình thức nhận. Trước đây ShopService là singleton nên giỏ của tài khoản cũ còn nguyên khi đăng nhập tài khoản khác.</summary>
    public void ResetForSignOut()
    {
        Cart.Clear();
        VoucherId = null;
        VoucherDiscount = 0;
        PreferredFulfillment = null; // [UI2-A2b]
        Changed?.Invoke();
    }
    public decimal VoucherDiscount { get; set; }
    public event Action? Changed;

    private MenuData? menuCache;
    private DateTime menuLoadedAt; // [BUG-01] tự tải lại sau 1 phút để thấy giá / món mới từ Admin

    public int CartCount => Cart.Sum(x => x.Quantity);
    public decimal CartSubtotal => Cart.Sum(x => x.Total);

    // ----- Thực đơn -----
    // [BUG-01] Đọc thực đơn thật từ GET v1/me/menu. Dữ liệu mẫu chỉ dùng khi không kết nối được máy chủ
    // (hoặc API cũ chưa có endpoint) và được đánh dấu IsDemo để không gửi ID món mẫu lên server.
    public async Task<MenuData> GetMenuAsync(bool refresh = false)
    {
        if (menuCache is { IsDemo: false } && !refresh && DateTime.UtcNow - menuLoadedAt < TimeSpan.FromMinutes(1)) return menuCache;
        try
        {
            var menu = await api.Get<MenuResponse>("v1/me/menu");
            menuCache = new MenuData(menu.Branch, menu.Items, false, menu.Modifiers);
            menuLoadedAt = DateTime.UtcNow;
        }
        catch (ApiException e) when (e.IsMissingEndpoint || e.Status == 503)
        {
            if (menuCache is not { IsDemo: false }) menuCache = new MenuData(Store, DemoMenu(), true, []);
        }
        return menuCache;
    }

    // ----- Giỏ hàng -----
    // [BUG-02] Thêm tùy chọn món; dòng được nhận diện bằng CartLine.Key (món + tùy chọn).
    public void Add(MenuProduct item, int quantity = 1, IReadOnlyList<MenuModifier>? options = null)
    {
        var line = new CartLine(item, quantity, options ?? []);
        var index = Cart.FindIndex(x => x.Key == line.Key);
        if (index >= 0) Cart[index] = Cart[index] with { Quantity = Cart[index].Quantity + quantity };
        else Cart.Add(line);
        Changed?.Invoke();
    }

    public void ChangeQuantity(string key, int delta)
    {
        var index = Cart.FindIndex(x => x.Key == key);
        if (index < 0) return;
        var quantity = Cart[index].Quantity + delta;
        if (quantity <= 0) Cart.RemoveAt(index);
        else Cart[index] = Cart[index] with { Quantity = quantity };
        if (Cart.Count == 0) { VoucherId = null; VoucherDiscount = 0; }
        Changed?.Invoke();
    }

    // ----- Đơn hàng -----
    // [BUG-02] Đặt đơn thật qua POST v1/me/orders. Đơn trả trước ở trạng thái "Chờ thanh toán" 15 phút;
    // khách thanh toán (mô phỏng) ở trang chi tiết đơn. Không còn đơn mô phỏng trong bộ nhớ.
    public async Task<Order> PlaceOrderAsync(Checkout info, Guid requestKey)
    {
        if (Cart.Count == 0) throw new ApiException(400, "Giỏ hàng đang trống.");
        if (menuCache is { IsDemo: true }) throw new ApiException(503, "Chưa kết nối được máy chủ nên chưa đặt hàng được.");
        var order = await api.Send<Order>(HttpMethod.Post, "v1/me/orders", new
        {
            info.Fulfillment,
            info.PaymentMode,
            Items = Cart.Select(x => new { x.Item.VariantId, x.Quantity, x.OptionIds, Note = (string?)null }).ToArray(),
            RequestKey = requestKey,
            CustomerPromotionId = VoucherId,
            info.ContactName,
            info.ContactPhone,
            info.DeliveryAddress,
            info.DeliveryNote,
            info.Note
        });
        Cart.Clear();
        VoucherId = null;
        VoucherDiscount = 0;
        Changed?.Invoke();
        return order;
    }

    public async Task<List<Order>> GetOrdersAsync() => await api.All<Order>("v1/me/orders"); // [BUG-15] tải mọi trang, không dừng ở 50 đơn

    public async Task<Order?> GetOrderAsync(int id)
    {
        try { return await api.Get<Order>($"v1/me/orders/{id}"); }
        catch (ApiException e) when (e.Status == 404) { return null; }
    }

    /// <summary>[BUG-02] Cổng thanh toán mô phỏng: Succeeded hoặc Failed.</summary>
    public async Task<Order> PayAsync(int id, string outcome)
    {
        var order = await api.Send<Order>(HttpMethod.Post, $"v1/me/orders/{id}:pay", new { Outcome = outcome });
        Changed?.Invoke();
        return order;
    }

    /// <summary>[BUG-02] Khách hủy khi cửa hàng chưa xác nhận.</summary>
    public async Task<Order> CancelAsync(int id, string reason)
    {
        var order = await api.Send<Order>(HttpMethod.Post, $"v1/me/orders/{id}:cancel", new { Reason = reason });
        Changed?.Invoke();
        return order;
    }

    public void MarkFeedbackSent(int orderId) { } // [BUG-02] đơn thật: API tự biết đơn đã có phản hồi (FeedbackSent)

    // ===== Dữ liệu mẫu (chỉ khi không kết nối được máy chủ) =====
    private static List<MenuProduct> DemoMenu() => DemoItems.Select(x => x with { IsDemo = true }).ToList(); // [BUG-01]
    private static readonly List<MenuProduct> DemoItems =
    [
        // Trùng ProductID/VariantID với 06_seed.sql nên yêu thích và phản hồi lưu được vào database.
        new(1, "Cà phê sữa", "Cà phê phin và sữa đặc", 1, "Cà phê", 1, "M", 30000),
        new(1, "Cà phê sữa", "Cà phê phin và sữa đặc", 1, "Cà phê", 2, "L", 40000),
        new(4, "Cà phê đen đá", "Đậm vị robusta", 1, "Cà phê", 5, "M", 29000),
        new(5, "Bạc xỉu", "Nhiều sữa, ít cà phê", 1, "Cà phê", 6, "M", 32000),
        new(6, "Cold brew cam sả", "Ủ lạnh 16 giờ", 1, "Cà phê", 7, "M", 45000),
        new(2, "Trà đào", "Trà đào thơm mát", 2, "Trà", 3, "M", 35000),
        new(7, "Trà vải lài", "Trà lài và vải tươi", 2, "Trà", 8, "M", 39000),
        new(8, "Trà sen vàng", "Hạt sen, kem sữa", 2, "Trà", 9, "M", 45000, IsAvailable: false),
        new(3, "Bánh tiramisu", "Bánh vị cà phê", 3, "Bánh", 4, "Standard", 45000),
        new(9, "Bánh croissant bơ", "Nướng mỗi sáng", 3, "Bánh", 10, "Standard", 29000),
        new(10, "Bánh mousse chanh dây", "Chua ngọt nhẹ", 3, "Bánh", 11, "Standard", 39000, IsAvailable: false),
    ];
}
