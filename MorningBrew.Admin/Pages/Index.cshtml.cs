using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MorningBrew.Admin.Pages;
public class IndexModel:PageModel
{
    // Chủ cửa hàng -> Quản trị; quản lý -> Quản lý CRM; khách -> cổng thành viên.
    public static string HomeFor(System.Security.Claims.ClaimsPrincipal user)=>
        user.IsInRole("Owner")?"/Admin/Index":user.IsInRole("BranchManager")?"/Manager/Index":user.IsInRole("Customer")?"/Portal":"/AccessDenied";
    public IActionResult OnGet()=>RedirectToPage(HomeFor(User));
}
