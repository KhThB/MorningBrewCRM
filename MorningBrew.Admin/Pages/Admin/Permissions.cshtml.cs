using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
// Ma trận vai trò × quyền (yêu cầu II.2 "phân quyền cho account"). Lưu = so sánh với trạng thái hiện tại
// và chỉ gửi các ô thay đổi lên API.
public class PermissionsModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Phân quyền theo vai trò";
    public override string Subtitle=>"Đánh dấu quyền cho từng vai trò. Thay đổi áp dụng cho mọi tài khoản có vai trò đó.";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"matrix"};
    public static readonly (string Code,string Name)[] Roles={("Owner","Chủ cửa hàng"),("BranchManager","Quản lý cửa hàng"),("Staff","Thu ngân")};
    public static readonly HashSet<string> EditableRoles=["BranchManager","Staff"];
    public List<Dictionary<string,JsonElement>> Matrix {get;set;}=[];
    public static string Column(string role)=>role switch{"BranchManager"=>"branch_manager","Staff"=>"staff",_=>"owner"};
    public static bool Granted(Dictionary<string,JsonElement> row,string role)=>ReportView.Int(row,Column(role))==1;
    public static bool Locked(Dictionary<string,JsonElement> row)=>row.TryGetValue("locked",out var v)&&v.ValueKind==JsonValueKind.True;

    private async Task<List<Dictionary<string,JsonElement>>> ReadMatrix()
        =>ReportView.Rows(await Api.Get<Dictionary<string,JsonElement>>("v1/permissions/matrix"),"permissions");
    protected override async Task LoadAsync()=>Matrix=await ReadMatrix();

    protected override async Task<IActionResult?> ExecuteAsync(string operation)
    {
        if(operation!="matrix") return await base.ExecuteAsync(operation);
        var wanted=Request.Form["grant"].Select(x=>x??"").ToHashSet();
        var changes=0;
        foreach(var row in await ReadMatrix())
        {
            if(Locked(row)) continue;
            var code=ReportView.Text(row,"permission");
            foreach(var role in EditableRoles)
            {
                var want=wanted.Contains($"{role}|{code}");
                if(want==Granted(row,role)) continue;
                await Api.Send(HttpMethod.Put,$"v1/roles/{role}/permissions/{Uri.EscapeDataString(code)}",new {allowed=want});
                changes++;
            }
        }
        TempData["Success"]=changes==0?"Không có thay đổi nào.":$"Đã cập nhật {changes} quyền. Người dùng đang đăng nhập nhận quyền mới ở thao tác kế tiếp.";
        return null;
    }
}
