using System.Text.Json;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class ReportsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Báo cáo";
    public override string Subtitle=>"Báo cáo khách hàng theo độ tuổi, giới tính, sở thích (yêu cầu 4.1.5) và doanh thu.";
    public Dictionary<string,JsonElement>? Report {get;set;}
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>();
    protected override async Task LoadAsync()
    {
        Report=await Api.Get<Dictionary<string,JsonElement>>("v1/reports/customers");
        Rows=(await Api.Get<ApiPage>("v1/reports/revenue")).Items;
    }
}
