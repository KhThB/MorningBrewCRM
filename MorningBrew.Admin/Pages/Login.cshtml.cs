using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
public class LoginModel(CrmApi api):PageModel
{
    [BindProperty,Required,EmailAddress] public string Email {get;set;}="";
    [BindProperty,Required] public string Password {get;set;}="";
    public async Task<IActionResult> OnPostAsync()
    {
        if(!ModelState.IsValid) return Page();
        try
        {
            var login=await api.Login(Email,Password);
            if(login.User.Role=="Staff") { await api.Revoke(login.Token); ModelState.AddModelError("","Nhân viên sử dụng ứng dụng POS."); return Page(); }
            var claims=new[]{new Claim(ClaimTypes.NameIdentifier,login.User.Id.ToString()),new Claim(ClaimTypes.Name,login.User.FullName),new Claim(ClaimTypes.Email,login.User.Email),new Claim(ClaimTypes.Role,login.User.Role)};
            var properties=new AuthenticationProperties { IsPersistent=false,ExpiresUtc=DateTime.SpecifyKind(login.ExpiresAt,DateTimeKind.Utc) };
            properties.StoreTokens([new AuthenticationToken { Name="access_token",Value=login.Token }]);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)),properties);
            return RedirectToPage(login.User.Role=="Customer"?"/Portal":"/Index");
        }
        catch(CrmApiException e) { ModelState.AddModelError("",e.Message);return Page(); }
    }
}
