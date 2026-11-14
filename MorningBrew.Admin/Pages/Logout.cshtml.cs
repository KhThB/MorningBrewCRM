using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
public class LogoutModel(CrmApi api):PageModel
{
    public IActionResult OnGet()=>RedirectToPage("/Index");
    public async Task<IActionResult> OnPostAsync()
    {
        try { await api.Send(HttpMethod.Post,"v1/auth/logout"); }
        catch(CrmApiException) { }
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Login");
    }
}
