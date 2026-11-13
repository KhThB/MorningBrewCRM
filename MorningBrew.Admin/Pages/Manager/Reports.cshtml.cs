using System.Text.Json;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class ReportsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Báo cáo";
    public override string Subtitle=>"Báo cáo khách hàng theo độ tuổi, giới tính, sở thích (yêu cầu 4.1.5) và doanh thu.";
    public Dictionary<string,JsonElement>? Report {get;set;}
    // [UI2-W4] Kỳ doanh thu: 30 ngày gần nhất tính theo giờ Việt Nam (hôm nay + 29 ngày trước), không còn theo mốc UTC.
    public DateTime From {get;set;}
    public DateTime To {get;set;}
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>();
    protected override async Task LoadAsync()
    {
        Report=await Api.Get<Dictionary<string,JsonElement>>("v1/reports/customers");
        var today=DateTime.UtcNow.AddHours(7).Date;
        From=today.AddDays(-29); To=today;
        string Vn(DateTime d)=>Uri.EscapeDataString(d.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)+"T00:00:00+07:00");
        Rows=(await Api.Get<ApiPage>($"v1/reports/revenue?fromTime={Vn(From)}&toTime={Vn(To.AddDays(1))}")).Items; // [UI2-W4]
    }
}
