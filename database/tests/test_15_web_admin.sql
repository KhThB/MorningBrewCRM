-- test_15_web_admin.sql – kiểm thử SQL cho kho gắn với bán hàng, thông báo giao hàng / hoàn tiền [WEB-08][WEB-14] (trước ở 15_web_admin.sql, nay trong 01–07) trên database có dữ liệu mẫu (06, 07).
-- KHÔNG làm thay đổi dữ liệu: mọi thao tác nằm trong một giao dịch và ROLLBACK ở cuối.
-- SSMS: chọn database MorningBrewCRM_Crm → mở file → Execute (F5). Đọc tab Messages: mỗi dòng PASS / FAIL, cuối cùng là tổng kết.
SET NOCOUNT ON; SET XACT_ABORT OFF;
IF OBJECT_ID(N'dbo.RecipeItems','U') IS NULL OR OBJECT_ID(N'dbo.TR_Orders_Inventory','TR') IS NULL
BEGIN RAISERROR(N'Database chưa cài bằng fullscript bản mới (01–07).',16,1); RETURN; END;
DECLARE @pass int=0,@fail int=0,@msg nvarchar(400);
DECLARE @r TABLE(ID int);
DECLARE @key uniqueidentifier,@order int,@payment int,@refund int,@code varchar(40),@cnt int,@q1 decimal(18,3),@q2 decimal(18,3);
DECLARE @staff int=(SELECT UserID FROM dbo.Users WHERE Email='staff.1@morningbrew.test'),
        @manager int=(SELECT UserID FROM dbo.Users WHERE Email='manager.1@morningbrew.test'),
        @lanUser int=(SELECT UserID FROM dbo.Users WHERE Email='lan@morningbrew.test'),
        @minhUser int=(SELECT UserID FROM dbo.Users WHERE Email='minh@morningbrew.test');
DECLARE @lan int=(SELECT CustomerID FROM dbo.Customers WHERE UserID=@lanUser),
        @minh int=(SELECT CustomerID FROM dbo.Customers WHERE UserID=@minhUser),
        @branch int=(SELECT TOP(1) BranchID FROM dbo.Branches WHERE IsActive=1 ORDER BY BranchID),
        @minhAddress int=(SELECT TOP(1) AddressID FROM dbo.CustomerAddresses ca JOIN dbo.Customers c ON c.CustomerID=ca.CustomerID JOIN dbo.Users u ON u.UserID=c.UserID WHERE u.Email='minh@morningbrew.test' AND ca.IsActive=1 ORDER BY ca.IsDefault DESC);
DECLARE @cake int=(SELECT TOP(1) v.VariantID FROM dbo.ProductVariants v JOIN dbo.Products p ON p.ProductID=v.ProductID WHERE p.Name=N'Bánh tiramisu' AND v.IsActive=1);
DECLARE @bk02 int=(SELECT IngredientID FROM dbo.Ingredients WHERE Code='NL-BK02'),@cf01 int=(SELECT IngredientID FROM dbo.Ingredients WHERE Code='NL-CF01');
DECLARE @items nvarchar(200)=CONCAT(N'[{"variantId":',@cake,N',"quantity":2}]');
IF @staff IS NULL OR @lan IS NULL OR @minh IS NULL OR @cake IS NULL OR @bk02 IS NULL
BEGIN RAISERROR(N'Database không có dữ liệu mẫu (06_seed, 07_demo). Chạy trên database cài bằng fullscript.',16,1); RETURN; END;

BEGIN TRAN;
BEGIN TRY
    -- TC-SQL-01: định mức mẫu
    SET @cnt=(SELECT COUNT(*) FROM dbo.RecipeItems);
    IF @cnt>=30 BEGIN SET @pass+=1; PRINT CONCAT(N'PASS TC-SQL-01 Có định mức mẫu (',@cnt,N' dòng)'); END
    ELSE BEGIN SET @fail+=1; PRINT CONCAT(N'FAIL TC-SQL-01 Định mức mẫu chỉ có ',@cnt,N' dòng'); END

    -- TC-SQL-02: đơn POS hoàn tất → tự trừ kho theo định mức (2 bánh tiramisu: cream cheese 0,06 kg; cà phê 0,01 kg)
    SELECT @q1=Quantity FROM dbo.Ingredients WHERE IngredientID=@bk02; SELECT @q2=Quantity FROM dbo.Ingredients WHERE IngredientID=@cf01;
    SET @key=NEWID(); INSERT @r EXEC dbo.CreateOrder @ActorID=@staff,@BranchID=@branch,@Source='POS',@Fulfillment='Pickup',@PaymentMode='COD',@Items=@items,@RequestKey=@key;
    SELECT @order=ID FROM @r; DELETE @r;
    SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @staff,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
    EXEC dbo.SimulatePayment @staff,@payment,'Succeeded';
    EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready';
    SET @cnt=(SELECT COUNT(*) FROM dbo.InventoryTransactions WHERE Note LIKE N'%'+(SELECT OrderCode FROM dbo.Orders WHERE OrderID=@order)+N'%');
    IF @cnt=0 BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-02a Đơn chưa hoàn tất (Ready) → chưa trừ kho'; END
    ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-02a Đơn chưa hoàn tất đã bị trừ kho'; END
    EXEC dbo.AdvanceOrder @staff,@order,'Completed';
    SELECT @code=OrderCode FROM dbo.Orders WHERE OrderID=@order;
    IF EXISTS(SELECT 1 FROM dbo.InventoryTransactions WHERE IngredientID=@bk02 AND Kind='Export' AND ActorID IS NULL AND QuantityChange=-0.060 AND Note LIKE N'%'+@code+N'%')
       AND (SELECT Quantity FROM dbo.Ingredients WHERE IngredientID=@bk02)=@q1-0.060
       AND (SELECT Quantity FROM dbo.Ingredients WHERE IngredientID=@cf01)=@q2-0.010
    BEGIN SET @pass+=1; PRINT CONCAT(N'PASS TC-SQL-02b Đơn ',@code,N' hoàn tất → phiếu Xuất dùng tự động, tồn kho giảm đúng định mức'); END
    ELSE BEGIN SET @fail+=1; PRINT CONCAT(N'FAIL TC-SQL-02b Không thấy phiếu tự trừ kho đúng cho đơn ',@code); END

    -- TC-SQL-03: thiếu hàng → trừ về 0, ghi chú "thiếu", không chặn hoàn tất đơn
    UPDATE dbo.Ingredients SET Quantity=0.010 WHERE IngredientID=@bk02;
    SET @key=NEWID(); INSERT @r EXEC dbo.CreateOrder @ActorID=@staff,@BranchID=@branch,@Source='POS',@Fulfillment='Pickup',@PaymentMode='COD',@Items=@items,@RequestKey=@key;
    SELECT @order=ID FROM @r; DELETE @r;
    SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @staff,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
    EXEC dbo.SimulatePayment @staff,@payment,'Succeeded';
    EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
    SELECT @code=OrderCode FROM dbo.Orders WHERE OrderID=@order;
    IF (SELECT Status FROM dbo.Orders WHERE OrderID=@order)='Completed' AND (SELECT Quantity FROM dbo.Ingredients WHERE IngredientID=@bk02)=0
       AND EXISTS(SELECT 1 FROM dbo.InventoryTransactions WHERE IngredientID=@bk02 AND QuantityChange=-0.010 AND BalanceAfter=0 AND Note LIKE N'%thiếu 0,05%')
    BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-03 Thiếu nguyên liệu → đơn vẫn hoàn tất, tồn về 0, ghi chú "thiếu 0,05 – cần kiểm kê"'; END
    ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-03 Xử lý thiếu nguyên liệu chưa đúng'; END

    -- TC-SQL-04/05: giao hàng chưa thành công / giao lại → thông báo cho khách
    SET @items=CONCAT(N'[{"variantId":',@cake,N',"quantity":1}]');
    SET @key=NEWID(); INSERT @r EXEC dbo.CreateOrder @ActorID=@minhUser,@BranchID=@branch,@Source='App',@Fulfillment='Delivery',@PaymentMode='COD',@Items=@items,@RequestKey=@key,@CustomerID=@minh,@AddressID=@minhAddress;
    SELECT @order=ID FROM @r; DELETE @r;
    EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready';
    EXEC dbo.AdvanceOrder @ActorID=@staff,@OrderID=@order,@NextStatus='Delivering',@CourierName=N'Anh Bình',@CourierPhone='0908888888';
    EXEC dbo.UpdateDelivery @staff,@order,'Failed',N'Không liên lạc được khách';
    IF EXISTS(SELECT 1 FROM dbo.Notifications WHERE UserID=@minhUser AND OrderID=@order AND Title=N'Giao hàng chưa thành công' AND Body LIKE N'%Không liên lạc được khách%')
    BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-04 Giao chưa thành công → khách nhận thông báo kèm lý do'; END
    ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-04 Không có thông báo giao chưa thành công'; END
    EXEC dbo.UpdateDelivery @staff,@order,'Delivering';
    IF EXISTS(SELECT 1 FROM dbo.Notifications WHERE UserID=@minhUser AND OrderID=@order AND Title=N'Đang giao lại đơn hàng')
    BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-05 Giao lại → khách nhận thông báo "Đang giao lại đơn hàng"'; END
    ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-05 Không có thông báo giao lại'; END

    -- TC-SQL-06/07: hoàn tiền chưa thành công → thông báo; thành công → thông báo cũ vẫn giữ
    SET @key=NEWID(); INSERT @r EXEC dbo.CreateOrder @ActorID=@lanUser,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',@Items=@items,@RequestKey=@key,@CustomerID=@lan;
    SELECT @order=ID FROM @r; DELETE @r;
    SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @lanUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
    EXEC dbo.SimulatePayment @lanUser,@payment,'Succeeded';
    EXEC dbo.CancelOrder @lanUser,@order,N'Khách đổi ý (kiểm thử)';
    SELECT @refund=RefundID FROM dbo.Refunds WHERE OrderID=@order;
    EXEC dbo.SimulateRefund @manager,@refund,'Failed',N'Cổng thanh toán bận';
    IF EXISTS(SELECT 1 FROM dbo.Notifications WHERE UserID=@lanUser AND OrderID=@order AND Title=N'Hoàn tiền chưa thành công' AND Body LIKE N'%Cổng thanh toán bận%')
    BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-06 Hoàn tiền thất bại → khách nhận thông báo kèm lý do'; END
    ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-06 Không có thông báo hoàn tiền chưa thành công'; END
    EXEC dbo.SimulateRefund @manager,@refund,'Succeeded';
    IF EXISTS(SELECT 1 FROM dbo.Notifications WHERE UserID=@lanUser AND OrderID=@order AND Title=N'Hoàn tiền thành công')
    BEGIN SET @pass+=1; PRINT N'PASS TC-SQL-07 Hoàn tiền lại thành công → thông báo "Hoàn tiền thành công" như trước'; END
    ELSE BEGIN SET @fail+=1; PRINT N'FAIL TC-SQL-07 Thiếu thông báo hoàn tiền thành công'; END
END TRY
BEGIN CATCH
    SET @fail+=1; SET @msg=CONCAT(N'FAIL lỗi SQL ',ERROR_NUMBER(),N' (',ERROR_PROCEDURE(),N' dòng ',ERROR_LINE(),N'): ',ERROR_MESSAGE()); PRINT @msg;
END CATCH;
IF @@TRANCOUNT>0 ROLLBACK; -- trả database về nguyên trạng
PRINT CONCAT(N'=== KẾT QUẢ: ',@pass,N' PASS, ',@fail,N' FAIL (đã ROLLBACK, dữ liệu không thay đổi) ===');
