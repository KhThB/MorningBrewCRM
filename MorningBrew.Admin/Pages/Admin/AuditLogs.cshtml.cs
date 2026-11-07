using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class AuditLogsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Nhật ký hệ thống";
    public override string Subtitle=>"Ai đã làm gì: tạo/khóa/xóa tài khoản, đổi vai trò, đổi quyền, xử lý phản hồi…";
    protected override string ListPath=>"v1/audit-logs";
    protected override bool Searchable=>true;
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>();
}
