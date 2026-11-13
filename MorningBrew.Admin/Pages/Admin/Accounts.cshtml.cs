using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class AccountsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Tài khoản";
    public override string Subtitle=>"Thêm, khóa, xóa tài khoản và phân vai trò (yêu cầu II.2).";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"account","account-role","account-active","account-delete","account-password"} /* [BUG-05] */;
    [BindProperty(SupportsGet=true)] public string? Role {get;set;}
    [BindProperty(SupportsGet=true)] public string? Active {get;set;}
    public static readonly (string Code,string Name)[] Roles={("Owner","Chủ cửa hàng"),("BranchManager","Quản lý cửa hàng"),("Staff","Thu ngân"),("Customer","Khách hàng")};
    protected override async Task LoadAsync()
    {
        var query=$"v1/accounts?pageSize=50&pageToken={Uri.EscapeDataString(PageToken??"")}&q={Uri.EscapeDataString(Q??"")}&role={Uri.EscapeDataString(Role??"")}";
        if(Active is "true" or "false") query+=$"&active={Active}";
        var page=await Api.Get<ApiPage>(query);
        Rows=page.Items; NextPageToken=page.NextPageToken;
    }
}
