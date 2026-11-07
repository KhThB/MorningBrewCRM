using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class PermissionsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Phân quyền theo vai trò";
    public override string Subtitle=>"Quyền đang cấp cho từng vai trò. Thay đổi áp dụng cho mọi tài khoản có vai trò đó.";
    protected override string ListPath=>"v1/permissions";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"permission"};
    public static readonly string[] Codes={"Orders.Create","Orders.Manage","Orders.Cancel","Orders.Read","Payments.Manage","Refunds.Manage","Menu.Manage","Customers.Lookup","Customers.Read","Customers.Manage","Promotions.Manage","Surveys.Manage","Feedbacks.Manage","Reports.Read"};
}
