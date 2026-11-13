using System.Globalization;
using System.Text.Json;
namespace MorningBrew.Admin.Services;

// [BUG-02] Nhãn tiếng Việt và cách đọc dữ liệu đơn hàng cho trang /Manager/Orders.
public static class OrderView
{
    public static string Status(string? s) => s switch
    {
        "AwaitingPayment" => "Chờ thanh toán", "Pending" => "Chờ xác nhận", "Confirmed" => "Đã xác nhận",
        "Preparing" => "Đang pha chế", "Ready" => "Sẵn sàng", "Delivering" => "Đang giao",
        "Completed" => "Hoàn tất", "Cancelled" => "Đã hủy", "Active" => "Cần xử lý", _ => s ?? "—"
    };
    public static string Css(string? s) => s switch { "Completed" => "ok", "Cancelled" => "bad", "AwaitingPayment" or "Pending" => "warn", _ => "info" };
    public static string Fulfillment(string? s) => s == "Delivery" ? "Giao hàng" : "Nhận tại quầy";
    public static string Payment(string? mode, string? status) =>
        (mode == "COD" ? "Tiền mặt" : "Trả trước") + " · " + (status switch { "Succeeded" => "đã thanh toán", "Failed" => "thất bại", "Pending" => "đang xử lý", _ => "chưa thanh toán" });
    public static string Refund(string? s) => s switch { "Pending" => "Chờ hoàn tiền", "Succeeded" => "Đã hoàn tiền", "Failed" => "Hoàn tiền thất bại", _ => "" };
    public static string Text(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? v.ToString() : "";
    public static string Money(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal().ToString("#,0", new CultureInfo("vi-VN")) + "đ" : "—";
    public static string Time(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) ? RowDisplay.Value("create_time", v) : "—";
    public static List<Dictionary<string, JsonElement>> List(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Array ? v.Deserialize<List<Dictionary<string, JsonElement>>>() ?? [] : [];
}
