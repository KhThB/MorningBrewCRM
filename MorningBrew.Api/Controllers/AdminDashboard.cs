using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [UI-D] Dữ liệu cho tab Tổng quan (dashboard) và ngăn chi tiết khách hàng của web quản lý.
// Doanh thu tính giống dbo.RevenueReport: đơn hoàn tất (theo CompletedAt) trừ khoản hoàn tiền thành công (theo ngày hoàn).
// Tháng / ngày tính theo giờ Việt Nam (UTC+7) – cùng quy ước với mã đơn MByyMMdd.
public sealed partial class AdminController
{
    private const string RevenueLedger = """
        WITH ledger AS (
            SELECT o.BranchID,DATEADD(hour,7,o.CompletedAt) LocalAt,1 Orders,o.TotalAmount Gross,CONVERT(decimal(18,2),0) Refunded
            FROM dbo.Orders o WHERE o.CompletedAt IS NOT NULL
            UNION ALL
            SELECT o.BranchID,DATEADD(hour,7,r.CompletedAt),0,0,r.Amount
            FROM dbo.Refunds r JOIN dbo.Orders o ON o.OrderID=r.OrderID WHERE r.Status='Succeeded' AND o.CompletedAt IS NOT NULL
        ), mine AS (SELECT * FROM ledger WHERE dbo.CanBranch(@actor,BranchID,'Reports.Read')=1)
        """;

    // [UI-D2][UI-D3][UI-D4][UI-D8] Một lần gọi trả đủ số liệu cho trang Tổng quan.
    [HttpGet("dashboard")]
    public async Task<object> Dashboard(int? year = null, int? month = null)
    {
        var now = DateTime.UtcNow.AddHours(7);
        var y = year ?? now.Year;
        var m = month ?? (y == now.Year ? now.Month : 12);
        if (y < 2000 || y > 2100 || m < 1 || m > 12) throw new ApiException(400, "Năm hoặc tháng không hợp lệ.");

        // D2: doanh thu 12 tháng của năm đã chọn (tháng không có đơn vẫn trả về 0 để biểu đồ đủ 12 cột).
        var months = await db.Query(RevenueLedger + """
            , m AS (SELECT v.n AS Month FROM (VALUES(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) v(n))
            SELECT m.Month,COALESCE(SUM(x.Orders),0) CompletedOrders,COALESCE(SUM(x.Gross),0) GrossRevenue,
                   COALESCE(SUM(x.Refunded),0) RefundedRevenue,COALESCE(SUM(x.Gross-x.Refunded),0) NetRevenue
            FROM m LEFT JOIN mine x ON YEAR(x.LocalAt)=@y AND MONTH(x.LocalAt)=m.Month
            GROUP BY m.Month ORDER BY m.Month
            """, ("@actor", Actor), ("@y", y));

        // D3: doanh thu từng ngày của tháng đã chọn.
        var days = await db.Query(RevenueLedger + """
            , d AS (SELECT TOP(DAY(EOMONTH(DATEFROMPARTS(@y,@m,1)))) ROW_NUMBER() OVER(ORDER BY (SELECT 1)) AS Day FROM sys.all_objects)
            SELECT d.Day,COALESCE(SUM(x.Orders),0) CompletedOrders,COALESCE(SUM(x.Gross-x.Refunded),0) NetRevenue
            FROM d LEFT JOIN mine x ON YEAR(x.LocalAt)=@y AND MONTH(x.LocalAt)=@m AND DAY(x.LocalAt)=d.Day
            GROUP BY d.Day ORDER BY d.Day
            """, ("@actor", Actor), ("@y", y), ("@m", m));

        // D4: top 5 sản phẩm bán chạy trong tháng (đơn hoàn tất).
        var topProducts = await db.Query("""
            SELECT TOP(5) d.ProductName,SUM(d.Quantity) Quantity,SUM(d.LineTotal) Revenue
            FROM dbo.Orders o JOIN dbo.OrderDetails d ON d.OrderID=o.OrderID
            WHERE o.CompletedAt IS NOT NULL AND YEAR(DATEADD(hour,7,o.CompletedAt))=@y AND MONTH(DATEADD(hour,7,o.CompletedAt))=@m
              AND dbo.CanBranch(@actor,o.BranchID,'Reports.Read')=1
            GROUP BY d.ProductName ORDER BY SUM(d.Quantity) DESC,SUM(d.LineTotal) DESC
            """, ("@actor", Actor), ("@y", y), ("@m", m));

        // D8: việc cần xử lý ngay (mỗi loại tối đa 5 dòng + tổng số).
        var pendingOrders = await db.Query("""
            SELECT TOP(5) o.OrderID,o.OrderCode,o.ContactName,o.TotalAmount,o.CreatedAt,COUNT(*) OVER() AS Total
            FROM dbo.Orders o WHERE o.Status='Pending' AND dbo.CanBranch(@actor,o.BranchID,'Orders.Read')=1 ORDER BY o.CreatedAt
            """, ("@actor", Actor));
        var pendingRefunds = await db.Query("""
            SELECT TOP(5) o.OrderID,o.OrderCode,r.Amount,r.Reason,r.CreatedAt,COUNT(*) OVER() AS Total
            FROM dbo.Refunds r JOIN dbo.Orders o ON o.OrderID=r.OrderID
            WHERE r.Status IN ('Pending','Failed') AND dbo.CanBranch(@actor,o.BranchID,'Orders.Read')=1 ORDER BY r.CreatedAt
            """, ("@actor", Actor));
        var newFeedbacks = await db.Query("""
            SELECT TOP(5) f.FeedbackID,u.FullName,f.Type,f.Content,f.Rating,f.CreatedAt,COUNT(*) OVER() AS Total
            FROM dbo.Feedbacks f JOIN dbo.Customers c ON c.CustomerID=f.CustomerID JOIN dbo.Users u ON u.UserID=c.UserID
            WHERE f.Status='New' AND ((f.BranchID IS NULL AND dbo.CanScope(@actor,'Chain',NULL,NULL,'Feedbacks.Manage')=1)
                  OR dbo.CanBranch(@actor,f.BranchID,'Feedbacks.Manage')=1)
            ORDER BY f.CreatedAt
            """, ("@actor", Actor));
        var endingSurveys = await db.Query("""
            SELECT TOP(5) s.SurveyID,s.Title,s.EndsAt,
                   (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID) Invited,
                   (SELECT COUNT(*) FROM dbo.SurveyRecipients r WHERE r.SurveyID=s.SurveyID AND r.CompletedAt IS NOT NULL) Completed,COUNT(*) OVER() AS Total
            FROM dbo.Surveys s
            WHERE s.Status='Open' AND s.StartsAt<=SYSUTCDATETIME() AND s.EndsAt>SYSUTCDATETIME() AND s.EndsAt<=DATEADD(day,3,SYSUTCDATETIME())
              AND dbo.CanScope(@actor,s.Scope,s.AreaID,s.BranchID,'Surveys.Manage')=1
            ORDER BY s.EndsAt
            """, ("@actor", Actor));

        return new { year = y, month = m, months, days, topProducts, todo = new { pendingOrders, pendingRefunds, newFeedbacks, endingSurveys } };
    }

    // [UI-05] Ngăn chi tiết khách hàng: hồ sơ, 5 đơn gần nhất, 5 phản hồi gần nhất, 5 giao dịch điểm gần nhất.
    [HttpGet("customers/{id:int}/overview")]
    public async Task<object> CustomerOverview(int id)
    {
        var profile = (await db.Query($"""
            SELECT c.CustomerID,u.FullName,u.Email,u.Phone,c.DateOfBirth,c.Gender,c.LoyaltyPoints,u.IsActive,u.CreatedAt,
                   (SELECT COUNT(*) FROM dbo.Orders o WHERE o.CustomerID=c.CustomerID AND o.Status='Completed') CompletedOrders,
                   (SELECT COALESCE(SUM(o.TotalAmount),0) FROM dbo.Orders o WHERE o.CustomerID=c.CustomerID AND o.Status='Completed') TotalSpent
            FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
            WHERE c.CustomerID=@id AND u.DeletedAt IS NULL AND {CustomerScope}
            """, ("@actor", Actor), ("@id", id))).SingleOrDefault() ?? throw new ApiException(404, "Không tìm thấy khách hàng.");
        var orders = await db.Query("""
            SELECT TOP(5) o.OrderID,o.OrderCode,o.Status,o.TotalAmount,o.CreatedAt FROM dbo.Orders o
            WHERE o.CustomerID=@id AND dbo.CanBranch(@actor,o.BranchID,'Orders.Read')=1 ORDER BY o.CreatedAt DESC
            """, ("@actor", Actor), ("@id", id));
        var feedbacks = await db.Query("""
            SELECT TOP(5) f.FeedbackID,f.Type,f.Content,f.Rating,f.Status,f.CreatedAt FROM dbo.Feedbacks f
            WHERE f.CustomerID=@id ORDER BY f.CreatedAt DESC
            """, ("@id", id));
        var loyalty = await db.Query("""
            SELECT TOP(5) l.Kind,l.Points,l.Note,l.CreatedAt FROM dbo.LoyaltyTransactions l
            WHERE l.CustomerID=@id ORDER BY l.CreatedAt DESC
            """, ("@id", id));
        return new { profile, orders, feedbacks, loyalty };
    }
}
