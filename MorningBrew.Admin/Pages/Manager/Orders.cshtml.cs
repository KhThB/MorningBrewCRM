using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;

// [BUG-02] Trang Đơn hàng: danh sách theo trạng thái + chi tiết một đơn.
// [ORD-01] Chỉ xem đơn và xử lý hoàn tiền. Vận hành đơn (xác nhận → pha chế → sẵn sàng → giao → hoàn tất, thu tiền mặt,
//          hủy, báo giao chưa thành công / giao lại) thực hiện trên POS để không trùng chức năng.
public class OrdersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>Id.HasValue?"Chi tiết đơn hàng":"Đơn hàng";
    protected override bool Searchable=>true;
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"order-refund","refund-complete"};
    // [REV-10] Mặc định mở tab Chờ hoàn tiền – việc duy nhất quản lý xử lý trên trang này.
    [BindProperty(SupportsGet=true)] public string? Status {get;set;}="RefundPending";
    [BindProperty(SupportsGet=true)] public int? Id {get;set;}
    public Dictionary<string,JsonElement>? Order {get;set;}
    // [REV-10] Rút từ 10 tab (+ Tất cả) còn 4 tab (+ Tất cả): các bước pha chế xử lý trên POS, trạng thái chi tiết vẫn hiện ở cột Trạng thái.
    public static readonly string[] Filters={"RefundPending","Active","Completed","Cancelled"};

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
            case "order-refund":
                await Api.Send(HttpMethod.Post,$"v1/orders/{id}:refund",new {reason=Text("reason")}); TempData["Success"]="Đã tạo yêu cầu hoàn tiền. Ghi nhận kết quả ngay bên dưới."; break;
            case "refund-complete":
                await Api.Send(HttpMethod.Post,$"v1/refunds/{Number("refundId")}:complete",new {outcome=Text("outcome"),failureReason=NullIfEmpty("reason")});
                TempData["Success"]=Text("outcome")=="Succeeded"?"Hoàn tiền thành công: đã thu hồi điểm và trả lại voucher (nếu có).":"Đã ghi nhận hoàn tiền thất bại."; break;
        }
        return RedirectToPage(new {id});
    }
    private string? NullIfEmpty(string key)=>string.IsNullOrWhiteSpace(Text(key))?null:Text(key);
}
