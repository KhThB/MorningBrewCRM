using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [BUG-02] Trang Đơn hàng của quản lý (/Manager/Orders): xem, xử lý theo trạng thái, xác nhận thu tiền mặt,
// giao hàng, hủy, hoàn tiền. Quyền kiểm tra trong stored procedure (Orders.Read/Manage/Cancel, Payments.Manage, Refunds.Manage).
public sealed partial class AdminController
{
    private static readonly string[] OrderStatuses = ["AwaitingPayment", "Pending", "Confirmed", "Preparing", "Ready", "Delivering", "Completed", "Cancelled"];

    [HttpGet("orders")]
    public async Task<ApiPage> ListOrders([FromQuery] PageRequest page, string? status = null, string? q = null)
    {
        if (!string.IsNullOrEmpty(status) && status != "Active" && !OrderStatuses.Contains(status))
            throw new ApiException(400, "Trạng thái đơn không hợp lệ.");
        // "Active" = các đơn còn cần xử lý (chưa hoàn tất, chưa hủy).
        var sql = OrderQueries.ListSql("""
            dbo.CanBranch(@actor,o.BranchID,'Orders.Read')=1
            AND (@status='' OR (@status='Active' AND o.Status NOT IN ('Completed','Cancelled')) OR o.Status=@status)
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

    public record AdvanceInput([Required] string NextStatus, [MaxLength(500)] string? Reason = null,
        [MaxLength(120)] string? CourierName = null, [MaxLength(20)] string? CourierPhone = null);
    [HttpPost("orders/{id:int}:advance")]
    public async Task<Dictionary<string, object?>> Advance(int id, AdvanceInput input)
    {
        await RequireOrder(id);
        if (!OrderStatuses.Contains(input.NextStatus) || input.NextStatus is "AwaitingPayment" or "Pending" or "Cancelled")
            throw new ApiException(400, "Trạng thái tiếp theo không hợp lệ.");
        if (input.NextStatus == "Delivering")
        {
            if (string.IsNullOrWhiteSpace(input.CourierName))
                throw new ApiException(400, "Cần nhập tên người giao hàng.", [new("FIELD_REQUIRED", "courier_name")]);
            if (string.IsNullOrWhiteSpace(input.CourierPhone))
                throw new ApiException(400, "Cần nhập số điện thoại người giao hàng.", [new("FIELD_REQUIRED", "courier_phone")]);
        }
        await db.Procedure("dbo.AdvanceOrder", ("@ActorID", Actor), ("@OrderID", id), ("@NextStatus", input.NextStatus), ("@Reason", input.Reason),
            ("@CourierName", input.CourierName?.Trim()), ("@CourierPhone", PhoneRule.Normalize(input.CourierPhone, "courier_phone")));
        return await OrderResource(id);
    }

    // Xác nhận đã thu tiền mặt (đơn COD) để có thể hoàn tất đơn.
    [HttpPost("orders/{id:int}:collect")]
    public async Task<Dictionary<string, object?>> Collect(int id)
    {
        await RequireOrder(id);
        await OrderQueries.Settle(db, Actor, id, "Succeeded");
        return await OrderResource(id);
    }

    public record ReasonInput([Required, StringLength(500, MinimumLength = 3)] string Reason);
    [HttpPost("orders/{id:int}:cancel")]
    public async Task<Dictionary<string, object?>> CancelOrder(int id, ReasonInput input)
    {
        await RequireOrder(id);
        await db.Procedure("dbo.CancelOrder", ("@ActorID", Actor), ("@OrderID", id), ("@Reason", input.Reason.Trim()));
        return await OrderResource(id);
    }

    public record DeliveryInput([Required] string Status, [MaxLength(500)] string? Reason = null);
    [HttpPost("orders/{id:int}:delivery")]
    public async Task<Dictionary<string, object?>> Delivery(int id, DeliveryInput input)
    {
        await RequireOrder(id);
        await db.Procedure("dbo.UpdateDelivery", ("@ActorID", Actor), ("@OrderID", id), ("@Status", input.Status), ("@Reason", input.Reason));
        return await OrderResource(id);
    }

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
