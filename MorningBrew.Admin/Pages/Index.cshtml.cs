using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MorningBrew.Admin.Pages;
public class IndexModel:PageModel
{
    // [SEP-05] Trang gốc chuyển về trang chủ của giao diện theo cổng (5190 → Admin server, 5191 → Quản lý CRM).
    public IActionResult OnGet()
    {
        var portal=MorningBrew.Admin.Services.Portals.Of(HttpContext) ?? MorningBrew.Admin.Services.Portals.Manager;
        return MorningBrew.Admin.Services.Portals.Allows(portal,User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value) ? Redirect(portal.Home) : RedirectToPage("/AccessDenied");
    }
}
