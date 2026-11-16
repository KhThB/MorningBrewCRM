using System.Globalization;

namespace MorningBrew.Pos.Services;

/// <summary>Định dạng hiển thị và nhãn tiếng Việt.</summary>
public static class Fmt
{
    private static readonly CultureInfo Vi = new("vi-VN");

    public static string Money(decimal value) => value.ToString("#,0", Vi) + "đ";
    public static string When(DateTimeOffset? value) => value?.ToLocalTime().ToString("HH:mm · dd/MM/yyyy") ?? "—";

    // [CLEAN-04] Bỏ các hàm định dạng chỉ app khách dùng (giảm giá, giới tính, phản hồi, voucher, điểm, các bước đơn, hạng, hoàn tiền).
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
