-- Bản vá thiếu kho khi tồn đã bằng 0 (18/11/2026).
-- Chọn đúng database CRM trước khi chạy. Không xóa / cài lại dữ liệu.
-- Chạy lại an toàn; không ghi bù cho đơn cũ. Cài mới đã có sửa này trong 01_schema + 02_rules.
SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
IF DB_NAME() IN ('master','model','msdb','tempdb')
    THROW 51000, N'Chọn database MorningBrew CRM trước khi chạy bản vá.', 1;
IF OBJECT_ID(N'dbo.InventoryTransactions','U') IS NULL
   OR OBJECT_ID(N'dbo.RecipeItems','U') IS NULL
   OR OBJECT_ID(N'dbo.TR_Orders_Inventory','TR') IS NULL
   OR OBJECT_ID(N'dbo.CK_InventoryTransactions_4','C') IS NULL
    THROW 51000, N'Database chưa có cấu trúc kho MorningBrew CRM cần vá.', 1;

BEGIN TRY
    BEGIN TRAN;
    ALTER TABLE dbo.InventoryTransactions DROP CONSTRAINT CK_InventoryTransactions_4;
    ALTER TABLE dbo.InventoryTransactions WITH CHECK ADD CONSTRAINT CK_InventoryTransactions_4
        CHECK ([Kind] NOT IN ('Export','Waste') OR [QuantityChange] < 0 OR ([Kind]='Export' AND [QuantityChange]=0 AND [BalanceAfter]=0 AND [ActorID] IS NULL AND [Note] IS NOT NULL AND [Note] LIKE N'Bán hàng%thiếu%kiểm kê%'));
    EXEC sys.sp_executesql N'CREATE OR ALTER TRIGGER dbo.TR_Orders_Inventory ON dbo.Orders AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Status) RETURN;
    DECLARE @done TABLE(OrderID int PRIMARY KEY,OrderCode varchar(40));
    INSERT @done SELECT i.OrderID,i.OrderCode FROM inserted i JOIN deleted d ON d.OrderID=i.OrderID
    WHERE i.Status=''Completed'' AND d.Status<>''Completed'';
    IF NOT EXISTS(SELECT 1 FROM @done) RETURN;

    DECLARE @note nvarchar(300)=LEFT(CONCAT(N''Bán hàng – đơn '',(SELECT STRING_AGG(COALESCE(OrderCode,CONVERT(varchar(20),OrderID)),'', '') FROM @done)),240);
    DECLARE @use TABLE(IngredientID int PRIMARY KEY,Need decimal(18,3));
    INSERT @use
    SELECT r.IngredientID,SUM(od.Quantity*r.Quantity)
    FROM @done x JOIN dbo.OrderDetails od ON od.OrderID=x.OrderID
    JOIN dbo.RecipeItems r ON r.VariantID=od.VariantID
    JOIN dbo.Ingredients g ON g.IngredientID=r.IngredientID AND g.IsActive=1
    GROUP BY r.IngredientID;
    IF NOT EXISTS(SELECT 1 FROM @use) RETURN;

    DECLARE @moved TABLE(IngredientID int,Delta decimal(18,3),Balance decimal(18,3),Short decimal(18,3));
    UPDATE g SET Quantity=CASE WHEN g.Quantity>=u.Need THEN g.Quantity-u.Need ELSE 0 END,StockUpdatedAt=SYSUTCDATETIME()
    OUTPUT inserted.IngredientID,inserted.Quantity-deleted.Quantity,inserted.Quantity,u.Need-(deleted.Quantity-inserted.Quantity) INTO @moved
    FROM dbo.Ingredients g WITH(UPDLOCK,HOLDLOCK) JOIN @use u ON u.IngredientID=g.IngredientID;

    INSERT dbo.InventoryTransactions(IngredientID,ActorID,Kind,QuantityChange,BalanceAfter,Note)
    SELECT IngredientID,NULL,''Export'',Delta,Balance,
           CONCAT(@note,CASE WHEN Short>0 THEN CONCAT(N'' (thiếu '',FORMAT(Short,''0.###'',''vi-VN''),N'' – cần kiểm kê)'') END)
    -- Tồn đã bằng 0 vẫn cần phiếu tự động ghi lượng thiếu; không tạo biến động kho giả.
    -- CHECK của InventoryTransactions chỉ cho phép Export=0 khi là phiếu thiếu kho tự động.
    FROM @moved WHERE Delta<0 OR Short>0;
END;';
    COMMIT;
    PRINT N'Đã cập nhật: tồn bằng 0 vẫn ghi phiếu tự động kèm lượng thiếu; dữ liệu được giữ nguyên.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
