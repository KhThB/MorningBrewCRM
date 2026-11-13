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
    public Portal Portal => Portals.Of(HttpContext) ?? Portals.Manager; // [SEP-04] giao diện của cổng đang mở
    [BindProperty,Required,EmailAddress] public string Email {get;set;}="";
    [BindProperty,Required] public string Password {get;set;}="";
    public async Task<IActionResult> OnPostAsync()
    {
        if(!ModelState.IsValid) return Page();
        try
        {
            var login=await api.Login(Email,Password);
            // Web Admin chỉ dành cho nội bộ (chủ cửa hàng, quản lý). Khách hàng dùng app Morning Brew; thu ngân dùng POS.
            if(login.User.Role is "Staff" or "Customer")
            {
                await api.Revoke(login.Token);
                ModelState.AddModelError("",login.User.Role=="Customer"?"Khách hàng vui lòng sử dụng ứng dụng Morning Brew.":"Nhân viên sử dụng ứng dụng POS.");
                return Page();
            }
            // [SEP-04] Cổng Admin server chỉ cho chủ cửa hàng; quản lý dùng cổng Quản lý CRM.
            if(!Portals.Allows(Portal,login.User.Role))
            {
                await api.Revoke(login.Token);
                ModelState.AddModelError("",$"Tài khoản này không dùng {Portal.Title}. Quản lý cửa hàng đăng nhập tại {Portals.Manager.BaseUrl}.");
                return Page();
            }
            var claims=new[]{new Claim(ClaimTypes.NameIdentifier,login.User.Id.ToString()),new Claim(ClaimTypes.Name,login.User.FullName),new Claim(ClaimTypes.Email,login.User.Email),new Claim(ClaimTypes.Role,login.User.Role)};
            var properties=new AuthenticationProperties { IsPersistent=false,ExpiresUtc=DateTime.SpecifyKind(login.ExpiresAt,DateTimeKind.Utc) };
            properties.StoreTokens([new AuthenticationToken { Name="access_token",Value=login.Token }]);
            await HttpContext.SignInAsync(Portal.Scheme,new ClaimsPrincipal(new ClaimsIdentity(claims,Portal.Scheme)),properties); // [SEP-04] cookie riêng của giao diện này
            return Redirect(Portal.Home);
        }
        catch(CrmApiException e) { ModelState.AddModelError("",e.Message);return Page(); }
    }
}
