using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class IndexModel(CrmApi api):PageModel
{
    public Dictionary<string,JsonElement> Stats {get;set;}=[];
    // [UI-D] Dữ liệu dashboard: doanh thu theo tháng (D2), theo ngày (D3), top sản phẩm (D4), việc cần xử lý (D8).
    public Dictionary<string,JsonElement> Dash {get;set;}=[];
    [BindProperty(SupportsGet=true)] public int? Year {get;set;}
    [BindProperty(SupportsGet=true)] public int? Month {get;set;}
    public int CurrentYear=>DateTime.UtcNow.AddHours(7).Year;
    public async Task OnGetAsync()
    {
        try
        {
            Stats=await api.Get<Dictionary<string,JsonElement>>("v1/dashboard/overview");
            Dash=await api.Get<Dictionary<string,JsonElement>>($"v1/dashboard?year={Year}&month={Month}"); // [UI-D]
            Year=ReportView.Int(Dash,"year"); Month=ReportView.Int(Dash,"month");
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
    }
}
