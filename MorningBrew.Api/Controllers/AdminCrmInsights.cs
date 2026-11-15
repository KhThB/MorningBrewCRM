using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [CRM-02] Phân tích hành vi mua: danh sách khách kèm số đơn, tổng chi tiêu, lần mua gần nhất, nhóm hành vi
//          (view dbo.CustomerStats, 16_crm.sql) và lọc theo nhóm / SĐT trùng [CRM-08].
// [CRM-04] Báo cáo hiệu quả ưu đãi: đã cấp, đã dùng, chưa dùng, hết hạn, tổng tiền giảm, doanh thu đơn có voucher.
public sealed partial class AdminController
{
    // Thứ tự hiển thị các nhóm hành vi; "Duplicate" chỉ là bộ lọc danh sách (SĐT đang dùng chung).
    internal static readonly string[] CustomerSegments = ["None", "New", "Active", "Loyal", "Lapsed"];

    [HttpGet("customers")]
    public Task<ApiPage> ListCustomers([FromQuery] PageRequest page, string? q = null, string? segment = null)
    {
        if (segment is not (null or "" or "Duplicate") && !CustomerSegments.Contains(segment))
            throw new ApiException(400, "Nhóm khách hàng không hợp lệ.");
        var sql = $"""
            SELECT c.CustomerID,u.FullName,u.Email,u.Phone,c.DateOfBirth,c.Gender,c.LoyaltyPoints,
            s.CompletedOrders,s.TotalSpent,s.LastOrderAt,s.Segment,u.IsActive
            FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID JOIN dbo.CustomerStats s ON s.CustomerID=c.CustomerID
            WHERE u.DeletedAt IS NULL AND {CustomerScope}
            AND (@q='' OR u.FullName LIKE @search OR u.Email LIKE @search OR u.Phone LIKE @search)
            AND (@segment='' OR s.Segment=@segment OR (@segment='Duplicate' AND u.Phone IS NOT NULL AND EXISTS(
                SELECT 1 FROM dbo.Users d WHERE d.Phone=u.Phone AND d.UserID<>u.UserID AND d.RoleID=u.RoleID AND d.DeletedAt IS NULL)))
            """;
        return pages.Read(sql, ["CustomerID"], true, page,
            JsonSerializer.Serialize(new { Actor, section = "customers", q = q ?? "", segment = segment ?? "" }),
            [("@actor", Actor), ("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%"), ("@segment", segment ?? "")],
            "customers", "CustomerID");
    }

    [HttpGet("reports/promotions")]
    public async Task<ApiPage> PromotionReport()
    {
        await RequirePermission("Reports.Read");
        var rows = await db.Query("""
            SELECT p.PromotionID,p.Name,p.IsActive,p.PointsCost,p.TotalLimit,p.StartsAt,p.EndsAt,
            COUNT(cp.CustomerPromotionID) AS Issued,
            SUM(CASE WHEN cp.Status='Used' THEN 1 ELSE 0 END) AS Used,
            SUM(CASE WHEN cp.Status IN ('Available','Reserved') AND cp.ExpiresAt>SYSUTCDATETIME() THEN 1 ELSE 0 END) AS Unused,
            SUM(CASE WHEN cp.Status='Available' AND cp.ExpiresAt<=SYSUTCDATETIME() THEN 1 ELSE 0 END) AS Expired,
            CAST(COALESCE(SUM(o.DiscountAmount),0) AS decimal(18,2)) AS TotalDiscount,
            CAST(COALESCE(SUM(o.TotalAmount),0) AS decimal(18,2)) AS Revenue,
            COALESCE(SUM(cp.PointsSpent),0) AS PointsSpent
            FROM dbo.Promotions p
            LEFT JOIN dbo.CustomerPromotions cp ON cp.PromotionID=p.PromotionID
            LEFT JOIN dbo.Orders o ON o.OrderID=cp.UsedOrderID AND o.Status='Completed'
            WHERE dbo.CanScope(@actor,p.Scope,p.AreaID,p.BranchID,'Promotions.Manage')=1
            GROUP BY p.PromotionID,p.Name,p.IsActive,p.PointsCost,p.TotalLimit,p.StartsAt,p.EndsAt
            ORDER BY COUNT(cp.CustomerPromotionID) DESC,p.PromotionID DESC
            """, ("@actor", Actor));
        return new ApiPage(rows, null);
    }
}
