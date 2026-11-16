using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [BUG-02] Đơn hàng của khách trong app: đặt (nhận tại quầy / giao hàng), thanh toán trả trước mô phỏng,
// xem lịch sử, hủy khi cửa hàng chưa xác nhận. Tiền mặt (COD) do cửa hàng xác nhận ở trang Đơn hàng / POS.
[ApiController, Authorize(Roles = "Customer"), Route("v1/me/orders")]
public sealed class CustomerOrdersController(SqlStore db, PageReader pages) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<int> CustomerId()
        => (int)((await db.Query("SELECT CustomerID FROM dbo.Customers WHERE UserID=@actor", ("@actor", Actor))).SingleOrDefault()?["CustomerID"]
            ?? throw new ApiException(404, "Chưa có hồ sơ khách hàng."));
    // Đơn phải thuộc khách đang đăng nhập.
    private async Task<int> Own(int id)
    {
        var customer = await CustomerId();
        if ((await db.Query("SELECT 1 AS OK FROM dbo.Orders WHERE OrderID=@id AND CustomerID=@customer", ("@id", id), ("@customer", customer))).Count == 0)
            throw new ApiException(404, "Không tìm thấy đơn hàng.");
        return customer;
    }

    [HttpGet]
    public async Task<ApiPage> List([FromQuery] PageRequest page)
        => await pages.Read(OrderQueries.ListSql("o.CustomerID=@id"), ["OrderID"], true, page,
            JsonSerializer.Serialize(new { Actor, section = "myOrders" }), [("@id", await CustomerId())], "orders", "OrderID");

    [HttpGet("{id:int}")]
    public async Task<Dictionary<string, object?>> Get(int id)
    {
        await Own(id);
        return OrderQueries_Resource(await OrderQueries.Read(db, id));
    }
    private static Dictionary<string, object?> OrderQueries_Resource(Dictionary<string, object?> row) => ApiContract.Resource(row, "orders", "OrderID");

    public record CreateInput([Required] string Fulfillment, [Required] string PaymentMode, [Required] OrderQueries.LineInput[] Items, Guid RequestKey,
        int? CustomerPromotionId = null, [MaxLength(120)] string? ContactName = null, [MaxLength(20)] string? ContactPhone = null,
        [MaxLength(500)] string? DeliveryAddress = null, [MaxLength(300)] string? DeliveryNote = null, [MaxLength(500)] string? Note = null);
    [HttpPost]
    public async Task<IActionResult> Create(CreateInput input)
    {
        var id = await OrderQueries.Create(db, Actor, "App", await CustomerId(), input.Fulfillment, input.PaymentMode, input.Items, input.RequestKey,
            input.CustomerPromotionId, input.ContactName, input.ContactPhone, input.DeliveryAddress, input.DeliveryNote, input.Note);
        return Created($"/v1/me/orders/{id}", OrderQueries_Resource(await OrderQueries.Read(db, id)));
    }

    // Cổng thanh toán mô phỏng cho đơn trả trước: khách chọn thành công hoặc thất bại (thất bại thì thanh toán lại được).
    public record PayInput([Required] string Outcome);
    [HttpPost("{id:int}:pay")]
    public async Task<Dictionary<string, object?>> Pay(int id, PayInput input)
    {
        await Own(id);
        var mode = (string)(await db.Query("SELECT PaymentMode FROM dbo.Orders WHERE OrderID=@id", ("@id", id))).Single()["PaymentMode"]!;
        if (mode != "Prepaid") throw new ApiException(409, "Đơn thanh toán tiền mặt khi nhận hàng, không thanh toán trong app.");
        await OrderQueries.Settle(db, Actor, id, input.Outcome, input.Outcome == "Failed" ? "Khách hủy thanh toán / thẻ bị từ chối (mô phỏng)" : null);
        return OrderQueries_Resource(await OrderQueries.Read(db, id));
    }

    public record CancelInput([Required, StringLength(500, MinimumLength = 3)] string Reason);
    [HttpPost("{id:int}:cancel")]
    public async Task<Dictionary<string, object?>> Cancel(int id, CancelInput input)
    {
        await Own(id);
        var status = (string)(await db.Query("SELECT Status FROM dbo.Orders WHERE OrderID=@id", ("@id", id))).Single()["Status"]!;
        if (status is not ("AwaitingPayment" or "Pending" or "Cancelled"))
            throw new ApiException(409, "Cửa hàng đã xác nhận đơn, vui lòng liên hệ cửa hàng để hủy.");
        await db.Procedure("dbo.CancelOrder", ("@ActorID", Actor), ("@OrderID", id), ("@Reason", input.Reason.Trim()));
        return OrderQueries_Resource(await OrderQueries.Read(db, id));
    }
}
