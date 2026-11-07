using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class BackupModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Sao lưu & phục hồi";
    public override string Subtitle=>"Sao lưu toàn bộ database và hướng dẫn phục hồi (yêu cầu III.1).";
    protected override string ListPath=>"v1/admin/backups";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"backup"};
    protected override async Task LoadAsync()=>Rows=(await Api.Get<ApiPage>(ListPath)).Items;
    protected override async Task<IActionResult?> ExecuteAsync(string operation)
    {
        if(operation!="backup") return await base.ExecuteAsync(operation);
        var r=await Api.Post<JsonElement>("v1/admin/backups",new {});
        TempData["Success"]=$"Đã sao lưu và kiểm tra: {r.GetProperty("file_name")} ({r.GetProperty("size_mb")} MB) tại {r.GetProperty("directory")}.";
        return null;
    }
}
