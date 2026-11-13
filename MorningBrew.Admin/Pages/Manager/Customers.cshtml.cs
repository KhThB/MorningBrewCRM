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
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"customer","customer-active","customer-delete","points"};

    // [UI-05] HTML cho ngăn chi tiết khách hàng: hồ sơ, đơn gần đây, phản hồi, giao dịch điểm.
    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        try { return Partial("_CustomerDetail",await Api.Get<Dictionary<string,JsonElement>>($"v1/customers/{id}/overview")); }
        catch(CrmApiException e) { return Content($"<div class=\"empty-state\">{System.Net.WebUtility.HtmlEncode(e.Message)}</div>","text/html"); }
    }
}
