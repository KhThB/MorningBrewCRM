using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class PromotionsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Ưu đãi";
    public override string Subtitle=>"Chương trình ưu đãi và voucher cho khách hàng.";
    protected override string ListPath=>"v1/promotions";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"promotion","issue","promotion-active"};
}
