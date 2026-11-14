using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;

// [WEB-10] Nhà cung cấp trong Quản lý CRM (cổng 5191) – cùng API, cùng giao diện với Admin server.
public class SuppliersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Nhà cung cấp";
    public override string Subtitle=>"Danh bạ nhà cung cấp nguyên liệu – dùng khi nhập kho.";
    protected override string ListPath=>"v1/suppliers";
    protected override bool Searchable=>true;
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"supplier","supplier-active","supplier-update"};
}
