using System.Globalization;

namespace MauiApp1.Services;

/// <summary>Định dạng hiển thị và nhãn tiếng Việt.</summary>
public static class Fmt
{
    private static readonly CultureInfo Vi = new("vi-VN");

    public static string Money(decimal value) => value.ToString("#,0", Vi) + "đ";
    public static string Date(DateTimeOffset? value) => value?.ToLocalTime().ToString("dd/MM/yyyy") ?? "—";
    public static string When(DateTimeOffset? value) => value?.ToLocalTime().ToString("HH:mm · dd/MM/yyyy") ?? "—";

    public static string Discount(string type, decimal value, decimal? max = null) =>
        type == "Percent" ? $"Giảm {value:0}%{(max is > 0 ? $" (tối đa {Money(max.Value)})" : "")}" : $"Giảm {Money(value)}";

    public static string Gender(string? code) => code switch
    {
        "Male" => "Nam", "Female" => "Nữ", "Other" => "Khác", _ => "Không tiết lộ"
    };

    public static string FeedbackType(string code) => code switch
    {
        "Product" => "Sản phẩm", "Service" => "Phục vụ", "Space" => "Không gian", _ => "Khác"
    };

    public static (string Text, string Css) FeedbackStatus(string code) => code switch
    {
        "New" => ("Đã gửi", "chip-warn"),
        "Processing" => ("Đang xử lý", "chip-info"),
        "Resolved" => ("Đã phản hồi", "chip-ok"),
        _ => (code, "chip-muted")
    };

    public static (string Text, string Css) VoucherStatus(string code) => code switch
    {
        "Available" => ("Có thể dùng", "chip-ok"),
        "Reserved" => ("Đang giữ cho đơn", "chip-warn"),
        "Used" => ("Đã dùng", "chip-muted"),
        "Expired" => ("Hết hạn", "chip-bad"),
        _ => (code, "chip-muted")
    };

    public static string PointKind(string code) => code switch
    {
        "Earn" => "Tích điểm đơn hàng", "Redeem" => "Đổi ưu đãi", "Reverse" => "Hoàn tác điểm", "Adjust" => "Điều chỉnh", _ => code
    };

    public static readonly string[] OrderSteps = ["Pending", "Confirmed", "Preparing", "Ready", "Completed"];
    // [BUG-02] Đơn giao hàng có thêm bước "Đang giao".
    public static string[] StepsFor(string fulfillment) => fulfillment == "Delivery"
        ? ["Pending", "Confirmed", "Preparing", "Ready", "Delivering", "Completed"] : OrderSteps;
    public static string RefundStatus(string code) => code switch
    {
        "Pending" => "Đang hoàn tiền", "Succeeded" => "Đã hoàn tiền", "Failed" => "Hoàn tiền chưa thành công – cửa hàng sẽ xử lý lại", _ => code
    };

    public static string OrderStatus(string code) => code switch
    {
        "AwaitingPayment" => "Chờ thanh toán", "Pending" => "Chờ xác nhận", "Confirmed" => "Đã xác nhận",
        "Preparing" => "Đang pha chế", "Ready" => "Sẵn sàng nhận", "Delivering" => "Đang giao",
        "Completed" => "Hoàn tất", "Cancelled" => "Đã hủy", _ => code
    };

    public static string OrderCss(string code) => code switch
    {
        "Completed" => "chip-ok", "Cancelled" => "chip-bad", "Ready" => "chip-info", _ => "chip-warn"
    };

    /// <summary>Hạng thành viên chỉ để hiển thị theo mốc điểm — database không lưu hạng.</summary>
    public static (string Name, int Next) Tier(int points) => points switch
    {
        >= 1000 => ("Kim cương", 0),
        >= 500 => ("Vàng", 1000),
        >= 200 => ("Bạc", 500),
        _ => ("Thành viên", 200)
    };
}

/// <summary>Thông báo ngắn (toast) ở đáy màn hình.</summary>
public sealed class ToastService
{
    public string? Message { get; private set; }
    public bool IsError { get; private set; }
    public event Action? Changed;
    private CancellationTokenSource? cts;

    public void Show(string message, bool error = false)
    {
        Message = message;
        IsError = error;
        Changed?.Invoke();
        cts?.Cancel();
        cts = new CancellationTokenSource();
        var token = cts.Token;
        _ = Task.Delay(3200, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            Message = null;
            Changed?.Invoke();
        }, TaskScheduler.Default);
    }

    public void Error(Exception e) => Show(e is ApiException ? e.Message : "Có lỗi xảy ra, vui lòng thử lại.", true);
}
