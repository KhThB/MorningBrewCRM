using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// Giai đoạn 4 – yêu cầu III.1 "phương án sao lưu và phục hồi CSDL".
// Sao lưu FULL (COPY_ONLY, CHECKSUM) ra thư mục Backup:Directory rồi kiểm tra lại bằng RESTORE VERIFYONLY.
// Phục hồi cần quyền độc quyền trên database nên chỉ làm bằng database/tools/Restore.ps1, không qua web.
// Lưu ý: thư mục là thư mục trên máy chạy SQL Server (LocalDB chạy cùng máy với API).
public sealed partial class AdminController
{
    private string BackupDirectory()
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var dir = config["Backup:Directory"];
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(env.ContentRootPath, "..", "database", ".local", "backups");
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [HttpGet("admin/backups")]
    public async Task<ApiPage> ListBackups()
    {
        await RequireOwner();
        var dir = BackupDirectory();
        var rows = new DirectoryInfo(dir).GetFiles("*.bak").OrderByDescending(f => f.LastWriteTimeUtc).Take(50)
            .Select(f => new Dictionary<string, object?>
            {
                ["FileName"] = f.Name,
                ["SizeMB"] = Math.Round(f.Length / 1024m / 1024m, 2),
                ["CreatedAt"] = f.LastWriteTimeUtc
            }).ToList();
        return new(rows, null);
    }

    [HttpPost("admin/backups")]
    public async Task<object> CreateBackup()
    {
        await RequireOwner();
        var dir = BackupDirectory();
        var database = (string)(await db.Query("SELECT DB_NAME() AS Name")).Single()["Name"]!;
        var file = Path.Combine(dir, $"{database}_{DateTime.UtcNow.AddHours(7):yyyyMMdd_HHmmss}.bak");
        await db.Query("""
            DECLARE @sql nvarchar(max)=N'BACKUP DATABASE '+QUOTENAME(DB_NAME())+N' TO DISK=@p WITH COPY_ONLY,CHECKSUM,INIT,NAME=@n;
                RESTORE VERIFYONLY FROM DISK=@p WITH CHECKSUM;';
            EXEC sys.sp_executesql @sql,N'@p nvarchar(4000),@n nvarchar(128)',@p=@path,@n=@name;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,Details) VALUES(@actor,'Database.Backup','Database',@path);
            """, ("@path", file), ("@name", $"MorningBrew web backup {DateTime.UtcNow:O}"), ("@actor", Actor));
        var info = new FileInfo(file);
        return new { fileName = info.Name, sizeMb = info.Exists ? Math.Round(info.Length / 1024m / 1024m, 2) : 0, directory = dir };
    }
}
