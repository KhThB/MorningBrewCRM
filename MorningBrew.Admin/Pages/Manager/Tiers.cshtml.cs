using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;

// [CRM-01] Hạng thành viên: các hạng, mức chi tiêu 12 tháng tối thiểu, quyền lợi, số khách mỗi hạng.
public class TiersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Hạng thành viên";
    public override string Subtitle=>"Hạng tính theo chi tiêu 12 tháng gần nhất; đổi mức ở đây áp dụng ngay cho mọi khách, web và app.";
    protected override string ListPath=>"v1/tiers";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"tier","tier-update","tier-delete"};
    protected override async Task LoadAsync()=>Rows=(await Api.Get<ApiPage>(ListPath)).Items;
    protected override async Task<IActionResult?> ExecuteAsync(string operation)
    {
        object Body()=>new{name=Text("name"),minSpent=Money("minSpent"),benefit=Blank("benefit")};
        switch(operation)
        {
            case "tier": await Api.Send(HttpMethod.Post,"v1/tiers",Body()); TempData["Success"]="Đã thêm hạng."; break;
            case "tier-update": await Api.Send(HttpMethod.Put,$"v1/tiers/{Number("id")}",Body()); TempData["Success"]="Đã lưu hạng. Hạng của khách được tính lại ngay."; break;
            case "tier-delete": await Api.Send(HttpMethod.Delete,$"v1/tiers/{Number("id")}"); TempData["Success"]="Đã xóa hạng. Khách thuộc hạng này chuyển xuống hạng thấp hơn."; break;
        }
        return null;
    }
}
