using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class SuppliersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Nhà cung cấp";
    public override string Subtitle=>"Danh bạ nhà cung cấp nguyên liệu.";
    protected override string ListPath=>"v1/suppliers";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"supplier","supplier-active"};
}
