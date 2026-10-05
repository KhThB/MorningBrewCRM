using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
public class IndexModel(CrmApi api):PageModel
{
    public Dictionary<string,JsonElement> Stats {get;set;}=[];
    public async Task OnGetAsync()
    {
        try { Stats=await api.Get<Dictionary<string,JsonElement>>("v1/dashboard/overview"); }
        catch(CrmApiException e) { TempData["Error"]=e.Message; }
    }
}
