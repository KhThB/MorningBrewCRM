using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class IndexModel(CrmApi api):PageModel
{
    public List<Dictionary<string,JsonElement>> Roles {get;set;}=[];
    public List<Dictionary<string,JsonElement>> Activity {get;set;}=[];
    public async Task OnGetAsync()
    {
        try
        {
            var data=await api.Get<Dictionary<string,JsonElement>>("v1/admin/overview");
            Roles=ReportView.Rows(data,"roles"); Activity=ReportView.Rows(data,"activity");
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
    }
}
