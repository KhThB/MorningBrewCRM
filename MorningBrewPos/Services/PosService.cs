namespace MorningBrew.Pos.Services;

/// <summary>[BUG-02 · giai đoạn 2] Nghiệp vụ POS: thực đơn, giỏ tại quầy, tra cứu thành viên, tạo đơn, hàng chờ đơn.</summary>
public sealed class PosService(ApiClient api)
{
    public record Member(int CustomerId, string FullName, string? Phone, int LoyaltyPoints);

    public MenuResponse? Menu { get; private set; }
    public List<CartLine> Cart { get; } = [];
    public Member? Customer { get; set; }
    public decimal Subtotal => Cart.Sum(x => x.Total);
    public event Action? Changed;

    public async Task<MenuResponse> LoadMenuAsync() => Menu = await api.Get<MenuResponse>("v1/pos/menu");

    public void Add(MenuProduct item, int quantity, IReadOnlyList<MenuModifier> options)
    {
        var line = new CartLine(item, quantity, options);
        var index = Cart.FindIndex(x => x.Key == line.Key);
        if (index >= 0) Cart[index] = Cart[index] with { Quantity = Cart[index].Quantity + quantity }; else Cart.Add(line);
        Changed?.Invoke();
    }

    public void ChangeQuantity(string key, int delta)
    {
        var index = Cart.FindIndex(x => x.Key == key);
        if (index < 0) return;
        var q = Cart[index].Quantity + delta;
        if (q <= 0) Cart.RemoveAt(index); else Cart[index] = Cart[index] with { Quantity = q };
        Changed?.Invoke();
    }

    public void Clear() { Cart.Clear(); Customer = null; Changed?.Invoke(); }

    public async Task<Member?> LookupAsync(string phone)
        => (await api.Get<ApiPage<Member>>($"v1/pos/customers?phone={Uri.EscapeDataString(phone)}")).Items.FirstOrDefault();

    public async Task<Order> SellAsync(Guid requestKey, string? note, bool collectNow)
    {
        var order = await api.Send<Order>(HttpMethod.Post, "v1/pos/orders", new
        {
            Items = Cart.Select(x => new { x.Item.VariantId, x.Quantity, x.OptionIds, Note = (string?)null }).ToArray(),
            RequestKey = requestKey,
            CustomerId = Customer?.CustomerId,
            ContactName = (string?)null,
            Note = note,
            CollectNow = collectNow
        });
        Clear();
        return order;
    }

    public async Task<List<Order>> QueueAsync(string status) => await api.All<Order>($"v1/pos/orders?status={status}"); // [BUG-15]
    public Task<Order> GetAsync(int id) => api.Get<Order>($"v1/pos/orders/{id}");
    public Task<Order> AdvanceAsync(int id, string next, string? courierName = null, string? courierPhone = null)
        => api.Send<Order>(HttpMethod.Post, $"v1/pos/orders/{id}:advance", new { NextStatus = next, CourierName = courierName, CourierPhone = courierPhone });
    public Task<Order> CollectAsync(int id) => api.Send<Order>(HttpMethod.Post, $"v1/pos/orders/{id}:collect");
    public Task<Order> CancelAsync(int id, string reason) => api.Send<Order>(HttpMethod.Post, $"v1/pos/orders/{id}:cancel", new { Reason = reason });
}
