using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [BUG-02 · giai đoạn 2] API cho app POS của thu ngân (MorningBrewPos): bán tại quầy, tra cứu thành viên để tích điểm,
// hàng chờ đơn (gồm đơn từ app khách), chuyển trạng thái, xác nhận thu tiền mặt.
// Quyền theo vai trò trong stored procedure: Staff có Orders.Create/Manage/Read, Payments.Manage, Customers.Lookup;
// hủy đơn (Orders.Cancel) và hoàn tiền (Refunds.Manage) chỉ quản lý / chủ cửa hàng làm được.
[ApiController, Authorize(Roles = "Staff,BranchManager,Owner"), Route("v1/pos")]
public sealed class PosController(SqlStore db, PageReader pages) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<Dictionary<string, object?>> OrderResource(int id) => ApiContract.Resource(await OrderQueries.Read(db, id), "orders", "OrderID");
    private async Task RequireOrder(int id)
    {
        if ((await db.Query("SELECT 1 AS OK FROM dbo.Orders WHERE OrderID=@id AND dbo.CanBranch(@actor,BranchID,'Orders.Read')=1", ("@id", id), ("@actor", Actor))).Count == 0)
            throw new ApiException(404, "Không tìm thấy đơn hàng.");
    }

    [HttpGet("menu")]
    public Task<object> Menu() => MenuQueries.Read(db);

    // Tra cứu thành viên bằng số điện thoại chính xác (dbo.LookupCustomer, quyền Customers.Lookup).
    [HttpGet("customers")]
    public async Task<ApiPage> Lookup([Required] string phone)
        => new(await db.Procedure("dbo.LookupCustomer", ("@ActorID", Actor), ("@BranchID", await OrderQueries.StoreBranch(db)),
            ("@Phone", PhoneRule.Normalize(phone) ?? throw new ApiException(400, "Nhập số điện thoại khách."))), null);

    public record SaleInput([Required] OrderQueries.LineInput[] Items, Guid RequestKey, int? CustomerId = null,
        [MaxLength(120)] string? ContactName = null, [MaxLength(500)] string? Note = null, bool CollectNow = true);
    // Đơn tại quầy: nhận tại quầy, tiền mặt. CollectNow = thu tiền ngay khi tạo đơn.
    [HttpPost("orders")]
    public async Task<IActionResult> Sale(SaleInput input)
    {
        var id = await OrderQueries.Create(db, Actor, "POS", input.CustomerId, "Pickup", "COD", input.Items, input.RequestKey,
            null, input.ContactName, null, null, null, input.Note);
        if (input.CollectNow && (await db.Query("SELECT 1 AS OK FROM dbo.PaymentAttempts WHERE OrderID=@id AND Status='Succeeded'", ("@id", id))).Count == 0)
            await OrderQueries.Settle(db, Actor, id, "Succeeded");
        return Created($"/v1/pos/orders/{id}", await OrderResource(id));
    }

    [HttpGet("orders")]
    public async Task<ApiPage> Queue([FromQuery] PageRequest page, string? status = "Active")
    {
        var sql = OrderQueries.ListSql("""
            dbo.CanBranch(@actor,o.BranchID,'Orders.Read')=1
            AND (@status='' OR (@status='Active' AND o.Status NOT IN ('Completed','Cancelled')) OR o.Status=@status)
            """);
        return await pages.Read(sql, ["OrderID"], true, page, JsonSerializer.Serialize(new { Actor, section = "posOrders", status = status ?? "" }),
            [("@actor", Actor), ("@status", status ?? "")], "orders", "OrderID");
    }

    [HttpGet("orders/{id:int}")]
    public async Task<Dictionary<string, object?>> Get(int id) { await RequireOrder(id); return await OrderResource(id); }

    public record AdvanceInput([Required] string NextStatus, [MaxLength(120)] string? CourierName = null, [MaxLength(20)] string? CourierPhone = null);
    [HttpPost("orders/{id:int}:advance")]
    public async Task<Dictionary<string, object?>> Advance(int id, AdvanceInput input)
    {
        await RequireOrder(id);
        if (input.NextStatus is not ("Confirmed" or "Preparing" or "Ready" or "Delivering" or "Completed"))
            throw new ApiException(400, "Trạng thái tiếp theo không hợp lệ.");
        if (input.NextStatus == "Delivering")
        {
            if (string.IsNullOrWhiteSpace(input.CourierName))
                throw new ApiException(400, "Cần nhập tên người giao hàng.", [new("FIELD_REQUIRED", "courier_name")]);
            if (string.IsNullOrWhiteSpace(input.CourierPhone))
                throw new ApiException(400, "Cần nhập số điện thoại người giao hàng.", [new("FIELD_REQUIRED", "courier_phone")]);
        }
        await db.Procedure("dbo.AdvanceOrder", ("@ActorID", Actor), ("@OrderID", id), ("@NextStatus", input.NextStatus), ("@Reason", null),
            ("@CourierName", input.CourierName?.Trim()), ("@CourierPhone", PhoneRule.Normalize(input.CourierPhone, "courier_phone")));
        return await OrderResource(id);
    }

    [HttpPost("orders/{id:int}:collect")]
    public async Task<Dictionary<string, object?>> Collect(int id)
    {
        await RequireOrder(id);
        await OrderQueries.Settle(db, Actor, id, "Succeeded");
        return await OrderResource(id);
    }

    public record CancelInput([Required, StringLength(500, MinimumLength = 3)] string Reason);
    [HttpPost("orders/{id:int}:cancel")]
    public async Task<Dictionary<string, object?>> Cancel(int id, CancelInput input)
    {
        await RequireOrder(id);
        await db.Procedure("dbo.CancelOrder", ("@ActorID", Actor), ("@OrderID", id), ("@Reason", input.Reason.Trim()));
        return await OrderResource(id);
    }
}
