using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
namespace MorningBrew.Api.Infrastructure;

/// <summary>[UI2-W3a] Sao lưu / phục hồi CSDL hoàn toàn trên web (thay cho database/tools/Backup.ps1, Restore.ps1).
/// - Sao lưu: FULL, COPY_ONLY, CHECKSUM ra file .bak rồi kiểm tra lại bằng RESTORE VERIFYONLY.
/// - Phục hồi: tự sao lưu bản "truoc-phuc-hoi" trước, rồi kết nối master: SINGLE_USER → RESTORE … WITH REPLACE → MULTI_USER.
/// - Tự động: AutoBackupService gọi CreateAsync("auto") mỗi ngày và chỉ giữ N bản tự động mới nhất.
/// Thư mục là thư mục trên máy chạy SQL Server (LocalDB chạy cùng máy với API).</summary>
public sealed partial class DatabaseBackup(IConfiguration config, IWebHostEnvironment env, SqlStore db)
{
    public const string Auto = "auto", Manual = "manual", BeforeRestore = "truoc-phuc-hoi";
    public int Keep => int.TryParse(config["Backup:Keep"], out var k) && k > 0 ? k : 14;

    public string Folder()
    {
        var dir = config["Backup:Directory"];
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(env.ContentRootPath, "..", "database", ".local", "backups");
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string ConnectionString => config.GetConnectionString("MorningBrew")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:MorningBrew.");
    public string DatabaseName => new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;

    public static string KindOf(string fileName) =>
        fileName.Contains("_" + BeforeRestore + "_") ? BeforeRestore : fileName.Contains("_" + Auto + "_") ? Auto : Manual;

    public List<FileInfo> Files() => new DirectoryInfo(Folder()).GetFiles("*.bak").OrderByDescending(f => f.LastWriteTimeUtc).ToList();

    /// <summary>Tạo bản sao lưu đã kiểm tra. kind: manual / auto / truoc-phuc-hoi.</summary>
    public async Task<FileInfo> CreateAsync(string kind, int? actor)
    {
        var tag = kind == Manual ? "" : "_" + kind;
        var file = Path.Combine(Folder(), $"{DatabaseName}{tag}_{DateTime.UtcNow.AddHours(7):yyyyMMdd_HHmmss}.bak");
        await db.Query("""
            DECLARE @sql nvarchar(max)=N'BACKUP DATABASE '+QUOTENAME(DB_NAME())+N' TO DISK=@p WITH COPY_ONLY,CHECKSUM,INIT,NAME=@n;
                RESTORE VERIFYONLY FROM DISK=@p WITH CHECKSUM;';
            EXEC sys.sp_executesql @sql,N'@p nvarchar(4000),@n nvarchar(128)',@p=@path,@n=@name;
            IF @actor IS NOT NULL INSERT dbo.AuditLogs(ActorID,Action,EntityType,Details) VALUES(@actor,'Database.Backup','Database',@path);
            """, ("@path", file), ("@name", $"MorningBrew {kind} backup {DateTime.UtcNow:O}"), ("@actor", actor));
        return new FileInfo(file);
    }

    /// <summary>Xóa bớt bản sao lưu tự động cũ, chỉ giữ Keep bản mới nhất (bản thủ công không bị xóa).</summary>
    public int PruneAuto()
    {
        var old = Files().Where(f => KindOf(f.Name) == Auto).Skip(Keep).ToList();
        foreach (var f in old) f.Delete();
        return old.Count;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]+\.bak$")] private static partial Regex SafeName();

    /// <summary>[UI2-BK] Đường dẫn đầy đủ của một file .bak trong thư mục sao lưu (chặn tên file chứa đường dẫn).</summary>
    public string Resolve(string fileName)
    {
        if (!SafeName().IsMatch(fileName)) throw new ApiException(400, "Tên file sao lưu không hợp lệ.");
        var path = Path.Combine(Folder(), fileName);
        if (!File.Exists(path)) throw new ApiException(404, "Không tìm thấy file sao lưu.");
        return path;
    }

    /// <summary>Phục hồi database đang chạy từ một file .bak trong thư mục sao lưu. Trả về tên bản an toàn đã tạo trước khi phục hồi.</summary>
    public async Task<string> RestoreAsync(string fileName, int actor)
    {
        var path = Resolve(fileName);

        // 1) Kiểm tra file trước khi động vào database đang chạy.
        await db.Query("""
            DECLARE @sql nvarchar(max)=N'RESTORE VERIFYONLY FROM DISK=@p WITH CHECKSUM;';
            EXEC sys.sp_executesql @sql,N'@p nvarchar(4000)',@p=@path;
            """, ("@path", path));
        // 2) Bản an toàn: nếu phục hồi nhầm thì vẫn quay lại được trạng thái ngay trước đó.
        var safety = await CreateAsync(BeforeRestore, actor);

        // 3) Phục hồi qua master (không thể RESTORE database mà chính kết nối đang dùng).
        var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master", Pooling = false };
        SqlConnection.ClearAllPools();
        await using (var connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                DECLARE @q sysname=QUOTENAME(@db);
                BEGIN TRY
                    EXEC(N'ALTER DATABASE '+@q+N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE;');
                    DECLARE @sql nvarchar(max)=N'RESTORE DATABASE '+@q+N' FROM DISK=@p WITH REPLACE,CHECKSUM;';
                    EXEC sys.sp_executesql @sql,N'@p nvarchar(4000)',@p=@path;
                    EXEC(N'ALTER DATABASE '+@q+N' SET MULTI_USER;');
                END TRY
                BEGIN CATCH
                    IF DB_ID(@db) IS NOT NULL EXEC(N'ALTER DATABASE '+@q+N' SET MULTI_USER;');
                    THROW;
                END CATCH
                """, connection) { CommandTimeout = 300 };
            command.Parameters.AddWithValue("@db", DatabaseName);
            command.Parameters.AddWithValue("@path", path);
            await command.ExecuteNonQueryAsync();
        }
        SqlConnection.ClearAllPools();

        // 4) Ghi nhật ký vào database vừa phục hồi (tài khoản có thể không tồn tại ở thời điểm của bản sao lưu → bỏ qua).
        try
        {
            await db.Query("INSERT dbo.AuditLogs(ActorID,Action,EntityType,Details) SELECT @actor,'Database.Restore','Database',@path WHERE EXISTS(SELECT 1 FROM dbo.Users WHERE UserID=@actor)",
                ("@actor", actor), ("@path", path));
        }
        catch (SqlException) { }
        return safety.Name;
    }
}

/// <summary>[UI2-W3a] Sao lưu tự động hằng ngày (thay lịch Task Scheduler + Backup.ps1). Kiểm tra mỗi giờ:
/// nếu bản tự động mới nhất đã quá 24 giờ thì tạo bản mới và xóa bớt bản tự động cũ (giữ Backup:Keep, mặc định 14).
/// Tắt bằng Backup:Auto=false trong appsettings.</summary>
public sealed class AutoBackupService(DatabaseBackup backup, IConfiguration config, ILogger<AutoBackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(config["Backup:Auto"], "false", StringComparison.OrdinalIgnoreCase)) return;
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                var last = backup.Files().FirstOrDefault(f => DatabaseBackup.KindOf(f.Name) == DatabaseBackup.Auto);
                if (last is null || DateTime.UtcNow - last.LastWriteTimeUtc > TimeSpan.FromHours(24))
                {
                    var file = await backup.CreateAsync(DatabaseBackup.Auto, null);
                    logger.LogInformation("Auto backup created: {File}; pruned {Count} old auto backups", file.Name, backup.PruneAuto());
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Auto backup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
