using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace MorningBrew.Api.Infrastructure;

// [BUG-02] Đơn hàng thật (trước đây app chỉ mô phỏng, không lưu database). Dùng chung cho app khách (/v1/me/orders),
// trang Đơn hàng của quản lý (/v1/orders) và app POS. Mọi kiểm tra nghiệp vụ nằm trong stored procedure
// của 01_core.sql (CreateOrder, StartPayment, SimulatePayment, AdvanceOrder, CancelOrder, RequestRefund…).
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

    // [BUG-13] bảng dịch chuyển sang SqlMessages (dùng chung mọi thông báo SQL).
    public static string Translate(string message, string? fallback = null) => SqlMessages.Translate(message, fallback);
}
