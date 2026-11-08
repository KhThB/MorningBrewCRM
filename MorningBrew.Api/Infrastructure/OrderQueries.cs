using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace MorningBrew.Api.Infrastructure;

// [BUG-02] Đơn hàng thật (trước đây app chỉ mô phỏng, không lưu database). Dùng chung cho app khách (/v1/me/orders),
// trang Đơn hàng của quản lý (/v1/orders) và app POS. Mọi kiểm tra nghiệp vụ nằm trong stored procedure
// của 03_orders.sql (CreateOrder, StartPayment, SimulatePayment, AdvanceOrder, CancelOrder, RequestRefund…).
public static class OrderQueries
{
    public static readonly string[] Fulfillments = ["Pickup", "Delivery"];
    public static readonly string[] PaymentModes = ["Prepaid", "COD"];

    public record LineInput([Range(1, int.MaxValue)] int VariantId, [Range(1, 100)] int Quantity, int[]? OptionIds = null, [MaxLength(300)] string? Note = null);

    public static async Task<int> StoreBranch(SqlStore db)
        => (int)((await db.Query("SELECT TOP(1) BranchID FROM dbo.Branches WHERE IsActive=1 ORDER BY BranchID")).SingleOrDefault()?["BranchID"]
            ?? throw new ApiException(404, "Chưa có cửa hàng đang hoạt động."));

    public static async Task<int> Create(SqlStore db, int actor, string source, int? customerId, string fulfillment, string paymentMode,
        LineInput[] items, Guid requestKey, int? voucherId, string? contactName, string? contactPhone, string? address, string? deliveryNote, string? note)
    {
        if (!Fulfillments.Contains(fulfillment)) throw new ApiException(400, "Hình thức nhận hàng không hợp lệ.", [new("FIELD_INVALID", "fulfillment")]);
        if (!PaymentModes.Contains(paymentMode)) throw new ApiException(400, "Hình thức thanh toán không hợp lệ.", [new("FIELD_INVALID", "payment_mode")]);
        if (items is not { Length: > 0 and <= 100 }) throw new ApiException(400, "Đơn cần từ 1 đến 100 món.", [new("FIELD_INVALID", "items")]);
        if (requestKey == Guid.Empty) throw new ApiException(400, "RequestKey không được rỗng.", [new("FIELD_INVALID", "request_key")]);
        var phone = PhoneRule.Normalize(contactPhone, "contact_phone");
        if (fulfillment == "Delivery" && (string.IsNullOrWhiteSpace(address) || (phone is null && customerId is null)))
            throw new ApiException(400, "Giao hàng cần địa chỉ và số điện thoại người nhận.", [new("FIELD_INVALID", "delivery_address")]);
        var json = JsonSerializer.Serialize(items.Select(i => new { variantId = i.VariantId, quantity = i.Quantity, note = i.Note, optionIds = i.OptionIds ?? [] }));
        var rows = await db.Procedure("dbo.CreateOrder", ("@ActorID", actor), ("@BranchID", await StoreBranch(db)), ("@Source", source),
            ("@Fulfillment", fulfillment), ("@PaymentMode", paymentMode), ("@Items", json), ("@RequestKey", requestKey),
            ("@CustomerID", customerId), ("@CustomerPromotionID", voucherId), ("@ContactName", string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim()),
            ("@ContactPhone", phone), ("@DeliveryAddress", string.IsNullOrWhiteSpace(address) ? null : address.Trim()),
            ("@DeliveryNote", deliveryNote), ("@Note", note));
        return (int)rows.Single()["OrderID"]!;
    }

    // Thu tiền: tạo lượt thanh toán rồi chốt kết quả (Succeeded/Failed). Tiền mặt (COD) do quầy xác nhận;
    // trả trước do khách xác nhận qua cổng thanh toán mô phỏng.
    public static async Task Settle(SqlStore db, int actor, int orderId, string outcome, string? reason = null)
    {
        if (outcome is not ("Succeeded" or "Failed")) throw new ApiException(400, "Kết quả thanh toán không hợp lệ.");
        var payment = (await db.Procedure("dbo.StartPayment", ("@ActorID", actor), ("@OrderID", orderId), ("@RequestKey", Guid.NewGuid()))).Single()["PaymentID"];
        await db.Procedure("dbo.SimulatePayment", ("@ActorID", actor), ("@PaymentID", payment), ("@Outcome", outcome), ("@Reason", reason));
    }

    private const string Summary = """
        SELECT o.OrderID,o.OrderCode,o.BranchID,b.Name AS BranchName,o.CustomerID,COALESCE(cu.FullName,o.ContactName) AS CustomerName,
        o.Status,o.Source,o.Fulfillment,o.PaymentMode,o.Subtotal,o.DiscountAmount AS Discount,o.DeliveryFee,o.TotalAmount,
        o.ContactName,o.ContactPhone,o.Note,o.CreatedAt,o.PaymentDueAt,
        (SELECT TOP(1) p.Status FROM dbo.PaymentAttempts p WHERE p.OrderID=o.OrderID ORDER BY p.PaymentID DESC) AS PaymentStatus,
        (SELECT TOP(1) r.Status FROM dbo.Refunds r WHERE r.OrderID=o.OrderID ORDER BY r.RefundID DESC) AS RefundStatus,
        (SELECT STRING_AGG(CONCAT(d.Quantity,N'× ',d.ProductName),N', ') FROM dbo.OrderDetails d WHERE d.OrderID=o.OrderID) AS ItemsText,
        CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Feedbacks f WHERE f.OrderID=o.OrderID) THEN 1 ELSE 0 END AS bit) AS FeedbackSent
        FROM dbo.Orders o JOIN dbo.Branches b ON b.BranchID=o.BranchID
        LEFT JOIN dbo.Customers c ON c.CustomerID=o.CustomerID LEFT JOIN dbo.Users cu ON cu.UserID=c.UserID
        """;
    // Danh sách: điều kiện phạm vi do nơi gọi truyền vào (khách: đơn của mình; quản lý/POS: quyền Orders.Read).
    public static string ListSql(string where) => $"{Summary} WHERE {where}";

    public static async Task<Dictionary<string, object?>> Read(SqlStore db, int orderId)
    {
        var order = (await db.Query($"""
            {Summary.Replace("FROM dbo.Orders o", ",dl.AddressText AS DeliveryAddress,dl.Note AS DeliveryNote,dl.Status AS DeliveryStatus,dl.CourierName,dl.CourierPhone,dl.FailureReason AS DeliveryFailure FROM dbo.Orders o")}
            LEFT JOIN dbo.Deliveries dl ON dl.OrderID=o.OrderID WHERE o.OrderID=@id
            """, ("@id", orderId))).SingleOrDefault() ?? throw new ApiException(404, "Không tìm thấy đơn hàng.");
        order["Lines"] = await db.Query("""
            SELECT d.OrderDetailID,d.ProductID,d.VariantID,d.ProductName AS Name,d.VariantName,d.Quantity,d.UnitPrice,d.ModifierTotal,d.LineTotal,d.Note,
            (SELECT STRING_AGG(m.OptionName,N', ') FROM dbo.OrderItemModifiers m WHERE m.OrderDetailID=d.OrderDetailID) AS Options
            FROM dbo.OrderDetails d WHERE d.OrderID=@id ORDER BY d.OrderDetailID
            """, ("@id", orderId));
        order["History"] = await db.Query("""
            SELECT h.FromStatus,h.ToStatus,h.Reason,h.CreatedAt,COALESCE(u.FullName,N'Hệ thống') AS ChangedByName
            FROM dbo.OrderStatusHistory h LEFT JOIN dbo.Users u ON u.UserID=h.ChangedBy WHERE h.OrderID=@id ORDER BY h.HistoryID
            """, ("@id", orderId));
        order["Payments"] = await db.Query("SELECT PaymentID,Method,Amount,Status,CreatedAt,SettledAt,FailureReason FROM dbo.PaymentAttempts WHERE OrderID=@id ORDER BY PaymentID", ("@id", orderId));
        order["Refunds"] = await db.Query("SELECT RefundID,Amount,Status,Reason,FailureReason,CreatedAt,CompletedAt FROM dbo.Refunds WHERE OrderID=@id ORDER BY RefundID", ("@id", orderId));
        return order;
    }

    // Thông báo tiếng Anh từ stored procedure đơn hàng → tiếng Việt cho app và Admin.
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Branch is closed or does not accept this fulfillment type."] = "Cửa hàng đang đóng cửa hoặc tạm ngưng hình thức nhận hàng này.",
        ["Delivery needs recipient phone and address."] = "Giao hàng cần số điện thoại và địa chỉ người nhận.",
        ["Invalid order lines."] = "Danh sách món không hợp lệ.",
        ["Invalid order mode."] = "Hình thức nhận hàng hoặc thanh toán không hợp lệ.",
        ["An item or size is unavailable at this branch."] = "Có món hoặc kích cỡ vừa hết, vui lòng tải lại thực đơn.",
        ["An option is unavailable or does not belong to this product."] = "Tùy chọn đã hết hoặc không thuộc món này.",
        ["Option selection count is invalid."] = "Vui lòng chọn đủ các tùy chọn bắt buộc của món (đường, đá…).",
        ["Voucher is invalid, reserved, expired or minimum is not met."] = "Voucher không dùng được: đang giữ cho đơn khác, đã hết hạn hoặc đơn chưa đạt giá trị tối thiểu.",
        ["No active loyalty policy."] = "Chưa có chính sách tích điểm đang áp dụng.",
        ["Customer is inactive or missing."] = "Tài khoản khách đã bị khóa hoặc không tồn tại.",
        ["Order is not payable."] = "Đơn không ở trạng thái chờ thanh toán hoặc đã quá hạn thanh toán.",
        ["Payment already pending or succeeded."] = "Đơn đang được thanh toán hoặc đã thanh toán.",
        ["Order is cancelled or payment timed out; simulator cannot collect funds."] = "Đơn đã hủy hoặc đã quá hạn thanh toán.",
        ["Terminal payment result cannot change."] = "Kết quả thanh toán đã chốt, không đổi được.",
        ["Invalid order transition."] = "Không chuyển được sang trạng thái này (sai thứ tự xử lý đơn).",
        ["Order must be fully paid and not under refund."] = "Đơn phải được thanh toán (xác nhận đã thu tiền) và không đang hoàn tiền mới hoàn tất được.",
        ["Delivery has not resumed after failure."] = "Lần giao trước thất bại: cần giao lại trước khi hoàn tất.",
        ["Courier name and phone are required."] = "Cần nhập tên và số điện thoại người giao hàng.",
        ["Order is not in delivery phase."] = "Đơn chưa ở bước đang giao.",
        ["Invalid delivery transition."] = "Không chuyển được trạng thái giao hàng này.",
        ["Failure reason is required."] = "Cần nhập lý do giao thất bại.",
        ["Use AdvanceOrder for handover and completion."] = "Dùng nút chuyển trạng thái đơn để giao và hoàn tất.",
        ["Completed orders use RequestRefund; operational history remains completed."] = "Đơn đã hoàn tất: dùng chức năng hoàn tiền thay vì hủy.",
        ["Cancellation reason is required."] = "Cần nhập lý do hủy đơn.",
        ["Cancel the active order before refunding."] = "Chỉ hoàn tiền cho đơn đã hoàn tất hoặc đã hủy.",
        ["Refund reason is required."] = "Cần nhập lý do hoàn tiền.",
        ["No successful payment to refund."] = "Đơn chưa có khoản thanh toán thành công để hoàn.",
        ["Successful refund cannot change."] = "Hoàn tiền đã thành công, không đổi được.",
        ["Invalid refund result."] = "Kết quả hoàn tiền không hợp lệ.",
        ["Order request is busy; retry."] = "Đơn đang được xử lý, vui lòng thử lại sau giây lát.",
        ["Order not found."] = "Không tìm thấy đơn hàng.",
        ["Payment not found."] = "Không tìm thấy lượt thanh toán.",
    };
    public static string Translate(string message, string? fallback = null)
        => Messages.TryGetValue(message, out var vi) ? vi : fallback ?? message;
}
