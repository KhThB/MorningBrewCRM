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
    public decimal VoucherDiscount { get; set; }
    public event Action? Changed;

    private readonly List<Order> demoOrders = [];
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
    public void Add(MenuProduct item, int quantity = 1)
    {
        var index = Cart.FindIndex(x => x.Item.VariantId == item.VariantId);
        if (index >= 0) Cart[index] = Cart[index] with { Quantity = Cart[index].Quantity + quantity };
        else Cart.Add(new CartLine(item, quantity));
        Changed?.Invoke();
    }

    public void ChangeQuantity(int variantId, int delta)
    {
        var index = Cart.FindIndex(x => x.Item.VariantId == variantId);
        if (index < 0) return;
        var quantity = Cart[index].Quantity + delta;
        if (quantity <= 0) Cart.RemoveAt(index);
        else Cart[index] = Cart[index] with { Quantity = quantity };
        if (Cart.Count == 0) { VoucherId = null; VoucherDiscount = 0; }
        Changed?.Invoke();
    }

    // ----- Đơn hàng -----
    public async Task<Order> PlaceOrderAsync(Guid requestKey)
    {
        if (Cart.Count == 0) throw new ApiException(400, "Giỏ hàng đang trống.");
        Order order;
        try
        {
            order = await api.Send<Order>(HttpMethod.Post, "v1/me/orders", new
            {
                BranchId = CurrentBranch.BranchId,
                Fulfillment = "Pickup",
                Items = Cart.Select(x => new { VariantId = x.Item.VariantId, Quantity = x.Quantity }).ToArray(),
                CustomerPromotionId = VoucherId,
                RequestKey = requestKey
            });
            await api.Send(HttpMethod.Post, $"v1/me/orders/{order.OrderId}:pay", new { RequestKey = Guid.NewGuid() });
        }
        catch (ApiException e) when (e.IsMissingEndpoint)
        {
            order = CreateDemoOrder();
        }
        Cart.Clear();
        VoucherId = null;
        VoucherDiscount = 0;
        Changed?.Invoke();
        return order;
    }

    public async Task<List<Order>> GetOrdersAsync()
    {
        try
        {
            var real = (await api.Page<Order>("v1/me/orders")).Items;
            return real.Concat(demoOrders).OrderByDescending(x => x.CreatedAt).ToList();
        }
        catch (ApiException e) when (e.IsMissingEndpoint)
        {
            return demoOrders.OrderByDescending(x => x.CreatedAt).ToList();
        }
    }

    public async Task<Order?> GetOrderAsync(int id)
    {
        var demo = demoOrders.FirstOrDefault(x => x.OrderId == id);
        if (demo is not null) return demo;
        try { return await api.Get<Order>($"v1/me/orders/{id}"); }
        catch (ApiException e) when (e.IsMissingEndpoint) { return null; }
    }

    public void MarkFeedbackSent(int orderId)
    {
        var demo = demoOrders.FirstOrDefault(x => x.OrderId == orderId);
        if (demo is not null) demo.FeedbackSent = true;
    }

    // ===== Dữ liệu mẫu =====
    private Order CreateDemoOrder()
    {
        var order = new Order
        {
            OrderId = 900000 + demoOrders.Count + 1,
            OrderCode = $"MB-DEMO-{DateTime.Now:HHmmss}",
            BranchId = CurrentBranch.BranchId,
            BranchName = CurrentBranch.Name,
            Status = "Confirmed",
            Subtotal = CartSubtotal,
            Discount = Math.Min(VoucherDiscount, CartSubtotal),
            IsDemo = true,
            Lines = Cart.Select(x => new OrderLine(x.Item.ProductId, x.Item.VariantId, x.Item.Name,
                x.Item.VariantName, x.Quantity, x.Item.Price)).ToList()
        };
        demoOrders.Add(order);
        _ = SimulateProgress(order);
        return order;
    }

    // Mô phỏng quán xử lý đơn: Confirmed → Preparing → Ready → Completed (mỗi bước ~6 giây).
    private async Task SimulateProgress(Order order)
    {
        foreach (var next in new[] { "Preparing", "Ready", "Completed" })
        {
            await Task.Delay(TimeSpan.FromSeconds(6));
            order.Status = next;
            Changed?.Invoke();
        }
    }

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
