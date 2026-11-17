using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [BUG-02] Trang Đơn hàng của quản lý (/Manager/Orders). [ORD-01][REV-03] chỉ còn xem đơn và hoàn tiền;
// quyền kiểm tra trong stored procedure (Orders.Read, Refunds.Manage).
public sealed partial class AdminController
{
    private static readonly string[] OrderStatuses = ["AwaitingPayment", "Pending", "Confirmed", "Preparing", "Ready", "Delivering", "Completed", "Cancelled"];

    [HttpGet("orders")]
    public async Task<ApiPage> ListOrders([FromQuery] PageRequest page, string? status = null, string? q = null)
    {
        if (!string.IsNullOrEmpty(status) && status is not ("Active" or "RefundPending") && !OrderStatuses.Contains(status))
            throw new ApiException(400, "Trạng thái đơn không hợp lệ.");
        // "Active" = các đơn còn cần xử lý (chưa hoàn tất, chưa hủy).
        // [ORD-01] "RefundPending" = đơn có yêu cầu hoàn tiền chưa thành công (chờ hoặc đã thất bại, cần xử lý lại).
        var sql = OrderQueries.ListSql("""
            dbo.CanBranch(@actor,o.BranchID,'Orders.Read')=1
            AND (@status='' OR (@status='Active' AND o.Status NOT IN ('Completed','Cancelled')) OR o.Status=@status
                 OR (@status='RefundPending' AND EXISTS(SELECT 1 FROM dbo.Refunds r WHERE r.OrderID=o.OrderID AND r.Status<>'Succeeded')))
            AND (@q='' OR o.OrderCode LIKE @search OR o.ContactName LIKE @search OR o.ContactPhone LIKE @search OR cu.FullName LIKE @search)
            """);
        return await pages.Read(sql, ["OrderID"], true, page,
            JsonSerializer.Serialize(new { Actor, section = "orders", status = status ?? "", q = q ?? "" }),
            [("@actor", Actor), ("@status", status ?? ""), ("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%")], "orders", "OrderID");
    }

    private async Task RequireOrder(int id, string permission = "Orders.Read")
    {
        var ok = await db.Query("SELECT 1 AS OK FROM dbo.Orders WHERE OrderID=@id AND dbo.CanBranch(@actor,BranchID,@permission)=1",
            ("@id", id), ("@actor", Actor), ("@permission", permission));
        if (ok.Count == 0) throw new ApiException(404, "Không tìm thấy đơn hàng trong phạm vi của bạn.");
    }
    private async Task<Dictionary<string, object?>> OrderResource(int id) => ApiContract.Resource(await OrderQueries.Read(db, id), "orders", "OrderID");

    [HttpGet("orders/{id:int}")]
    public async Task<Dictionary<string, object?>> GetOrder(int id) { await RequireOrder(id); return await OrderResource(id); }

    // [REV-03] Bỏ :advance, :collect, :cancel, :delivery của quản lý – vận hành đơn làm trên POS (v1/pos/orders/...).
    public record ReasonInput([Required, StringLength(500, MinimumLength = 3)] string Reason);
    [HttpPost("orders/{id:int}:refund")]
    public async Task<Dictionary<string, object?>> Refund(int id, ReasonInput input)
    {
        await RequireOrder(id);
        await db.Procedure("dbo.RequestRefund", ("@ActorID", Actor), ("@OrderID", id), ("@Reason", input.Reason.Trim()), ("@RequestKey", Guid.NewGuid()));
        return await OrderResource(id);
    }

    // Chốt kết quả hoàn tiền (mô phỏng cổng thanh toán): thành công thì thu hồi điểm và trả lại voucher.
    public record RefundResultInput([Required] string Outcome, [MaxLength(500)] string? FailureReason = null);
    [HttpPost("refunds/{id:int}:complete")]
    public async Task<IActionResult> CompleteRefund(int id, RefundResultInput input)
    {
        await db.Procedure("dbo.SimulateRefund", ("@ActorID", Actor), ("@RefundID", id), ("@Outcome", input.Outcome), ("@FailureReason", input.FailureReason));
        return NoContent();
    }
}
