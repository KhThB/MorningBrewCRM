using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;

// [BUG-02] Trang Đơn hàng: danh sách theo trạng thái + chi tiết một đơn với các thao tác hợp lệ cho trạng thái hiện tại.
public class OrdersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>Id.HasValue?"Chi tiết đơn hàng":"Đơn hàng";
    public override string Subtitle=>"Đơn từ app khách và POS: xác nhận, pha chế, giao hàng, thu tiền, hủy, hoàn tiền.";
    protected override bool Searchable=>true;
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"order-advance","order-collect","order-cancel","order-delivery","order-refund","refund-complete"};
    [BindProperty(SupportsGet=true)] public string? Status {get;set;}="Active";
    [BindProperty(SupportsGet=true)] public int? Id {get;set;}
    public Dictionary<string,JsonElement>? Order {get;set;}
    public static readonly string[] Filters={"Active","AwaitingPayment","Pending","Confirmed","Preparing","Ready","Delivering","Completed","Cancelled"};

    protected override async Task LoadAsync()
    {
        if(Id.HasValue){ Order=await Api.Get<Dictionary<string,JsonElement>>($"v1/orders/{Id}"); return; }
        var page=await Api.Get<ApiPage>($"v1/orders?pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}&status={Uri.EscapeDataString(Status??"")}&q={Uri.EscapeDataString(Q??"")}");
        Rows=page.Items; NextPageToken=page.NextPageToken;
    }

    protected override async Task<IActionResult?> ExecuteAsync(string operation)
    {
        var id=Number("id");
        switch(operation)
        {
            case "order-advance":
                await Api.Send(HttpMethod.Post,$"v1/orders/{id}:advance",new {nextStatus=Text("next"),reason=NullIfEmpty("reason"),courierName=NullIfEmpty("courierName"),courierPhone=NullIfEmpty("courierPhone")});
                TempData["Success"]=$"Đã chuyển đơn sang: {OrderView.Status(Text("next"))}."; break;
            case "order-collect":
                await Api.Send(HttpMethod.Post,$"v1/orders/{id}:collect"); TempData["Success"]="Đã xác nhận thu tiền."; break;
            case "order-cancel":
                await Api.Send(HttpMethod.Post,$"v1/orders/{id}:cancel",new {reason=Text("reason")}); TempData["Success"]="Đã hủy đơn. Nếu đơn đã thanh toán, một yêu cầu hoàn tiền được tạo."; break;
            case "order-delivery":
                await Api.Send(HttpMethod.Post,$"v1/orders/{id}:delivery",new {status=Text("deliveryStatus"),reason=NullIfEmpty("reason")}); break;
            case "order-refund":
                await Api.Send(HttpMethod.Post,$"v1/orders/{id}:refund",new {reason=Text("reason")}); TempData["Success"]="Đã tạo yêu cầu hoàn tiền. Chốt kết quả ở mục Hoàn tiền."; break;
            case "refund-complete":
                await Api.Send(HttpMethod.Post,$"v1/refunds/{Number("refundId")}:complete",new {outcome=Text("outcome"),failureReason=NullIfEmpty("reason")}); break;
        }
        return RedirectToPage(new {id});
    }
    private string? NullIfEmpty(string key)=>string.IsNullOrWhiteSpace(Text(key))?null:Text(key);
}
