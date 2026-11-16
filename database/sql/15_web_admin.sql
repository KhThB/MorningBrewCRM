-- 15_web_admin.sql – đợt sửa web Admin 15/11/2026 (chạy sau 14_inventory.sql). Chạy lại nhiều lần không sao.
--   [WEB-08] Thông báo cho khách khi giao hàng chưa thành công / giao lại, và khi hoàn tiền chưa thành công
--            (trước đây chỉ có thông báo "Hoàn tiền thành công"). Ghi đè dbo.UpdateDelivery, dbo.SimulateRefund
--            (03_orders.sql), giữ nguyên nghiệp vụ, chỉ thêm dòng INSERT dbo.Notifications.
--   [WEB-14] Kho gắn với bán hàng: bảng định mức dbo.RecipeItems (mỗi size dùng bao nhiêu nguyên liệu) và
--            trigger dbo.TR_Orders_Inventory – đơn chuyển sang "Hoàn tất" thì tự ghi phiếu "Xuất dùng" trừ kho.
--            Thiếu hàng thì trừ về 0 và ghi chú "thiếu … – cần kiểm kê" (không chặn việc hoàn tất đơn).
--            Kèm định mức mẫu cho 10 món của dữ liệu mẫu (chỉ thêm khi bảng còn trống).
-- [SQL-01] Chặn chạy nhầm database giống 14_inventory.sql.
SET NOEXEC OFF;
DECLARE @db sysname = DB_NAME();
IF OBJECT_ID(N'dbo.Ingredients', N'U') IS NULL OR OBJECT_ID(N'dbo.CreateOrder', N'P') IS NULL
BEGIN
    RAISERROR(N'>>> DỪNG: database đang chọn [%s] chưa phải MorningBrew CRM (đã chạy 01–14). Chọn đúng database (ví dụ MorningBrewCRM_Crm) rồi chạy lại.', 16, 1, @db);
    SET NOEXEC ON;
END
GO
SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
GO
-- ===== [WEB-08] Giao hàng: thêm thông báo khi giao chưa thành công / giao lại =====
CREATE OR ALTER PROCEDURE dbo.UpdateDelivery @ActorID int,@OrderID int,@Status varchar(12),@Reason nvarchar(500)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@orderStatus varchar(20),@old varchar(12),@customer int; -- [WEB-08] @customer
 SELECT @branch=BranchID,@orderStatus=Status,@customer=CustomerID FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@OrderID AND Fulfillment='Delivery';
 IF @branch IS NULL OR dbo.CanBranch(@ActorID,@branch,'Orders.Manage')=0 THROW 51103,N'Cannot update this delivery.',1;
 IF @orderStatus<>'Delivering' THROW 51100,N'Order is not in delivery phase.',1;
 SELECT @old=Status FROM dbo.Deliveries WHERE OrderID=@OrderID;
 IF @Status NOT IN ('Failed','Delivering') THROW 51100,N'Use AdvanceOrder for handover and completion.',1;
 IF @old=@Status BEGIN COMMIT; RETURN; END;
 IF NOT ((@old='Delivering' AND @Status='Failed') OR (@old='Failed' AND @Status='Delivering')) THROW 51100,N'Invalid delivery transition.',1;
 IF @Status='Failed' AND NULLIF(@Reason,'') IS NULL THROW 51100,N'Failure reason is required.',1;
 UPDATE dbo.Deliveries SET Status=@Status,FailureReason=CASE WHEN @Status='Failed' THEN @Reason END,AttemptCount=AttemptCount+CASE WHEN @Status='Delivering' THEN 1 ELSE 0 END,DispatchedAt=CASE WHEN @Status='Delivering' THEN SYSUTCDATETIME() ELSE DispatchedAt END WHERE OrderID=@OrderID;
 -- [WEB-08] báo cho khách: giao chưa thành công (kèm lý do) hoặc cửa hàng đang giao lại
 INSERT dbo.Notifications(UserID,Title,Body,OrderID)
 SELECT UserID,
        CASE WHEN @Status='Failed' THEN N'Giao hàng chưa thành công' ELSE N'Đang giao lại đơn hàng' END,
        CASE WHEN @Status='Failed' THEN CONCAT(N'Lý do: ',@Reason,N'. Cửa hàng sẽ liên hệ để giao lại hoặc hủy đơn.') ELSE N'Trạng thái mới: Đang giao lại' END,
        @OrderID
 FROM dbo.Customers WHERE CustomerID=@customer;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Delivery.Update','Deliveries',@OrderID,CONCAT(@Status,': ',@Reason));
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- ===== [WEB-08] Hoàn tiền: thêm thông báo khi hoàn tiền chưa thành công =====
CREATE OR ALTER PROCEDURE dbo.SimulateRefund @ActorID int,@RefundID int,@Outcome varchar(12),@FailureReason nvarchar(500)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @order int,@branch int,@customer int,@voucher int,@old varchar(12),@points int;
 SELECT @order=OrderID FROM dbo.Refunds WHERE RefundID=@RefundID;
 SELECT @branch=BranchID,@customer=CustomerID,@voucher=CustomerPromotionID FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@order;
 IF @branch IS NULL OR dbo.CanBranch(@ActorID,@branch,'Refunds.Manage')=0 THROW 51103,N'Cannot process this refund.',1;
 IF @Outcome NOT IN ('Succeeded','Failed') THROW 51100,N'Invalid refund result.',1;
 SELECT @old=Status FROM dbo.Refunds WITH(UPDLOCK,HOLDLOCK) WHERE RefundID=@RefundID;
 IF @old='Succeeded' BEGIN
  IF @Outcome<>'Succeeded' THROW 51109,N'Successful refund cannot change.',1;
  COMMIT; RETURN;
 END;
 UPDATE dbo.Refunds SET Status=@Outcome,AttemptCount=AttemptCount+1,FailureReason=CASE WHEN @Outcome='Failed' THEN @FailureReason END,ProviderReference=CASE WHEN @Outcome='Succeeded' THEN CONCAT('REF-',@RefundID) END,CompletedAt=CASE WHEN @Outcome='Succeeded' THEN SYSUTCDATETIME() END WHERE RefundID=@RefundID;
 IF @Outcome='Succeeded' BEGIN
  SELECT @points=Points FROM dbo.LoyaltyTransactions WHERE OrderID=@order AND Kind='Earn';
  IF @points IS NOT NULL BEGIN
   UPDATE dbo.Customers WITH(UPDLOCK) SET LoyaltyPoints=LoyaltyPoints-@points WHERE CustomerID=@customer;
   INSERT dbo.LoyaltyTransactions(CustomerID,OrderID,Kind,Points,RequestKey,Note) VALUES(@customer,@order,'Reverse',-@points,NEWID(),N'Thu hồi điểm do hoàn toàn bộ đơn');
  END;
  UPDATE dbo.CustomerPromotions SET Status='Available',UsedOrderID=NULL,UsedAt=NULL WHERE CustomerPromotionID=@voucher AND Status='Used' AND UsedOrderID=@order;
  INSERT dbo.Notifications(UserID,Title,Body,OrderID) SELECT UserID,N'Hoàn tiền thành công',N'Toàn bộ tiền của đơn đã được hoàn.',@order FROM dbo.Customers WHERE CustomerID=@customer;
 END;
 -- [WEB-08] hoàn tiền thất bại: báo cho khách biết cửa hàng sẽ xử lý lại
 IF @Outcome='Failed'
  INSERT dbo.Notifications(UserID,Title,Body,OrderID)
  SELECT UserID,N'Hoàn tiền chưa thành công',CONCAT(N'Cửa hàng sẽ thực hiện hoàn tiền lại.',CASE WHEN NULLIF(@FailureReason,'') IS NOT NULL THEN CONCAT(N' Lý do: ',@FailureReason) END),@order
  FROM dbo.Customers WHERE CustomerID=@customer;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Refund.Simulate','Refunds',@RefundID,@Outcome);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- ===== [WEB-14] Định mức nguyên liệu theo size =====
SET NOCOUNT ON; SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID('dbo.RecipeItems','U') IS NULL
CREATE TABLE dbo.[RecipeItems] (
    [VariantID]    int           NOT NULL,                   -- size của món (dbo.ProductVariants)
    [IngredientID] int           NOT NULL,                   -- nguyên liệu (dbo.Ingredients)
    [Quantity]     decimal(18,3) NOT NULL,                   -- lượng dùng cho 1 ly / 1 cái, theo đơn vị của nguyên liệu
    CONSTRAINT [PK_RecipeItems]   PRIMARY KEY ([VariantID],[IngredientID]),
    CONSTRAINT [CK_RecipeItems_0] CHECK ([Quantity] > 0)
);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_RecipeItems_VariantID')
    ALTER TABLE dbo.[RecipeItems] ADD CONSTRAINT [FK_RecipeItems_VariantID] FOREIGN KEY ([VariantID]) REFERENCES dbo.[ProductVariants]([VariantID]);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_RecipeItems_IngredientID')
    ALTER TABLE dbo.[RecipeItems] ADD CONSTRAINT [FK_RecipeItems_IngredientID] FOREIGN KEY ([IngredientID]) REFERENCES dbo.[Ingredients]([IngredientID]);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_RecipeItems_22' AND object_id=OBJECT_ID('dbo.RecipeItems'))
    CREATE INDEX IX_RecipeItems_22 ON dbo.[RecipeItems]([IngredientID]);

-- Định mức mẫu (lượng cho size M / Standard; size L dùng gấp 1,3 lần, ly và ống hút giữ 1).
IF NOT EXISTS(SELECT 1 FROM dbo.RecipeItems)
BEGIN
    DECLARE @mau TABLE(Product nvarchar(150),Code varchar(30),Qty decimal(18,3),Scale bit);
    INSERT @mau VALUES
    (N'Cà phê sữa','NL-CF01',0.020,1),(N'Cà phê sữa','NL-SU02',0.100,1),(N'Cà phê sữa','NL-PP01',1,0),(N'Cà phê sữa','NL-PP02',1,0),
    (N'Cà phê đen đá','NL-CF01',0.020,1),(N'Cà phê đen đá','NL-PP01',1,0),(N'Cà phê đen đá','NL-PP02',1,0),
    (N'Bạc xỉu','NL-CF01',0.010,1),(N'Bạc xỉu','NL-SU01',0.080,1),(N'Bạc xỉu','NL-SU02',0.080,1),(N'Bạc xỉu','NL-PP01',1,0),(N'Bạc xỉu','NL-PP02',1,0),
    (N'Cold brew cam sả','NL-CF02',0.025,1),(N'Cold brew cam sả','NL-TC03',0.050,1),(N'Cold brew cam sả','NL-PP01',1,0),(N'Cold brew cam sả','NL-PP02',1,0),
    (N'Trà đào','NL-TR01',0.008,1),(N'Trà đào','NL-TC01',0.050,1),(N'Trà đào','NL-BK03',0.020,1),(N'Trà đào','NL-PP01',1,0),(N'Trà đào','NL-PP02',1,0),
    (N'Trà vải lài','NL-TR01',0.008,1),(N'Trà vải lài','NL-BK03',0.020,1),(N'Trà vải lài','NL-PP01',1,0),(N'Trà vải lài','NL-PP02',1,0),
    (N'Trà sen vàng','NL-TR01',0.008,1),(N'Trà sen vàng','NL-SU03',0.030,1),(N'Trà sen vàng','NL-PP01',1,0),(N'Trà sen vàng','NL-PP02',1,0),
    (N'Bánh tiramisu','NL-BK02',0.030,0),(N'Bánh tiramisu','NL-CF01',0.005,0),
    (N'Bánh croissant bơ','NL-BK01',0.060,0),
    (N'Bánh mousse chanh dây','NL-SU03',0.040,0),(N'Bánh mousse chanh dây','NL-BK03',0.020,0);
    INSERT dbo.RecipeItems(VariantID,IngredientID,Quantity)
    SELECT v.VariantID,i.IngredientID,CASE WHEN m.Scale=1 AND v.Name='L' THEN ROUND(m.Qty*1.3,3) ELSE m.Qty END
    FROM @mau m JOIN dbo.Products p ON p.Name=m.Product
    JOIN dbo.ProductVariants v ON v.ProductID=p.ProductID
    JOIN dbo.Ingredients i ON i.Code=m.Code;
END;
COMMIT;
GO
-- ===== [WEB-14] Đơn hoàn tất → tự trừ kho theo định mức =====
-- Chạy trong cùng giao dịch với dbo.AdvanceOrder (POS và web Quản lý đều hoàn tất đơn qua thủ tục này).
-- Không bao giờ báo lỗi để chặn việc hoàn tất đơn: thiếu hàng thì trừ về 0 và ghi chú để kiểm kê.
CREATE OR ALTER TRIGGER dbo.TR_Orders_Inventory ON dbo.Orders AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Status) RETURN;
    DECLARE @done TABLE(OrderID int PRIMARY KEY,OrderCode varchar(40));
    INSERT @done SELECT i.OrderID,i.OrderCode FROM inserted i JOIN deleted d ON d.OrderID=i.OrderID
    WHERE i.Status='Completed' AND d.Status<>'Completed';
    IF NOT EXISTS(SELECT 1 FROM @done) RETURN;

    DECLARE @note nvarchar(300)=LEFT(CONCAT(N'Bán hàng – đơn ',(SELECT STRING_AGG(COALESCE(OrderCode,CONVERT(varchar(20),OrderID)),', ') FROM @done)),240);
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
    SELECT IngredientID,NULL,'Export',Delta,Balance,
           CONCAT(@note,CASE WHEN Short>0 THEN CONCAT(N' (thiếu ',FORMAT(Short,'0.###','vi-VN'),N' – cần kiểm kê)') END)
    FROM @moved WHERE Delta<0;
END;
GO
SET NOEXEC OFF; -- [SQL-01] trả lại trạng thái bình thường cho cửa sổ SSMS
GO
