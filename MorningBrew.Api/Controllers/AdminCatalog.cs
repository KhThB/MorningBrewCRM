using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;
namespace MorningBrew.Api.Controllers;

// [WEB-03] Sửa thông tin sản phẩm (tên, mô tả, danh mục, Đá / Nóng), thêm / ẩn size, quản lý danh mục.
// Chỉ chủ cửa hàng (giống tạo sản phẩm, sửa giá). App khách và POS đọc thực đơn mới sau khoảng 1 phút (bộ nhớ đệm).
public sealed partial class AdminController
{
    public record ProductUpdateInput([Required, MaxLength(150)] string Name, [MaxLength(2000)] string? Description,
        [Range(1, int.MaxValue)] int CategoryId, bool HasTemperature = false);
    [HttpPut("products/{id:int}")]
    public async Task<Dictionary<string, object?>> UpdateProduct(int id, ProductUpdateInput input)
    {
        await RequireOwner();
        if (string.IsNullOrWhiteSpace(input.Name)) throw new ApiException(400, "Cần nhập tên sản phẩm.", [new("FIELD_REQUIRED", "name")]);
        await db.Query($"""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF NOT EXISTS(SELECT 1 FROM dbo.Products WITH(UPDLOCK,HOLDLOCK) WHERE ProductID=@id) THROW 51104,N'Không tìm thấy sản phẩm.',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.Categories WHERE CategoryID=@category AND IsActive=1) THROW 51100,N'Danh mục không tồn tại hoặc đã ẩn.',1;
            UPDATE dbo.Products SET Name=@name,Description=@description,CategoryID=@category WHERE ProductID=@id;
            {TemperatureSql}
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Product.Update','Products',@id,@name);
            COMMIT;
            """, ("@id", id), ("@name", input.Name.Trim()), ("@description", string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim()),
            ("@category", input.CategoryId), ("@hasTemp", input.HasTemperature), ("@actor", Actor));
        return await ReadResource("products", id);
    }

    // Thêm một size cho sản phẩm có sẵn (ví dụ thêm size L). Đơn cũ không bị ảnh hưởng.
    [HttpPost("products/{id:int}/variants")]
    public async Task<IActionResult> AddVariant(int id, VariantInput input)
    {
        await RequireOwner();
        var variant = CheckVariants([input]).Single();
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF NOT EXISTS(SELECT 1 FROM dbo.Products WITH(UPDLOCK,HOLDLOCK) WHERE ProductID=@id) THROW 51104,N'Không tìm thấy sản phẩm.',1;
            IF EXISTS(SELECT 1 FROM dbo.ProductVariants WHERE ProductID=@id AND Name=@name) THROW 51120,N'Sản phẩm đã có size này. Muốn bán lại size đã ẩn thì bấm "Hiện size".',1;
            IF (SELECT COUNT(*) FROM dbo.ProductVariants WHERE ProductID=@id)>=5 THROW 51120,N'Tối đa 5 size cho một sản phẩm.',1;
            INSERT dbo.ProductVariants(ProductID,Name,BasePrice,DisplayOrder)
            VALUES(@id,@name,@price,(SELECT COALESCE(MAX(DisplayOrder),0)+1 FROM dbo.ProductVariants WHERE ProductID=@id));
            DECLARE @variant int=SCOPE_IDENTITY();
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Variant.Create','ProductVariants',@variant,@name);
            COMMIT; SELECT @variant AS VariantID;
            """, ("@id", id), ("@name", variant.Name), ("@price", variant.Price), ("@actor", Actor));
        var variantId = (int)created.Single()["VariantID"]!;
        return Created($"/v1/variants/{variantId}", await ReadResource("variants", variantId));
    }

    // Ẩn / hiện một size (không xóa vì đơn cũ còn tham chiếu). Phải còn ít nhất một size đang bán.
    [HttpPut("variants/{id:int}/active")]
    public async Task<IActionResult> VariantActive(int id, ActiveInput input)
    {
        await RequireOwner();
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @product int=(SELECT ProductID FROM dbo.ProductVariants WITH(UPDLOCK,HOLDLOCK) WHERE VariantID=@id);
            IF @product IS NULL THROW 51104,N'Không tìm thấy size.',1;
            IF @active=0 AND NOT EXISTS(SELECT 1 FROM dbo.ProductVariants WHERE ProductID=@product AND VariantID<>@id AND IsActive=1)
                THROW 51120,N'Phải còn ít nhất một size đang bán. Muốn thôi bán cả món thì dùng "Ngừng bán".',1;
            UPDATE dbo.ProductVariants SET IsActive=@active WHERE VariantID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Variant.Status','ProductVariants',@id,CASE WHEN @active=1 THEN 'Active' ELSE 'Hidden' END);
            COMMIT;
            """, ("@id", id), ("@active", input.IsActive), ("@actor", Actor));
        return NoContent();
    }

    // ----- Danh mục -----
    [HttpGet("categories")]
    public async Task<ApiPage> ListCategories()
    {
        await RequireOwner();
        var rows = await db.Query("""
            SELECT c.CategoryID,c.Name,c.IsActive,
            (SELECT COUNT(*) FROM dbo.Products p WHERE p.CategoryID=c.CategoryID AND p.IsActive=1) AS ActiveProducts
            FROM dbo.Categories c ORDER BY c.CategoryID
            """);
        return new ApiPage(rows.Select(r => ApiContract.Resource(r, "categories", "CategoryID")).ToList(), null);
    }
    public record CategoryInput([Required, StringLength(100, MinimumLength = 2)] string Name);
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(CategoryInput input)
    {
        await RequireOwner();
        var created = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF EXISTS(SELECT 1 FROM dbo.Categories WITH(UPDLOCK,HOLDLOCK) WHERE Name=@name) THROW 51120,N'Tên danh mục đã tồn tại.',1;
            INSERT dbo.Categories(Name) VALUES(@name);
            DECLARE @id int=SCOPE_IDENTITY();
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Category.Create','Categories',@id,@name);
            COMMIT; SELECT @id AS CategoryID;
            """, ("@name", input.Name.Trim()), ("@actor", Actor));
        return Created("/v1/categories", new { categoryId = created.Single()["CategoryID"] });
    }
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> RenameCategory(int id, CategoryInput input)
    {
        await RequireOwner();
        await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            IF NOT EXISTS(SELECT 1 FROM dbo.Categories WITH(UPDLOCK,HOLDLOCK) WHERE CategoryID=@id) THROW 51104,N'Không tìm thấy danh mục.',1;
            IF EXISTS(SELECT 1 FROM dbo.Categories WHERE Name=@name AND CategoryID<>@id) THROW 51120,N'Tên danh mục đã tồn tại.',1;
            UPDATE dbo.Categories SET Name=@name WHERE CategoryID=@id;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Category.Update','Categories',@id,@name);
            COMMIT;
            """, ("@id", id), ("@name", input.Name.Trim()), ("@actor", Actor));
        return NoContent();
    }
    // Ẩn danh mục: mọi món trong danh mục ẩn khỏi thực đơn app và POS (món vẫn giữ nguyên trạng thái riêng).
    [HttpPut("categories/{id:int}/active")]
    public async Task<IActionResult> CategoryActive(int id, ActiveInput input)
    {
        await RequireOwner();
        await db.Query("""
            UPDATE dbo.Categories SET IsActive=@active WHERE CategoryID=@id;
            IF @@ROWCOUNT=0 THROW 51104,N'Không tìm thấy danh mục.',1;
            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@actor,'Category.Status','Categories',@id,CASE WHEN @active=1 THEN 'Active' ELSE 'Hidden' END);
            """, ("@id", id), ("@active", input.IsActive), ("@actor", Actor));
        return NoContent();
    }
}
