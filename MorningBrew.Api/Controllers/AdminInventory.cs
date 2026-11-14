using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MorningBrew.Api.Infrastructure;

namespace MorningBrew.Api.Controllers;

// [KHO] Quản lý kho nguyên liệu.
// Routes:
//   GET  v1/inventory/summary          – thống kê tổng quan + options cho form
//   GET  v1/inventory/ingredients      – danh sách tồn kho (phân trang)
//   POST v1/inventory/ingredients      – thêm nguyên liệu
//   PUT  v1/inventory/ingredients/{id} – sửa thông tin nguyên liệu
//   PUT  v1/inventory/ingredients/{id}/active – bật/tắt nguyên liệu
//   GET  v1/inventory/transactions     – lịch sử phiếu kho (phân trang)
//   POST v1/inventory/transactions     – ghi phiếu nhập/xuất/hủy/kiểm kê
// Quyền: Owner + BranchManager có Inventory.Manage.
[ApiController, Route("v1/inventory"), Authorize(Roles = "Owner,BranchManager")]
public sealed class InventoryController(SqlStore db, PageReader pages) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Kiểm tra quyền Inventory.Manage
    private async Task RequireInventory()
    {
        var ok = await db.Query(
            "SELECT 1 AS OK WHERE dbo.CanScope(@actor,'Chain',NULL,NULL,'Inventory.Manage')=1",
            ("@actor", Actor));
        if (ok.Count == 0) throw new ApiException(403, "Bạn không có quyền quản lý kho.");
    }

    // ─────────────────────────── SUMMARY ───────────────────────────────────
    /// <summary>Thống kê tổng quan kho + danh sách nguyên liệu và NCC để điền form.</summary>
    [HttpGet("summary")]
    public async Task<object> Summary()
    {
        await RequireInventory();
        var stats = (await db.Query("""
            SELECT
              (SELECT COUNT(*)   FROM dbo.Ingredients WHERE IsActive=1) AS ingredients,
              (SELECT COUNT(*)   FROM dbo.Ingredients WHERE IsActive=1 AND Quantity < MinStock) AS low_stock,
              CAST(COALESCE((SELECT SUM(TotalCost) FROM dbo.InventoryTransactions
                WHERE Kind='Import' AND CreatedAt >= DATEADD(day,-30,SYSUTCDATETIME())),0)
                AS decimal(18,2)) AS import_cost,
              (SELECT COUNT(*)   FROM dbo.InventoryTransactions
                WHERE CreatedAt >= DATEADD(hour,-24,SYSUTCDATETIME())) AS recent_transactions
            """)).Single();

        var ingredientOptions = await db.Query("""
            SELECT IngredientID AS id,
                   CONCAT('[',Code,'] ',Name,' (',Unit,')') AS display_name,
                   SupplierID AS supplier_id,
                   LastUnitCost AS last_unit_cost,
                   Unit AS unit,
                   Quantity AS quantity
            FROM dbo.Ingredients WHERE IsActive=1 ORDER BY Name
            """);

        var supplierOptions = await db.Query("""
            SELECT SupplierID AS id, Name AS display_name
            FROM dbo.Suppliers WHERE IsActive=1 ORDER BY Name
            """);

        return new
        {
            ingredients         = stats["ingredients"],
            low_stock           = stats["low_stock"],
            import_cost         = stats["import_cost"],
            recent_transactions = stats["recent_transactions"],
            ingredient_options  = ingredientOptions,
            supplier_options    = supplierOptions
        };
    }

    // ──────────────────────── INGREDIENTS LIST ──────────────────────────────
    /// <summary>Danh sách nguyên liệu (tab Tồn kho, phân trang keyset).</summary>
    [HttpGet("ingredients")]
    public async Task<ApiPage> ListIngredients(
        [FromQuery] PageRequest page,
        string? q = null,
        bool lowOnly = false,
        bool includeInactive = false)
    {
        await RequireInventory();
        var sql = """
            SELECT i.IngredientID AS ingredient_id,
                   i.Code         AS code,
                   CONCAT('[',i.Code,'] ',i.Name) AS display_name,
                   i.Name         AS name,
                   i.Unit         AS unit,
                   i.Quantity     AS quantity,
                   i.MinStock     AS min_stock,
                   CAST(CASE WHEN i.Quantity < i.MinStock THEN 1 ELSE 0 END AS bit) AS is_low,
                   i.LastUnitCost AS last_unit_cost,
                   i.SupplierID   AS supplier_id,
                   s.Name         AS supplier_name,
                   i.IsActive     AS is_active,
                   i.StockUpdatedAt AS stock_updated_at
            FROM dbo.Ingredients i
            LEFT JOIN dbo.Suppliers s ON s.SupplierID=i.SupplierID
            WHERE (@includeInactive=1 OR i.IsActive=1)
              AND (@lowOnly=0  OR i.Quantity < i.MinStock)
              AND (@q='' OR i.Name LIKE @search OR i.Code LIKE @search)
            """;
        return await pages.Read(sql, ["is_low", "ingredient_id"], true, page,
            JsonSerializer.Serialize(new { Actor, q = q ?? "", lowOnly, includeInactive }),
            [("@q", q ?? ""), ("@search", "%" + (q ?? "") + "%"),
             ("@lowOnly", lowOnly ? (object)1 : 0),
             ("@includeInactive", includeInactive ? (object)1 : 0)],
            "ingredients", "ingredient_id");
    }

    // ──────────────────────── CREATE INGREDIENT ─────────────────────────────
    public record IngredientInput(
        [Required, MaxLength(30)]  string Code,
        [Required, MaxLength(150)] string Name,
        [Required, MaxLength(20)]  string Unit,
        [Range(0, (double)decimal.MaxValue)] decimal MinStock = 0,
        int? SupplierId = null);

    /// <summary>Thêm nguyên liệu mới.</summary>
    [HttpPost("ingredients")]
    public async Task<IActionResult> CreateIngredient(IngredientInput input)
    {
        await RequireInventory();
        var created = await db.Query("""
            IF EXISTS(SELECT 1 FROM dbo.Ingredients WHERE Code=@code)
                THROW 51122, N'Mã nguyên liệu đã tồn tại.', 1;
            INSERT dbo.Ingredients(Code,Name,Unit,MinStock,SupplierID)
            VALUES(@code,@name,@unit,@minStock,@supplierId);
            SELECT SCOPE_IDENTITY() AS IngredientID;
            """,
            ("@code",       input.Code.Trim()),
            ("@name",       input.Name.Trim()),
            ("@unit",       input.Unit.Trim()),
            ("@minStock",   input.MinStock),
            ("@supplierId", (object?)input.SupplierId ?? DBNull.Value));

        var id = Convert.ToInt32(created.Single()["IngredientID"]);
        var row = await GetIngredientRow(id);
        return Created($"/v1/inventory/ingredients/{id}", row);
    }

    // ──────────────────────── UPDATE INGREDIENT ─────────────────────────────
    /// <summary>Cập nhật thông tin nguyên liệu (không thay đổi số lượng tồn).</summary>
    [HttpPut("ingredients/{id:int}")]
    public async Task<IActionResult> UpdateIngredient(int id, IngredientInput input)
    {
        await RequireInventory();
        await db.Query("""
            IF NOT EXISTS(SELECT 1 FROM dbo.Ingredients WHERE IngredientID=@id)
                THROW 51104, N'Không tìm thấy nguyên liệu.', 1;
            IF EXISTS(SELECT 1 FROM dbo.Ingredients WHERE Code=@code AND IngredientID<>@id)
                THROW 51122, N'Mã nguyên liệu đã tồn tại.', 1;
            UPDATE dbo.Ingredients
            SET Code=@code, Name=@name, Unit=@unit, MinStock=@minStock, SupplierID=@supplierId
            WHERE IngredientID=@id;
            """,
            ("@id",         id),
            ("@code",       input.Code.Trim()),
            ("@name",       input.Name.Trim()),
            ("@unit",       input.Unit.Trim()),
            ("@minStock",   input.MinStock),
            ("@supplierId", (object?)input.SupplierId ?? DBNull.Value));

        return Ok(await GetIngredientRow(id));
    }

    // ──────────────────────── ACTIVE / INACTIVE ─────────────────────────────
    public record IngredientActiveInput([property: System.Text.Json.Serialization.JsonRequired] bool IsActive);

    /// <summary>Bật / tắt sử dụng nguyên liệu.</summary>
    [HttpPut("ingredients/{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, IngredientActiveInput input)
    {
        await RequireInventory();
        await db.Query("""
            UPDATE dbo.Ingredients SET IsActive=@active WHERE IngredientID=@id;
            IF @@ROWCOUNT=0 THROW 51104,'Ingredient not found.',1;
            """, ("@id", id), ("@active", input.IsActive));
        return NoContent();
    }

    // ──────────────────────── TRANSACTIONS LIST ─────────────────────────────
    /// <summary>Lịch sử phiếu kho, phân trang keyset (mới nhất trước).</summary>
    [HttpGet("transactions")]
    public async Task<ApiPage> ListTransactions(
        [FromQuery] PageRequest page,
        int?    ingredientId = null,
        string? kind         = null,
        DateTimeOffset? fromTime = null,
        DateTimeOffset? toTime   = null)
    {
        await RequireInventory();
        var sql = """
            SELECT t.TransactionID  AS transaction_id,
                   t.IngredientID   AS ingredient_id,
                   i.Code           AS code,
                   i.Name           AS ingredient_name,
                   i.Unit           AS unit,
                   t.Kind           AS kind,
                   t.QuantityChange AS quantity_change,
                   t.BalanceAfter   AS balance_after,
                   t.UnitCost       AS unit_cost,
                   t.TotalCost      AS total_cost,
                   s.Name           AS supplier_name,
                   u.FullName       AS actor_name,
                   t.Note           AS note,
                   t.CreatedAt      AS created_at
            FROM dbo.InventoryTransactions t
            JOIN dbo.Ingredients i ON i.IngredientID=t.IngredientID
            LEFT JOIN dbo.Suppliers s ON s.SupplierID=t.SupplierID
            LEFT JOIN dbo.Users    u ON u.UserID=t.ActorID
            WHERE (@ingredientId IS NULL OR t.IngredientID=@ingredientId)
              AND (@kind IS NULL         OR t.Kind=@kind)
              AND (@fromTime IS NULL     OR t.CreatedAt >= @fromTime)
              AND (@toTime   IS NULL     OR t.CreatedAt <  @toTime)
            """;
        return await pages.Read(sql, ["transaction_id"], false, page,
            JsonSerializer.Serialize(new { Actor, ingredientId, kind,
                from = fromTime?.ToString("O"), to = toTime?.ToString("O") }),
            [("@ingredientId", (object?)ingredientId          ?? DBNull.Value),
             ("@kind",         (object?)kind                  ?? DBNull.Value),
             ("@fromTime",     (object?)fromTime?.UtcDateTime  ?? DBNull.Value),
             ("@toTime",       (object?)toTime?.UtcDateTime    ?? DBNull.Value)],
            "transactions", "transaction_id");
    }

    // ──────────────────────── POST TRANSACTION ──────────────────────────────
    public record TransactionInput(
        [Range(1, int.MaxValue)] int IngredientId,
        [Required] string Kind,                            // Import | Export | Waste | Adjust
        [Range(0, (double)decimal.MaxValue)] decimal Quantity, // luôn >= 0; hướng tính theo Kind
        decimal? UnitCost   = null,                        // chỉ khi Kind=Import
        int?     SupplierId = null,                        // chỉ khi Kind=Import
        [MaxLength(300)] string? Note = null);

    /// <summary>Ghi phiếu kho (nhập / xuất dùng / hủy hỏng / kiểm kê).</summary>
    [HttpPost("transactions")]
    public async Task<IActionResult> PostTransaction(TransactionInput input)
    {
        await RequireInventory();

        if (input.Kind is not ("Import" or "Export" or "Waste" or "Adjust"))
            throw new ApiException(400, "Kind phải là Import, Export, Waste hoặc Adjust.");

        if (input.Kind is "Import" or "Export" or "Waste" && input.Quantity <= 0)
            throw new ApiException(400, "Số lượng nhập, xuất hoặc hủy phải lớn hơn 0.");

        // Với Adjust: Quantity là số thực tế đếm được (hệ thống tự tính chênh lệch trong SQL).
        var result = await db.Query("""
            SET XACT_ABORT ON; BEGIN TRAN;
            DECLARE @current decimal(18,3), @delta decimal(18,3), @balance decimal(18,3), @isActive bit;
            SELECT @current=Quantity, @isActive=IsActive
            FROM dbo.Ingredients WITH(UPDLOCK,HOLDLOCK)
            WHERE IngredientID=@id;
            IF @current IS NULL THROW 51104,N'Không tìm thấy nguyên liệu.',1;
            IF @isActive=0 THROW 51124,N'Nguyên liệu này đã ngừng dùng, không thể ghi phiếu mới.',1;

            SET @delta = CASE @kind
                WHEN 'Import' THEN  @qty
                WHEN 'Export' THEN -@qty
                WHEN 'Waste'  THEN -@qty
                WHEN 'Adjust' THEN  @qty - @current
            END;
            SET @balance = @current + @delta;
            IF @balance < 0 THROW 51125,N'Số lượng xuất/hủy vượt quá tồn kho hiện tại.',1;

            UPDATE dbo.Ingredients
            SET Quantity        = @balance,
                LastUnitCost    = CASE WHEN @kind='Import' AND @unitCost IS NOT NULL
                                       THEN @unitCost ELSE LastUnitCost END,
                SupplierID      = CASE WHEN @kind='Import' AND @supplierId IS NOT NULL
                                       THEN @supplierId ELSE SupplierID END,
                StockUpdatedAt  = SYSUTCDATETIME()
            WHERE IngredientID=@id;

            INSERT dbo.InventoryTransactions
                (IngredientID,SupplierID,ActorID,Kind,QuantityChange,BalanceAfter,UnitCost,Note)
            VALUES(@id,@supplierId,@actor,@kind,@delta,@balance,@unitCost,@note);

            INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details)
            VALUES(@actor,'Inventory.Transaction','Ingredients',@id,@kind);

            COMMIT;
            SELECT SCOPE_IDENTITY() AS TransactionID, @balance AS balance_after, @delta AS quantity_change;
            """,
            ("@id",         input.IngredientId),
            ("@kind",       input.Kind),
            ("@qty",        input.Quantity),
            ("@unitCost",   (object?)input.UnitCost   ?? DBNull.Value),
            ("@supplierId", (object?)input.SupplierId ?? DBNull.Value),
            ("@note",       (object?)input.Note       ?? DBNull.Value),
            ("@actor",      Actor));

        var row = result.Single();
        return Ok(new
        {
            transaction_id  = Convert.ToInt32(row["TransactionID"]),
            balance_after   = row["balance_after"],
            quantity_change = row["quantity_change"]
        });
    }

    // ──────────────────────── HELPER ────────────────────────────────────────
    private async Task<Dictionary<string, object?>> GetIngredientRow(int id)
    {
        var rows = await db.Query("""
            SELECT i.IngredientID AS ingredient_id,
                   i.Code,i.Name,i.Unit,i.Quantity,i.MinStock,
                   i.LastUnitCost AS last_unit_cost,
                   i.SupplierID   AS supplier_id,
                   s.Name         AS supplier_name,
                   i.IsActive     AS is_active,
                   i.StockUpdatedAt AS stock_updated_at,
                   i.CreatedAt      AS created_at
            FROM dbo.Ingredients i
            LEFT JOIN dbo.Suppliers s ON s.SupplierID=i.SupplierID
            WHERE i.IngredientID=@id
            """, ("@id", id));
        return rows.SingleOrDefault()
               ?? throw new ApiException(404, "Không tìm thấy nguyên liệu.");
    }
}
