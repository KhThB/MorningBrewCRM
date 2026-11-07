using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MorningBrew.Admin.Pages;
public class IndexModel:PageModel
{
    // Chủ cửa hàng -> Quản trị; quản lý -> Quản lý CRM; vai trò khác không dùng web.
    public static string HomeFor(System.Security.Claims.ClaimsPrincipal user)=>
        user.IsInRole("Owner")?"/Admin/Index":user.IsInRole("BranchManager")?"/Manager/Index":"/AccessDenied";
    public IActionResult OnGet()=>RedirectToPage(HomeFor(User));
}
