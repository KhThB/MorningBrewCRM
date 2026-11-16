using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
// [SEP-03] Trang báo mở nhầm giao diện; link sang đúng cổng giữ nguyên đường dẫn đang mở.
public class WrongPortalModel:PageModel
{
    public Portal Current {get;private set;}=null!;
    public Portal Other {get;private set;}=null!;
    public string Target {get;private set;}="";
    public void OnGet(string? path)
    {
        Current=Portals.Of(HttpContext) ?? Portals.Manager;
        Other=Portals.Other(Current);
        var p=string.IsNullOrEmpty(path) || !path.StartsWith(Other.Prefix,StringComparison.OrdinalIgnoreCase) ? Other.Home : path;
        Target=Other.BaseUrl+p;
    }
}
