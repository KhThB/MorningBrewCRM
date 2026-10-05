SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- ActorID always comes from authenticated backend claims, never from an app request body.
CREATE OR ALTER PROCEDURE dbo.IssueVoucher
 @ActorID int,@CustomerID int,@PromotionID int,@RequestKey uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @id int,@cost int,@limit int,@per int,@scope varchar(10),@area int,@branch int,@now datetime2(3)=SYSUTCDATETIME();
 SELECT @scope=Scope,@area=AreaID,@branch=BranchID,@cost=PointsCost,@limit=TotalLimit,@per=PerCustomerLimit FROM dbo.Promotions WITH(UPDLOCK,HOLDLOCK) WHERE PromotionID=@PromotionID;
 IF @scope IS NULL THROW 51100,N'Promotion not found.',1;
 IF dbo.IsCustomer(@ActorID,@CustomerID)=0 AND (dbo.CanScope(@ActorID,@scope,@area,@branch,'Promotions.Manage')=0 OR dbo.CustomerInScope(@CustomerID,@scope,@area,@branch)=0) THROW 51103,N'Voucher recipient is outside actor scope.',1;
 SELECT @id=CustomerPromotionID FROM dbo.CustomerPromotions WHERE RequestKey=@RequestKey AND CustomerID=@CustomerID AND PromotionID=@PromotionID;
 IF @id IS NOT NULL BEGIN COMMIT; SELECT @id AS CustomerPromotionID; RETURN; END;
 IF NOT EXISTS(SELECT 1 FROM dbo.Promotions WHERE PromotionID=@PromotionID AND IsActive=1 AND StartsAt<=@now AND EndsAt>@now) THROW 51100,N'Promotion is inactive or outside its validity period.',1;
 IF dbo.IsCustomer(@ActorID,@CustomerID)=1 AND @cost IS NULL THROW 51103,N'Gift vouchers must be issued by management.',1;
 IF (SELECT COUNT(*) FROM dbo.CustomerPromotions WHERE PromotionID=@PromotionID)>=@limit OR (SELECT COUNT(*) FROM dbo.CustomerPromotions WHERE PromotionID=@PromotionID AND CustomerID=@CustomerID)>=@per THROW 51100,N'Voucher issuance limit reached.',1;
 DECLARE @balance int;
 SELECT @balance=LoyaltyPoints FROM dbo.Customers WITH(UPDLOCK,HOLDLOCK) WHERE CustomerID=@CustomerID;
 IF @balance IS NULL THROW 51100,N'Customer not found.',1;
 IF COALESCE(@cost,0)>0 AND @balance<@cost THROW 51100,N'Insufficient points.',1;
 INSERT dbo.CustomerPromotions(CustomerID,PromotionID,Code,RequestKey,ExpiresAt,PointsSpent,DiscountType,DiscountValue,MaxDiscount,MinSubtotal)
 SELECT @CustomerID,PromotionID,CONCAT('MB-',REPLACE(CONVERT(varchar(36),NEWID()),'-','')),@RequestKey,EndsAt,COALESCE(PointsCost,0),DiscountType,DiscountValue,MaxDiscount,MinSubtotal FROM dbo.Promotions WHERE PromotionID=@PromotionID;
 SET @id=SCOPE_IDENTITY();
 IF COALESCE(@cost,0)>0 BEGIN
  UPDATE dbo.Customers SET LoyaltyPoints=LoyaltyPoints-@cost WHERE CustomerID=@CustomerID;
  INSERT dbo.LoyaltyTransactions(CustomerID,CustomerPromotionID,Kind,Points,RequestKey,Note) VALUES(@CustomerID,@id,'Redeem',-@cost,@RequestKey,N'Đổi điểm lấy voucher');
 END;
 INSERT dbo.Notifications(UserID,Title,Body,CustomerPromotionID) SELECT UserID,N'Voucher mới',N'Voucher đã được thêm vào tài khoản.',@id FROM dbo.Customers WHERE CustomerID=@CustomerID;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@ActorID,@branch,'Voucher.Issue','CustomerPromotions',@id);
 COMMIT;
 SELECT @id AS CustomerPromotionID;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Items JSON: [{"variantId":1,"quantity":2,"note":"...","optionIds":[1,4]}]
CREATE OR ALTER PROCEDURE dbo.CreateOrder
 @ActorID int,@BranchID int,@Source varchar(5),@Fulfillment varchar(10),@PaymentMode varchar(10),
 @Items nvarchar(max),@RequestKey uniqueidentifier,@CustomerID int=NULL,@CustomerPromotionID int=NULL,
 @ContactName nvarchar(120)=NULL,@ContactPhone varchar(20)=NULL,@AddressID int=NULL,
 @DeliveryAddress nvarchar(500)=NULL,@DeliveryNote nvarchar(300)=NULL,@Note nvarchar(500)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 IF @Source NOT IN ('App','POS') OR @Fulfillment NOT IN ('Pickup','Delivery') OR @PaymentMode NOT IN ('Prepaid','COD') THROW 51100,N'Invalid order mode.',1;
 IF (@Source='App' AND dbo.IsCustomer(@ActorID,@CustomerID)=0) OR (@Source='POS' AND dbo.CanBranch(@ActorID,@BranchID,'Orders.Create')=0) THROW 51103,N'Cannot create this order.',1;
 DECLARE @payload nvarchar(max)=(SELECT @ActorID ActorID,@BranchID BranchID,@Source Source,@Fulfillment Fulfillment,@PaymentMode PaymentMode,@Items Items,@CustomerID CustomerID,@CustomerPromotionID CustomerPromotionID,@ContactName ContactName,@ContactPhone ContactPhone,@AddressID AddressID,@DeliveryAddress DeliveryAddress,@DeliveryNote DeliveryNote,@Note Note FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
 DECLARE @requestHash binary(32)=HASHBYTES('SHA2_256',@payload);
 DECLARE @id int,@now datetime2(3)=SYSUTCDATETIME(),@fee decimal(18,2),@area int,@policy int,@rate decimal(18,2),@subtotal decimal(18,2),@discount decimal(18,2)=0;
 -- Serialize identical request keys even if a caller retries while first submission is running.
 DECLARE @lockResult int,@resource nvarchar(255)=CONCAT('CreateOrder:',CONVERT(varchar(36),@RequestKey));
 EXEC @lockResult=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lockResult<0 THROW 51109,N'Order request is busy; retry.',1;
 SELECT @id=OrderID FROM dbo.Orders WHERE RequestKey=@RequestKey;
 IF @id IS NOT NULL BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderID=@id AND RequestHash=@requestHash AND CreatedBy=@ActorID AND BranchID=@BranchID AND Source=@Source AND Fulfillment=@Fulfillment AND PaymentMode=@PaymentMode AND ISNULL(CustomerID,0)=ISNULL(@CustomerID,0) AND ISNULL(CustomerPromotionID,0)=ISNULL(@CustomerPromotionID,0)) THROW 51109,N'Request key belongs to a different order.',1;
  COMMIT; SELECT @id AS OrderID; RETURN;
 END;
 DECLARE @localtime time=CONVERT(time,DATEADD(hour,7,@now));
 SELECT @fee=CASE WHEN @Fulfillment='Delivery' THEN DeliveryFee ELSE 0 END,@area=AreaID FROM dbo.Branches WITH(HOLDLOCK)
 WHERE BranchID=@BranchID AND IsActive=1 AND AcceptingOrders=1 AND @localtime>=OpeningTime AND @localtime<ClosingTime
 AND ((@Fulfillment='Pickup' AND PickupEnabled=1) OR (@Fulfillment='Delivery' AND DeliveryEnabled=1));
 IF @area IS NULL THROW 51100,N'Branch is closed or does not accept this fulfillment type.',1;
 IF @CustomerID IS NOT NULL BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE c.CustomerID=@CustomerID AND u.IsActive=1) THROW 51100,N'Customer is inactive or missing.',1;
  SELECT @ContactName=COALESCE(@ContactName,u.FullName),@ContactPhone=COALESCE(@ContactPhone,u.Phone) FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE c.CustomerID=@CustomerID;
 END;
 SET @ContactName=COALESCE(NULLIF(LTRIM(RTRIM(@ContactName)),''),N'Khách vãng lai');
 IF @AddressID IS NOT NULL BEGIN
  IF @Fulfillment<>'Delivery' OR NOT EXISTS(SELECT 1 FROM dbo.CustomerAddresses WHERE AddressID=@AddressID AND CustomerID=@CustomerID AND IsActive=1) THROW 51103,N'Address does not belong to this customer.',1;
  SELECT @ContactName=RecipientName,@ContactPhone=Phone,@DeliveryAddress=AddressText,@DeliveryNote=Note FROM dbo.CustomerAddresses WHERE AddressID=@AddressID;
 END;
 IF @Fulfillment='Delivery' AND (NULLIF(LTRIM(RTRIM(@DeliveryAddress)),'') IS NULL OR NULLIF(@ContactPhone,'') IS NULL) THROW 51100,N'Delivery needs recipient phone and address.',1;
 IF ISJSON(@Items)<>1 OR LEFT(LTRIM(@Items),1)<>'[' THROW 51100,N'Items must be a JSON array.',1;
 DECLARE @lines TABLE(ItemIndex int PRIMARY KEY,VariantID int,Quantity int,Note nvarchar(300),Options nvarchar(max),ProductID int,ProductName nvarchar(150),VariantName nvarchar(50),UnitPrice decimal(18,2),ModifierTotal decimal(18,2));
 INSERT @lines(ItemIndex,VariantID,Quantity,Note,Options)
 SELECT CONVERT(int,j.[key]),x.variantId,x.quantity,x.note,COALESCE(x.optionIds,N'[]') FROM OPENJSON(@Items) j CROSS APPLY OPENJSON(j.value) WITH(variantId int,quantity int,note nvarchar(300),optionIds nvarchar(max) AS JSON) x;
 IF NOT EXISTS(SELECT 1 FROM @lines) OR (SELECT COUNT(*) FROM @lines)>100 OR EXISTS(SELECT 1 FROM @lines WHERE VariantID IS NULL OR Quantity IS NULL OR Quantity NOT BETWEEN 1 AND 100 OR LEFT(LTRIM(Options),1)<>'[') THROW 51100,N'Invalid order lines.',1;
 UPDATE l SET ProductID=p.ProductID,ProductName=p.Name,VariantName=v.Name,UnitPrice=COALESCE(bv.SellingPrice,v.BasePrice),ModifierTotal=0 FROM @lines l
 JOIN dbo.ProductVariants v WITH(HOLDLOCK) ON v.VariantID=l.VariantID AND v.IsActive=1 JOIN dbo.Products p WITH(HOLDLOCK) ON p.ProductID=v.ProductID AND p.IsActive=1
 JOIN dbo.Categories cat ON cat.CategoryID=p.CategoryID AND cat.IsActive=1 JOIN dbo.BranchProducts bp WITH(HOLDLOCK) ON bp.ProductID=p.ProductID AND bp.BranchID=@BranchID AND bp.IsAvailable=1
 LEFT JOIN dbo.BranchProductVariants bv WITH(HOLDLOCK) ON bv.VariantID=v.VariantID AND bv.BranchID=@BranchID WHERE COALESCE(bv.IsAvailable,1)=1;
 IF EXISTS(SELECT 1 FROM @lines WHERE ProductID IS NULL) THROW 51100,N'An item or size is unavailable at this branch.',1;
 DECLARE @opts TABLE(ItemIndex int,OptionID int,GroupID int,GroupName nvarchar(100),OptionName nvarchar(100),Price decimal(18,2),PRIMARY KEY(ItemIndex,OptionID));
 IF EXISTS(SELECT 1 FROM @lines l CROSS APPLY OPENJSON(l.Options) o WHERE o.type<>2 OR TRY_CONVERT(int,o.value) IS NULL) THROW 51100,N'Option IDs must be integers.',1;
 INSERT @opts(ItemIndex,OptionID) SELECT l.ItemIndex,CONVERT(int,o.value) FROM @lines l CROSS APPLY OPENJSON(l.Options) o;
 UPDATE o SET GroupID=g.ModifierGroupID,GroupName=g.Name,OptionName=mo.Name,Price=COALESCE(bo.ExtraPrice,mo.ExtraPrice) FROM @opts o JOIN @lines l ON l.ItemIndex=o.ItemIndex
 JOIN dbo.ModifierOptions mo WITH(HOLDLOCK) ON mo.ModifierOptionID=o.OptionID AND mo.IsActive=1 JOIN dbo.ModifierGroups g ON g.ModifierGroupID=mo.ModifierGroupID AND g.IsActive=1
 JOIN dbo.ProductModifierGroups pg ON pg.ProductID=l.ProductID AND pg.ModifierGroupID=g.ModifierGroupID
 LEFT JOIN dbo.BranchModifierOptions bo WITH(HOLDLOCK) ON bo.BranchID=@BranchID AND bo.ModifierOptionID=mo.ModifierOptionID WHERE COALESCE(bo.IsAvailable,1)=1;
 IF EXISTS(SELECT 1 FROM @opts WHERE GroupID IS NULL) THROW 51100,N'An option is unavailable or does not belong to this product.',1;
 IF EXISTS(SELECT 1 FROM @lines l JOIN dbo.ProductModifierGroups pg ON pg.ProductID=l.ProductID JOIN dbo.ModifierGroups g ON g.ModifierGroupID=pg.ModifierGroupID AND g.IsActive=1
 CROSS APPLY(SELECT COUNT(*) AS n FROM @opts o WHERE o.ItemIndex=l.ItemIndex AND o.GroupID=pg.ModifierGroupID) x WHERE x.n<pg.MinSelections OR x.n>pg.MaxSelections) THROW 51100,N'Option selection count is invalid.',1;
 UPDATE l SET ModifierTotal=COALESCE((SELECT SUM(Price) FROM @opts o WHERE o.ItemIndex=l.ItemIndex),0) FROM @lines l;
 SELECT @subtotal=SUM((UnitPrice+ModifierTotal)*Quantity) FROM @lines;
 IF @CustomerPromotionID IS NOT NULL BEGIN
  DECLARE @dtype varchar(10),@dvalue decimal(18,2),@cap decimal(18,2),@minimum decimal(18,2);
  SELECT @dtype=cp.DiscountType,@dvalue=cp.DiscountValue,@cap=cp.MaxDiscount,@minimum=cp.MinSubtotal
  FROM dbo.CustomerPromotions cp WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Promotions p ON p.PromotionID=cp.PromotionID
  WHERE cp.CustomerPromotionID=@CustomerPromotionID AND cp.CustomerID=@CustomerID AND cp.Status='Available' AND cp.ExpiresAt>@now
  AND (p.Scope='Chain' OR (p.Scope='Area' AND p.AreaID=@area) OR (p.Scope='Branch' AND p.BranchID=@BranchID));
  IF @dtype IS NULL OR @subtotal<@minimum THROW 51100,N'Voucher is invalid, reserved, expired or minimum is not met.',1;
  SET @discount=CASE WHEN @dtype='Amount' THEN @dvalue ELSE FLOOR(@subtotal*@dvalue/100) END;
  IF @cap IS NOT NULL AND @discount>@cap SET @discount=@cap;
  IF @discount>@subtotal SET @discount=@subtotal;
 END;
 SELECT @policy=PolicyID,@rate=AmountPerPoint FROM dbo.LoyaltyPolicies WITH(HOLDLOCK) WHERE EffectiveFrom<=@now AND (EffectiveTo IS NULL OR EffectiveTo>@now);
 IF @policy IS NULL THROW 51100,N'No active loyalty policy.',1;
 DECLARE @status varchar(20)=CASE WHEN @PaymentMode='Prepaid' THEN 'AwaitingPayment' ELSE 'Pending' END;
 INSERT dbo.Orders(OrderCode,RequestKey,RequestHash,BranchID,CustomerID,CreatedBy,Source,Fulfillment,PaymentMode,Status,ContactName,ContactPhone,Note,Subtotal,DiscountAmount,DeliveryFee,TotalAmount,CustomerPromotionID,PolicyID,AmountPerPoint,PaymentDueAt)
 VALUES(CONCAT('MB-',REPLACE(CONVERT(varchar(36),NEWID()),'-','')),@RequestKey,@requestHash,@BranchID,@CustomerID,@ActorID,@Source,@Fulfillment,@PaymentMode,@status,@ContactName,@ContactPhone,@Note,@subtotal,@discount,@fee,@subtotal-@discount+@fee,@CustomerPromotionID,@policy,@rate,CASE WHEN @PaymentMode='Prepaid' THEN DATEADD(minute,15,@now) END);
 SET @id=SCOPE_IDENTITY();
 IF @CustomerPromotionID IS NOT NULL UPDATE dbo.CustomerPromotions SET Status='Reserved',ReservedOrderID=@id WHERE CustomerPromotionID=@CustomerPromotionID;
 DECLARE @line int,@detail int;
 DECLARE items CURSOR LOCAL FAST_FORWARD FOR SELECT ItemIndex FROM @lines;
 OPEN items; FETCH NEXT FROM items INTO @line;
 WHILE @@FETCH_STATUS=0 BEGIN
  INSERT dbo.OrderDetails(OrderID,ProductID,VariantID,ProductName,VariantName,Quantity,UnitPrice,ModifierTotal,LineTotal,Note) SELECT @id,ProductID,VariantID,ProductName,VariantName,Quantity,UnitPrice,ModifierTotal,(UnitPrice+ModifierTotal)*Quantity,Note FROM @lines WHERE ItemIndex=@line;
  SET @detail=SCOPE_IDENTITY();
  INSERT dbo.OrderItemModifiers(OrderDetailID,ModifierOptionID,GroupName,OptionName,ExtraPrice) SELECT @detail,OptionID,GroupName,OptionName,Price FROM @opts WHERE ItemIndex=@line;
  FETCH NEXT FROM items INTO @line;
 END;
 CLOSE items; DEALLOCATE items;
 IF @Fulfillment='Delivery' INSERT dbo.Deliveries(OrderID,RecipientName,Phone,AddressText,Note) VALUES(@id,@ContactName,@ContactPhone,@DeliveryAddress,@DeliveryNote);
 INSERT dbo.OrderStatusHistory(OrderID,ToStatus,ChangedBy,Reason) VALUES(@id,@status,@ActorID,N'Tạo đơn');
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@ActorID,@BranchID,'Order.Create','Orders',@id);
 COMMIT;
 SELECT @id AS OrderID;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.StartPayment @ActorID int,@OrderID int,@RequestKey uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@customer int,@status varchar(20),@mode varchar(10),@amount decimal(18,2),@id int,@due datetime2(3);
 SELECT @branch=BranchID,@customer=CustomerID,@status=Status,@mode=PaymentMode,@amount=TotalAmount,@due=PaymentDueAt FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@OrderID;
 IF @branch IS NULL THROW 51100,N'Order not found.',1;
 IF dbo.IsCustomer(@ActorID,@customer)=0 AND dbo.CanBranch(@ActorID,@branch,'Payments.Manage')=0 THROW 51103,N'Cannot pay this order.',1;
 IF @mode='COD' AND dbo.CanBranch(@ActorID,@branch,'Payments.Manage')=0 THROW 51103,N'Only POS can confirm collected COD.',1;
 SELECT @id=PaymentID FROM dbo.PaymentAttempts WHERE RequestKey=@RequestKey AND OrderID=@OrderID;
 IF @id IS NOT NULL BEGIN COMMIT; SELECT @id AS PaymentID; RETURN; END;
 IF @status IN ('Cancelled','Completed') OR (@mode='Prepaid' AND (@status<>'AwaitingPayment' OR @due<=SYSUTCDATETIME())) THROW 51100,N'Order is not payable.',1;
 IF EXISTS(SELECT 1 FROM dbo.PaymentAttempts WHERE OrderID=@OrderID AND Status IN ('Pending','Succeeded')) THROW 51109,N'Payment already pending or succeeded.',1;
 INSERT dbo.PaymentAttempts(OrderID,RequestKey,Method,Provider,Amount) VALUES(@OrderID,@RequestKey,CASE WHEN @mode='COD' THEN 'Cash' ELSE 'Simulator' END,CASE WHEN @mode='COD' THEN 'Manual' ELSE 'Simulator' END,@amount);
 SET @id=SCOPE_IDENTITY();
 COMMIT; SELECT @id AS PaymentID;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Development simulator only. Production gateway callbacks must authenticate provider signatures in the backend.
CREATE OR ALTER PROCEDURE dbo.SimulatePayment @ActorID int,@PaymentID int,@Outcome varchar(12),@Reason nvarchar(500)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 IF @Outcome NOT IN ('Pending','Succeeded','Failed') THROW 51100,N'Invalid payment outcome.',1;
 DECLARE @order int,@branch int,@customer int,@mode varchar(10),@old varchar(12),@status varchar(20),@due datetime2(3);
 SELECT @order=OrderID FROM dbo.PaymentAttempts WHERE PaymentID=@PaymentID;
 SELECT @branch=BranchID,@customer=CustomerID,@mode=PaymentMode,@status=Status,@due=PaymentDueAt FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@order;
 IF @branch IS NULL THROW 51100,N'Payment not found.',1;
 IF (@mode='COD' AND dbo.CanBranch(@ActorID,@branch,'Payments.Manage')=0) OR (@mode='Prepaid' AND dbo.IsCustomer(@ActorID,@customer)=0 AND dbo.CanBranch(@ActorID,@branch,'Payments.Manage')=0) THROW 51103,N'Cannot settle this payment.',1;
 SELECT @old=Status FROM dbo.PaymentAttempts WITH(UPDLOCK,HOLDLOCK) WHERE PaymentID=@PaymentID;
 IF @old=@Outcome BEGIN COMMIT; RETURN; END;
 IF @old<>'Pending' THROW 51109,N'Terminal payment result cannot change.',1;
 IF @Outcome='Succeeded' AND (@status IN ('Cancelled','Completed') OR (@mode='Prepaid' AND @due<=SYSUTCDATETIME())) THROW 51100,N'Order is cancelled or payment timed out; simulator cannot collect funds.',1;
 IF @Outcome='Pending' BEGIN COMMIT; RETURN; END;
 UPDATE dbo.PaymentAttempts SET Status=@Outcome,ProviderReference=CONCAT(CASE WHEN @mode='COD' THEN 'CASH-' ELSE 'SIM-' END,@PaymentID),SettledAt=SYSUTCDATETIME(),FailureReason=CASE WHEN @Outcome='Failed' THEN @Reason END WHERE PaymentID=@PaymentID;
 IF @Outcome='Succeeded' AND @status='AwaitingPayment' BEGIN
  UPDATE dbo.Orders SET Status='Pending' WHERE OrderID=@order;
  INSERT dbo.OrderStatusHistory(OrderID,FromStatus,ToStatus,ChangedBy,Reason) VALUES(@order,@status,'Pending',@ActorID,N'Thanh toán thành công');
 END;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Payment.Simulate','PaymentAttempts',@PaymentID,@Outcome);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.AdvanceOrder
 @ActorID int,@OrderID int,@NextStatus varchar(20),@Reason nvarchar(500)=NULL,@CourierName nvarchar(120)=NULL,@CourierPhone varchar(20)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@old varchar(20),@fulfill varchar(10),@customer int,@voucher int,@points int;
 SELECT @branch=BranchID,@old=Status,@fulfill=Fulfillment,@customer=CustomerID,@voucher=CustomerPromotionID,@points=FLOOR((Subtotal-DiscountAmount)/AmountPerPoint) FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@OrderID;
 IF @branch IS NULL THROW 51100,N'Order not found.',1;
 IF dbo.CanBranch(@ActorID,@branch,'Orders.Manage')=0 THROW 51103,N'Cannot manage this branch order.',1;
 IF @old=@NextStatus BEGIN COMMIT; RETURN; END;
 IF NOT ((@old='Pending' AND @NextStatus='Confirmed') OR (@old='Confirmed' AND @NextStatus='Preparing') OR (@old='Preparing' AND @NextStatus='Ready') OR (@old='Ready' AND @NextStatus='Delivering' AND @fulfill='Delivery') OR (@NextStatus='Completed' AND ((@old='Ready' AND @fulfill='Pickup') OR (@old='Delivering' AND @fulfill='Delivery')))) THROW 51100,N'Invalid order transition.',1;
 IF @NextStatus='Delivering' BEGIN
  IF NULLIF(@CourierName,'') IS NULL OR NULLIF(@CourierPhone,'') IS NULL THROW 51100,N'Courier name and phone are required.',1;
  UPDATE dbo.Deliveries SET Status='Delivering',CourierName=@CourierName,CourierPhone=@CourierPhone,AttemptCount=AttemptCount+1,DispatchedAt=SYSUTCDATETIME(),FailureReason=NULL WHERE OrderID=@OrderID;
 END;
 IF @NextStatus='Completed' BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.PaymentAttempts WHERE OrderID=@OrderID AND Status='Succeeded') OR EXISTS(SELECT 1 FROM dbo.Refunds WHERE OrderID=@OrderID) THROW 51100,N'Order must be fully paid and not under refund.',1;
  IF @fulfill='Delivery' AND NOT EXISTS(SELECT 1 FROM dbo.Deliveries WHERE OrderID=@OrderID AND Status='Delivering') THROW 51100,N'Delivery has not resumed after failure.',1;
  IF @voucher IS NOT NULL BEGIN
   UPDATE dbo.CustomerPromotions SET Status='Used',UsedOrderID=@OrderID,ReservedOrderID=NULL,UsedAt=SYSUTCDATETIME() WHERE CustomerPromotionID=@voucher AND Status='Reserved' AND ReservedOrderID=@OrderID;
   IF @@ROWCOUNT<>1 THROW 51109,N'Voucher reservation was lost.',1;
  END;
  IF @customer IS NOT NULL AND @points>0 BEGIN
   UPDATE dbo.Customers WITH(UPDLOCK) SET LoyaltyPoints=LoyaltyPoints+@points WHERE CustomerID=@customer;
   INSERT dbo.LoyaltyTransactions(CustomerID,OrderID,Kind,Points,RequestKey,Note) VALUES(@customer,@OrderID,'Earn',@points,NEWID(),N'Tích điểm đơn hoàn tất');
  END;
  UPDATE dbo.Orders SET CompletedAt=SYSUTCDATETIME() WHERE OrderID=@OrderID;
  IF @fulfill='Delivery' UPDATE dbo.Deliveries SET Status='Delivered',DeliveredAt=SYSUTCDATETIME() WHERE OrderID=@OrderID;
 END;
 UPDATE dbo.Orders SET Status=@NextStatus WHERE OrderID=@OrderID;
 INSERT dbo.OrderStatusHistory(OrderID,FromStatus,ToStatus,ChangedBy,Reason) VALUES(@OrderID,@old,@NextStatus,@ActorID,@Reason);
 INSERT dbo.Notifications(UserID,Title,Body,OrderID) SELECT UserID,N'Cập nhật đơn hàng',CONCAT(N'Trạng thái mới: ',@NextStatus),@OrderID FROM dbo.Customers WHERE CustomerID=@customer;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Order.Advance','Orders',@OrderID,@NextStatus);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.UpdateDelivery @ActorID int,@OrderID int,@Status varchar(12),@Reason nvarchar(500)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@orderStatus varchar(20),@old varchar(12);
 SELECT @branch=BranchID,@orderStatus=Status FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@OrderID AND Fulfillment='Delivery';
 IF @branch IS NULL OR dbo.CanBranch(@ActorID,@branch,'Orders.Manage')=0 THROW 51103,N'Cannot update this delivery.',1;
 IF @orderStatus<>'Delivering' THROW 51100,N'Order is not in delivery phase.',1;
 SELECT @old=Status FROM dbo.Deliveries WHERE OrderID=@OrderID;
 IF @Status NOT IN ('Failed','Delivering') THROW 51100,N'Use AdvanceOrder for handover and completion.',1;
 IF @old=@Status BEGIN COMMIT; RETURN; END;
 IF NOT ((@old='Delivering' AND @Status='Failed') OR (@old='Failed' AND @Status='Delivering')) THROW 51100,N'Invalid delivery transition.',1;
 IF @Status='Failed' AND NULLIF(@Reason,'') IS NULL THROW 51100,N'Failure reason is required.',1;
 UPDATE dbo.Deliveries SET Status=@Status,FailureReason=CASE WHEN @Status='Failed' THEN @Reason END,AttemptCount=AttemptCount+CASE WHEN @Status='Delivering' THEN 1 ELSE 0 END,DispatchedAt=CASE WHEN @Status='Delivering' THEN SYSUTCDATETIME() ELSE DispatchedAt END WHERE OrderID=@OrderID;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Delivery.Update','Deliveries',@OrderID,CONCAT(@Status,': ',@Reason));
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.CancelOrder @ActorID int,@OrderID int,@Reason nvarchar(500)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@customer int,@old varchar(20),@voucher int,@payment int,@amount decimal(18,2);
 SELECT @branch=BranchID,@customer=CustomerID,@old=Status,@voucher=CustomerPromotionID FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@OrderID;
 IF @branch IS NULL THROW 51100,N'Order not found.',1;
 IF dbo.CanBranch(@ActorID,@branch,'Orders.Cancel')=0 AND NOT(dbo.IsCustomer(@ActorID,@customer)=1 AND @old IN ('AwaitingPayment','Pending','Cancelled')) THROW 51103,N'Cancellation requires management after confirmation.',1;
 IF @old='Cancelled' BEGIN COMMIT; RETURN; END;
 IF @old='Completed' THROW 51100,N'Completed orders use RequestRefund; operational history remains completed.',1;
 IF NULLIF(LTRIM(RTRIM(@Reason)),'') IS NULL THROW 51100,N'Cancellation reason is required.',1;
 UPDATE dbo.Orders SET Status='Cancelled',CancelledAt=SYSUTCDATETIME() WHERE OrderID=@OrderID;
 UPDATE dbo.CustomerPromotions SET Status='Available',ReservedOrderID=NULL WHERE CustomerPromotionID=@voucher AND Status='Reserved' AND ReservedOrderID=@OrderID;
 UPDATE dbo.Deliveries SET Status='Cancelled' WHERE OrderID=@OrderID;
 UPDATE dbo.PaymentAttempts SET Status='Failed',FailureReason=N'Đơn bị hủy',SettledAt=SYSUTCDATETIME() WHERE OrderID=@OrderID AND Status='Pending';
 SELECT @payment=PaymentID,@amount=Amount FROM dbo.PaymentAttempts WHERE OrderID=@OrderID AND Status='Succeeded';
 IF @payment IS NOT NULL INSERT dbo.Refunds(PaymentID,OrderID,RequestKey,Amount,Reason,RequestedBy) VALUES(@payment,@OrderID,NEWID(),@amount,@Reason,@ActorID);
 INSERT dbo.OrderStatusHistory(OrderID,FromStatus,ToStatus,ChangedBy,Reason) VALUES(@OrderID,@old,'Cancelled',@ActorID,@Reason);
 INSERT dbo.Notifications(UserID,Title,Body,OrderID) SELECT UserID,N'Đơn hàng đã hủy',@Reason,@OrderID FROM dbo.Customers WHERE CustomerID=@customer;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Order.Cancel','Orders',@OrderID,@Reason);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.RequestRefund @ActorID int,@OrderID int,@Reason nvarchar(500),@RequestKey uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@status varchar(20),@payment int,@amount decimal(18,2),@id int;
 SELECT @branch=BranchID,@status=Status FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@OrderID;
 IF @branch IS NULL OR dbo.CanBranch(@ActorID,@branch,'Refunds.Manage')=0 THROW 51103,N'Cannot refund this branch order.',1;
 IF @status NOT IN ('Completed','Cancelled') THROW 51100,N'Cancel the active order before refunding.',1;
 SELECT @id=RefundID FROM dbo.Refunds WHERE OrderID=@OrderID;
 IF @id IS NOT NULL BEGIN COMMIT; SELECT @id AS RefundID; RETURN; END;
 IF NULLIF(@Reason,'') IS NULL THROW 51100,N'Refund reason is required.',1;
 SELECT @payment=PaymentID,@amount=Amount FROM dbo.PaymentAttempts WHERE OrderID=@OrderID AND Status='Succeeded';
 IF @payment IS NULL THROW 51100,N'No successful payment to refund.',1;
 INSERT dbo.Refunds(PaymentID,OrderID,RequestKey,Amount,Reason,RequestedBy) VALUES(@payment,@OrderID,@RequestKey,@amount,@Reason,@ActorID);
 SET @id=SCOPE_IDENTITY();
 COMMIT; SELECT @id AS RefundID;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
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
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Refund.Simulate','Refunds',@RefundID,@Outcome);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Backend scheduler calls every minute. Owner/service actor with chain scope is required.
CREATE OR ALTER PROCEDURE dbo.ExpireUnpaidOrders @ActorID int
AS
BEGIN
 SET NOCOUNT ON;
 IF dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Orders.Cancel')=0 THROW 51103,N'Chain cancellation permission required.',1;
 DECLARE @id int;
 DECLARE expired CURSOR LOCAL FAST_FORWARD FOR SELECT OrderID FROM dbo.Orders WHERE Status='AwaitingPayment' AND PaymentDueAt<=SYSUTCDATETIME();
 OPEN expired; FETCH NEXT FROM expired INTO @id;
 WHILE @@FETCH_STATUS=0 BEGIN
  -- Recheck under order lock so a just-settled order is not cancelled by a stale scan.
  BEGIN TRAN;
  IF EXISTS(SELECT 1 FROM dbo.Orders WITH(UPDLOCK,HOLDLOCK) WHERE OrderID=@id AND Status='AwaitingPayment' AND PaymentDueAt<=SYSUTCDATETIME()) EXEC dbo.CancelOrder @ActorID,@id,N'Hết thời gian thanh toán';
  COMMIT;
  FETCH NEXT FROM expired INTO @id;
 END;
 CLOSE expired; DEALLOCATE expired;
END;
GO
