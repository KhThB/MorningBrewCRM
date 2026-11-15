using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class CustomersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Khách hàng";
    public override string Subtitle=>"Thêm, khóa, xóa khách hàng và điều chỉnh điểm (yêu cầu 4.1.1–4.1.3).";
    protected override string ListPath=>"v1/customers";
    protected override bool Searchable=>true;
    // [CRM-02] lọc theo nhóm hành vi mua; [CRM-08] "Duplicate" = SĐT đang dùng chung cho nhiều khách
    [BindProperty(SupportsGet=true)] public string? Segment {get;set;}
    protected override async Task LoadAsync()
    {
        var page=await Api.Get<ApiPage>($"v1/customers?pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}&q={Uri.EscapeDataString(Q??"")}&segment={Uri.EscapeDataString(Segment??"")}");
        Rows=page.Items; NextPageToken=page.NextPageToken;
    }

    // [CRM-07] Xuất danh sách khách (đúng bộ lọc đang xem, mọi trang) ra Excel.
    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows=new List<Dictionary<string,JsonElement>>(); string? token="";
        for(var i=0;i<100 && token is not null;i++)
        {
            var page=await Api.Get<ApiPage>($"v1/customers?pageSize=100&pageToken={Uri.EscapeDataString(token)}&q={Uri.EscapeDataString(Q??"")}&segment={Uri.EscapeDataString(Segment??"")}");
            rows.AddRange(page.Items); token=string.IsNullOrEmpty(page.NextPageToken)?null:page.NextPageToken;
        }
        var sheet=XlsxSheet.From("Khách hàng",rows,[("customer_id","Mã khách"),("full_name","Họ tên"),("email","Email"),("phone","Điện thoại"),
            ("date_of_birth","Ngày sinh"),("gender","Giới tính"),("loyalty_points","Điểm"),("completed_orders","Đơn hoàn tất"),("total_spent","Tổng chi tiêu (đ)"),
            ("last_order_at","Mua gần nhất"),("segment","Nhóm khách"),("is_active","Đang hoạt động")]);
        return File(XlsxExport.Build([sheet]),XlsxExport.ContentType,XlsxExport.FileName("KhachHang"));
    }
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"customer","customer-active","customer-delete","points","customer-update"} /* [WEB-04] */;

    // [UI-05] HTML cho ngăn chi tiết khách hàng: hồ sơ, đơn gần đây, phản hồi, giao dịch điểm.
    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        try { return Partial("_CustomerDetail",await Api.Get<Dictionary<string,JsonElement>>($"v1/customers/{id}/overview")); }
        catch(CrmApiException e) { return Content($"<div class=\"empty-state\">{System.Net.WebUtility.HtmlEncode(e.Message)}</div>","text/html"); }
    }
}
