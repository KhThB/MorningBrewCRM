using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [NOTI] Trang Thông báo của app khách. Bảng dbo.Notifications đã được các thủ tục SQL ghi sẵn khi:
// đơn đổi trạng thái / bị hủy / hoàn tiền (03_orders), cấp voucher (03_orders), mời khảo sát và trả lời phản hồi (04_crm, 12_bugfix).
// API này chỉ đọc và đánh dấu đã đọc – không đổi nghiệp vụ nào khác.
[ApiController, Authorize(Roles = "Customer"), Route("v1/me")]
public sealed class CustomerNotificationsController(SqlStore db) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Nội dung "Trạng thái mới: Confirmed" do thủ tục AdvanceOrder ghi bằng mã trạng thái → đổi sang tiếng Việt khi trả về.
    private static readonly Dictionary<string, string> OrderStatus = new()
    {
        ["AwaitingPayment"] = "Chờ thanh toán", ["Pending"] = "Chờ xác nhận", ["Confirmed"] = "Đã xác nhận",
        ["Preparing"] = "Đang pha chế", ["Ready"] = "Sẵn sàng nhận", ["Delivering"] = "Đang giao",
        ["Completed"] = "Hoàn tất", ["Cancelled"] = "Đã hủy"
    };

    /// <summary>100 thông báo mới nhất + số chưa đọc. Đơn hàng kèm mã đơn ngắn để hiển thị.</summary>
    [HttpGet("notifications")]
    public async Task<object> List()
    {
        var items = await db.Query("""
            SELECT TOP(100) n.NotificationID,n.Title,n.Body,n.OrderID,o.OrderCode,n.CustomerPromotionID,n.SurveyID,n.CreatedAt,n.ReadAt
            FROM dbo.Notifications n LEFT JOIN dbo.Orders o ON o.OrderID=n.OrderID
            WHERE n.UserID=@actor ORDER BY n.NotificationID DESC
            """, ("@actor", Actor));
        foreach (var row in items)
            if (row["Body"] is string body && body.StartsWith("Trạng thái mới: ") && OrderStatus.TryGetValue(body["Trạng thái mới: ".Length..], out var vi))
                row["Body"] = "Trạng thái mới: " + vi;
        return new { items, unread = await Unread() };
    }

    /// <summary>Số thông báo chưa đọc – cho chấm đỏ trên nút chuông ở trang chủ.</summary>
    [HttpGet("notifications/unread-count")]
    public async Task<object> UnreadCount() => new { unread = await Unread() };

    [HttpPost("notifications/{id:int}:read")]
    public async Task<object> Read(int id)
    {
        await db.Query("UPDATE dbo.Notifications SET ReadAt=SYSUTCDATETIME() WHERE NotificationID=@id AND UserID=@actor AND ReadAt IS NULL",
            ("@id", id), ("@actor", Actor));
        return new { unread = await Unread() };
    }

    [HttpPost("notifications:readAll")]
    public async Task<object> ReadAll()
    {
        await db.Query("UPDATE dbo.Notifications SET ReadAt=SYSUTCDATETIME() WHERE UserID=@actor AND ReadAt IS NULL", ("@actor", Actor));
        return new { unread = 0 };
    }

    private async Task<int> Unread() =>
        (int)(await db.Query("SELECT COUNT(*) AS N FROM dbo.Notifications WHERE UserID=@actor AND ReadAt IS NULL", ("@actor", Actor)))[0]["N"]!;
}
