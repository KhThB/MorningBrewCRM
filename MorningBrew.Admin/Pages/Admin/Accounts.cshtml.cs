using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class AccountsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Tài khoản";
    public override string Subtitle=>"Thêm, khóa, xóa tài khoản và chọn vai trò (yêu cầu II.2).";
    protected override string ListPath=>"v1/accounts";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"account","account-active","account-delete"};
}
