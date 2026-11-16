using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;

// [WEB-05] Người đang đăng nhập tự đổi mật khẩu: gọi POST v1/auth/password (API kiểm tra mật khẩu hiện tại,
// thu hồi các phiên khác, ghi nhật ký Account.ChangePassword).
public class PasswordModel(CrmApi api):PageModel
{
    public string? Error {get;set;}
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync(string? current,string? next,string? confirm)
    {
        if(string.IsNullOrEmpty(current)||string.IsNullOrEmpty(next)) Error="Vui lòng nhập đủ mật khẩu hiện tại và mật khẩu mới.";
        else if(next.Length<10) Error="Mật khẩu mới phải có ít nhất 10 ký tự.";
        else if(next!=confirm) Error="Mật khẩu nhập lại không khớp.";
        else if(next==current) Error="Mật khẩu mới phải khác mật khẩu hiện tại.";
        if(Error is not null) return Page();
        try { await api.Send(HttpMethod.Post,"v1/auth/password",new {currentPassword=current,newPassword=next}); }
        catch(CrmApiException e) { Error=e.Message; return Page(); }
        TempData["Success"]="Đã đổi mật khẩu. Lần đăng nhập sau dùng mật khẩu mới.";
        return RedirectToPage();
    }
}
