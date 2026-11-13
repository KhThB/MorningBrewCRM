using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages.AdminArea;
public class BackupModel(CrmApi api):WorkspaceBase(api)
{
    public override string Title=>"Sao lưu & phục hồi";
    public override string Subtitle=>"Sao lưu, phục hồi và sao lưu tự động toàn bộ database (yêu cầu II.1).";
    protected override string ListPath=>"v1/admin/backups";
    protected override IReadOnlySet<string> Operations {get;}=new HashSet<string>{"backup","restore","backup-delete"}; // [UI2-W3a] phục hồi, [UI2-BK] xóa file
    protected override async Task LoadAsync()=>Rows=(await Api.Get<ApiPage>(ListPath)).Items;

    // [UI2-BK] Tải file .bak về máy: web Admin lấy file từ API (kèm token) rồi trả cho trình duyệt.
    public async Task<IActionResult> OnGetDownloadAsync(string file)
    {
        try
        {
            var (data,name)=await Api.Download($"v1/admin/backups/{Uri.EscapeDataString(file)}/download",file);
            return File(data,"application/octet-stream",name);
        }
        catch(CrmApiException e) { TempData["Error"]=e.Message; return RedirectToPage(); }
    }
    protected override async Task<IActionResult?> ExecuteAsync(string operation)
    {
        switch(operation)
        {
            case "backup":
                var r=await Api.Post<JsonElement>("v1/admin/backups",new {});
                TempData["Success"]=$"Đã sao lưu và kiểm tra: {r.GetProperty("file_name")} ({r.GetProperty("size_mb")} MB).";
                return null;
            case "restore": // [UI2-W3a]
                var x=await Api.Post<JsonElement>("v1/admin/backups:restore",new {fileName=Text("fileName")});
                TempData["Success"]=$"Đã phục hồi từ {x.GetProperty("restored_from")}. Bản an toàn trước khi phục hồi: {x.GetProperty("safety_backup")}. Nếu bị đăng xuất, hãy đăng nhập lại.";
                return null;
            case "backup-delete": // [UI2-BK]
                await Api.Send(HttpMethod.Delete,$"v1/admin/backups/{Uri.EscapeDataString(Text("fileName"))}");
                TempData["Success"]=$"Đã xóa file {Text("fileName")}.";
                return null;
            default: return await base.ExecuteAsync(operation);
        }
    }
}
