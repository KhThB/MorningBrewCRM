namespace MorningBrew.Api.Infrastructure;

// [BUG-01] Thực đơn đọc từ database (trước đây app dùng danh sách viết cứng). Dùng chung cho app khách
// (GET v1/me/menu) và app POS. Gồm cả món tạm hết (IsAvailable = 0) để app hiện nhãn "Tạm hết".
public static class MenuQueries
{
    public static async Task<object> Read(SqlStore db)
    {
        var branch = (await db.Query("""
            SELECT TOP(1) BranchID,Name,Address,Phone,OpeningTime,ClosingTime,AcceptingOrders,PickupEnabled,DeliveryEnabled,DeliveryFee
            FROM dbo.Branches WHERE IsActive=1 ORDER BY BranchID
            """)).SingleOrDefault() ?? throw new ApiException(404, "Chưa có cửa hàng đang hoạt động.");
        var id = branch["BranchID"];
        var items = await db.Query("""
            SELECT p.ProductID,p.Name,p.Description,p.ImageURL AS ImageUrl,p.CategoryID,c.Name AS Category,
            v.VariantID,v.Name AS VariantName,v.BasePrice AS Price,
            CAST(CASE WHEN bp.IsAvailable=1 AND COALESCE(bv.IsAvailable,1)=1 THEN 1 ELSE 0 END AS bit) AS IsAvailable
            FROM dbo.Products p JOIN dbo.Categories c ON c.CategoryID=p.CategoryID AND c.IsActive=1
            JOIN dbo.ProductVariants v ON v.ProductID=p.ProductID AND v.IsActive=1
            JOIN dbo.BranchProducts bp ON bp.ProductID=p.ProductID AND bp.BranchID=@branch
            LEFT JOIN dbo.BranchProductVariants bv ON bv.BranchID=@branch AND bv.VariantID=v.VariantID
            WHERE p.IsActive=1 ORDER BY c.CategoryID,p.ProductID,v.DisplayOrder,v.VariantID
            """, ("@branch", id));
        // Tùy chọn (đường, đá, topping): MinSelections > 0 là bắt buộc chọn khi đặt món (CreateOrder kiểm tra lại).
        var modifiers = await db.Query("""
            SELECT pg.ProductID,g.ModifierGroupID AS GroupId,g.Name AS GroupName,pg.MinSelections,pg.MaxSelections,
            o.ModifierOptionID AS OptionId,o.Name AS OptionName,o.ExtraPrice
            FROM dbo.ProductModifierGroups pg JOIN dbo.ModifierGroups g ON g.ModifierGroupID=pg.ModifierGroupID AND g.IsActive=1
            JOIN dbo.ModifierOptions o ON o.ModifierGroupID=g.ModifierGroupID AND o.IsActive=1
            LEFT JOIN dbo.BranchModifierOptions bo ON bo.BranchID=@branch AND bo.ModifierOptionID=o.ModifierOptionID
            WHERE COALESCE(bo.IsAvailable,1)=1 ORDER BY pg.ProductID,g.ModifierGroupID,o.DisplayOrder,o.ModifierOptionID
            """, ("@branch", id));
        return new { branch, items, modifiers, nextPageToken = (string?)null };
    }
}
