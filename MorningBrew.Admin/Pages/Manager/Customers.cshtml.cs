using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.ManagerArea;
public class CustomersModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Khách hàng";
    public override string Subtitle=>"Thêm, khóa, xóa khách hàng và điều chỉnh điểm (yêu cầu 4.1.1–4.1.3).";
    protected override string ListPath=>"v1/customers";
    protected override bool Searchable=>true;
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"customer","customer-active","customer-delete","points"};
}
