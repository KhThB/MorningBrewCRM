namespace MauiApp1.Services;

/// <summary>Thực đơn, giỏ hàng và đơn hàng của khách (gọi MorningBrew.Api: /v1/me/menu, /v1/me/orders).</summary>
// [CLEAN-02] Bỏ thực đơn mẫu (DemoMenu) khi mất kết nối: lỗi kết nối nay hiện ở trang Thực đơn kèm nút "Thử lại".
public sealed class ShopService(ApiClient api)
{
    // Hệ thống chỉ có 1 cửa hàng (06_seed.sql, mã MB01); dùng để hiện tên cửa hàng trước khi tải xong thực đơn.
    public static readonly Branch Store = new(1, "Morning Brew Thủ Đức", "12 Võ Văn Ngân, TP.HCM");

    public Branch CurrentBranch => menuCache?.Branch ?? Store; // [BUG-01] cửa hàng lấy từ API, Store chỉ để dự phòng
    public List<CartLine> Cart { get; } = [];
    // [APP-01] Giữ cả voucher đã chọn (không chỉ số tiền giảm lúc chọn) để tính lại khi đổi số lượng món.
    public Voucher? SelectedVoucher { get; set; }
    public int? VoucherId => SelectedVoucher?.CustomerPromotionId;
    /// <summary>[BUG-17] Hình thức nhận khách đã chọn (ô "Nhận tại quán" / "Giao hàng" ở trang chủ hoặc trong giỏ).
    /// Giỏ hàng mở ra với lựa chọn này thay vì luôn là "Nhận tại quầy".</summary>
    public string? PreferredFulfillment { get; set; } // [UI2-A2b] null = chưa chọn → Thực đơn hỏi khách khi mở

    /// <summary>[BUG-16] Xóa dữ liệu của phiên trước khi đăng xuất / phiên bị thu hồi: giỏ hàng, voucher đã chọn,
    /// hình thức nhận. Trước đây ShopService là singleton nên giỏ của tài khoản cũ còn nguyên khi đăng nhập tài khoản khác.</summary>
    public void ResetForSignOut()
    {
        Cart.Clear();
        SelectedVoucher = null; // [APP-01]
        PreferredFulfillment = null; // [UI2-A2b]
        Changed?.Invoke();
    }
    // [APP-01] Tính lại mỗi lần đọc theo tạm tính hiện tại (trước đây lưu cố định lúc chọn voucher).
    public decimal VoucherDiscount => SelectedVoucher?.DiscountFor(CartSubtotal) ?? 0;
    // [APP-01] Đã chọn voucher nhưng giỏ chưa đủ giá trị tối thiểu → máy chủ sẽ từ chối đơn.
    public bool VoucherBelowMinimum => SelectedVoucher is { } v && CartSubtotal < v.MinSubtotal;
    public event Action? Changed;

    private MenuResponse? menuCache; // [CLEAN-02] dùng thẳng kết quả API, bỏ MenuData (chỉ thêm cờ IsDemo)
    private DateTime menuLoadedAt; // [BUG-01] tự tải lại sau 1 phút để thấy giá / món mới từ Admin

    public int CartCount => Cart.Sum(x => x.Quantity);
    public decimal CartSubtotal => Cart.Sum(x => x.Total);

    // ----- Thực đơn -----
    // [BUG-01] Đọc thực đơn thật từ GET v1/me/menu, tự tải lại sau 1 phút để thấy giá / món mới từ Admin.
    public async Task<MenuResponse> GetMenuAsync(bool refresh = false)
    {
        if (menuCache is not null && !refresh && DateTime.UtcNow - menuLoadedAt < TimeSpan.FromMinutes(1)) return menuCache;
        menuCache = await api.Get<MenuResponse>("v1/me/menu");
        menuLoadedAt = DateTime.UtcNow;
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
        if (Cart.Count == 0) SelectedVoucher = null; // [APP-01]
        Changed?.Invoke();
    }

    // ----- Đơn hàng -----
    // [BUG-02] Đặt đơn thật qua POST v1/me/orders. Đơn trả trước ở trạng thái "Chờ thanh toán" 15 phút;
    // khách thanh toán (mô phỏng) ở trang chi tiết đơn. Không còn đơn mô phỏng trong bộ nhớ.
    public async Task<Order> PlaceOrderAsync(Checkout info, Guid requestKey)
    {
        if (Cart.Count == 0) throw new ApiException(400, "Giỏ hàng đang trống.");
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
        SelectedVoucher = null; // [APP-01]
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
}
