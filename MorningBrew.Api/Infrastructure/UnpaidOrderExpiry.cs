namespace MorningBrew.Api.Infrastructure;

// [BUG-02] Mỗi phút hủy các đơn trả trước quá hạn thanh toán (15 phút sau khi đặt) bằng dbo.ExpireUnpaidOrders.
// Procedure yêu cầu quyền Orders.Cancel phạm vi chuỗi nên chạy dưới danh nghĩa chủ cửa hàng đang hoạt động đầu tiên.
public sealed class UnpaidOrderExpiry(SqlStore db, ILogger<UnpaidOrderExpiry> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                var owner = (await db.Query("""
                    SELECT TOP(1) u.UserID FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
                    WHERE r.Code='Owner' AND u.IsActive=1 AND u.DeletedAt IS NULL ORDER BY u.UserID
                    """)).SingleOrDefault()?["UserID"];
                if (owner is not null) await db.Procedure("dbo.ExpireUnpaidOrders", ("@ActorID", owner));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Database tạm tắt / chưa cài: chỉ ghi log, lần sau chạy lại.
                logger.LogWarning(ex, "ExpireUnpaidOrders failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
