using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// Giai đoạn 4 – yêu cầu II.1 "phương án sao lưu và phục hồi CSDL".
// [UI2-W3a] Sao lưu, phục hồi và sao lưu tự động đều làm trên web/API (Infrastructure/DatabaseBackup.cs);
// không còn cần database/tools/Backup.ps1, Restore.ps1.
public sealed partial class AdminController
{
    private DatabaseBackup Backups => HttpContext.RequestServices.GetRequiredService<DatabaseBackup>();

    [HttpGet("admin/backups")]
    public async Task<ApiPage> ListBackups()
    {
        await RequireOwner();
        var rows = Backups.Files().Take(50).Select(f => new Dictionary<string, object?>
        {
            ["FileName"] = f.Name,
            ["BackupType"] = DatabaseBackup.KindOf(f.Name) switch { DatabaseBackup.Auto => "Tự động", DatabaseBackup.BeforeRestore => "Trước khi phục hồi", _ => "Thủ công" },
            ["SizeMB"] = Math.Round(f.Length / 1024m / 1024m, 2),
            ["CreatedAt"] = f.LastWriteTimeUtc
        }).ToList();
        return new(rows, null);
    }

    [HttpPost("admin/backups")]
    public async Task<object> CreateBackup()
    {
        await RequireOwner();
        var info = await Backups.CreateAsync(DatabaseBackup.Manual, Actor);
        return new { fileName = info.Name, sizeMb = info.Exists ? Math.Round(info.Length / 1024m / 1024m, 2) : 0, directory = info.DirectoryName };
    }

    public record RestoreInput([Required, MaxLength(260)] string FileName);
    // [UI2-W3a] Phục hồi database đang chạy từ file .bak đã chọn. Mọi phiên đăng nhập quay về thời điểm của bản sao lưu.
    [HttpPost("admin/backups:restore")]
    public async Task<object> RestoreBackup(RestoreInput input)
    {
        await RequireOwner();
        var safety = await Backups.RestoreAsync(input.FileName, Actor);
        return new { restoredFrom = input.FileName, safetyBackup = safety };
    }
}
