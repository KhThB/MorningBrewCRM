using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
public class PortalModel(CrmApi api):PageModel
{
    public static readonly Dictionary<string,string> Titles=new(){["profile"]="Hồ sơ thành viên",["loyalty"]="Lịch sử điểm",["wallet"]="Ví voucher",["promotions"]="Ưu đãi",["feedbacks"]="Phản hồi của tôi",["surveys"]="Khảo sát dành cho tôi"};
    [BindProperty(SupportsGet=true)] public string Section{get;set;}="profile";
    [BindProperty(SupportsGet=true)] public string? PageToken {get;set;}
    public string? NextPageToken {get;set;}
    public string Title=>Titles.GetValueOrDefault(Section,"Thành viên");
    public List<Dictionary<string,JsonElement>> Rows{get;set;}=[];
    public async Task<IActionResult> OnGetAsync()
    {
        if(!Titles.ContainsKey(Section))return NotFound();
        try
        {
            if(Section=="profile")Rows=[await api.Get<Dictionary<string,JsonElement>>("v1/me/profile")];
            else { var page=await api.Get<ApiPage>($"v1/me/{Section}?pageToken={Uri.EscapeDataString(PageToken??"")}"); Rows=page.Items; NextPageToken=page.NextPageToken; }
        }
        catch(CrmApiException e){TempData["Error"]=e.Message;}
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string operation)
    {
        string Text(string key)=>Request.Form[key].ToString().Trim();
        try
        {
            switch(operation)
            {
                case "feedback":
                    await api.Send(HttpMethod.Post,"v1/me/feedbacks",new {content=Text("content"),type=Text("type"),rating=int.Parse(Text("rating")),branchId=string.IsNullOrEmpty(Text("branchId"))?(int?)null:int.Parse(Text("branchId"))});break;
                case "redeem":
                    await api.Send(HttpMethod.Post,$"v1/me/promotions/{int.Parse(Text("id"))}:redeem",new {requestKey=Guid.Parse(Text("requestKey"))});break;
                case "profile":
                    await api.Send(HttpMethod.Patch,"v1/me/profile",new {fullName=Text("fullName"),phone=Text("phone"),dateOfBirth=string.IsNullOrEmpty(Text("birth"))?null:Text("birth"),gender=Text("gender")},etag:Text("etag"));break;
                default:return BadRequest();
            }
            TempData["Success"]="Đã lưu thành công.";
        }
        catch(CrmApiException e){TempData["Error"]=e.Message;}
        catch(FormatException){TempData["Error"]="Thông tin nhập chưa hợp lệ.";}
        return RedirectToPage(new {section=Section});
    }
}
