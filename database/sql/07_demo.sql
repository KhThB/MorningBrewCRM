-- Dữ liệu giao dịch demo cho phạm vi CRM (1 cửa hàng, 30 khách).
-- Mọi ID được tra theo email / thứ tự hiển thị, không gán cứng.
-- [GOP-01] Chạy trên thực đơn ở trạng thái cuối (06_seed.sql): đồ uống bắt buộc chọn Nhiệt độ Đá / Nóng nên đơn mẫu
--          gửi kèm tùy chọn này. Trigger của 02_rules.sql tự sinh mã đơn ngắn và tự trừ kho khi đơn hoàn tất.
-- [DEMO-01] Thêm mục 0 (lịch sử 12 tháng: 68 đơn, nhập kho định kỳ, voucher quà tặng) và mục 4 (hủy hàng, kiểm kê);
--          mục 3 bỏ đơn hôm nay của nhóm "lâu không quay lại".
SET NOCOUNT ON;
DECLARE @r TABLE(ID int);
DECLARE @voucher int,@order int,@payment int,@refund int,@feedback int,@key uniqueidentifier=NEWID();
DECLARE @owner int=(SELECT UserID FROM dbo.Users WHERE Email='owner@morningbrew.test'),
        @manager int=(SELECT UserID FROM dbo.Users WHERE Email='manager.1@morningbrew.test'),
        @staff int=(SELECT UserID FROM dbo.Users WHERE Email='staff.1@morningbrew.test'),
        @lanUser int=(SELECT UserID FROM dbo.Users WHERE Email='lan@morningbrew.test'),
        @minhUser int=(SELECT UserID FROM dbo.Users WHERE Email='minh@morningbrew.test');
DECLARE @lan int=(SELECT CustomerID FROM dbo.Customers WHERE UserID=@lanUser),
        @minh int=(SELECT CustomerID FROM dbo.Customers WHERE UserID=@minhUser),
        @branch int=(SELECT BranchID FROM dbo.Branches WHERE Code='MB01'),
        @minhAddress int;
DECLARE @iceGroup int=(SELECT ModifierGroupID FROM dbo.ModifierGroups WHERE Kind='Ice' AND IsActive=1);
DECLARE @da int=(SELECT ModifierOptionID FROM dbo.ModifierOptions WHERE ModifierGroupID=@iceGroup AND Name=N'Đá'),
        @nong int=(SELECT ModifierOptionID FROM dbo.ModifierOptions WHERE ModifierGroupID=@iceGroup AND Name=N'Nóng');
DECLARE @lines nvarchar(200);
SELECT @minhAddress=AddressID FROM dbo.CustomerAddresses WHERE CustomerID=@minh AND IsDefault=1;

-- ===== 0. [DEMO-01] Lịch sử mua hàng 12 tháng (chạy trước các luồng mẫu để mã đơn và phiếu kho theo đúng thời gian) =====
-- Mục đích: báo cáo theo tháng, phân nhóm khách và hạng thành viên có dữ liệu ngay sau khi cài.
--   Phân nhóm dự kiến (CustomerStats): Chưa mua 4 (2, 10, 20, 30) · Mới 7 (1, 5, 15, 18, 23, 26, 28)
--   · Đang hoạt động 7 (6, 8, 12, 13, 17, 21, 25) · Trung thành 6 (3, 7, 11, 16, 22, 27) · Lâu không quay lại 6 (4, 9, 14, 19, 24, 29).
--   Hạng 12 tháng (CustomerTiers): Kim cương 3 · Vàng 7, 12 · Bạc 4, 6, 11, 16 (đơn đặt cho văn phòng 25–30 ly) · còn lại Thành viên.
-- Cách làm: mỗi đơn chạy đúng luồng thủ tục (CreateOrder → StartPayment → SimulatePayment → AdvanceOrder) để tiền, điểm,
--   kho, thông báo khớp nghiệp vụ; sau đó dời thời gian các dòng vừa sinh về ngày trong quá khứ. Sổ điểm và phiếu kho là
--   append-only nên tạm tắt 2 trigger chặn sửa trong lúc dời ngày rồi bật lại ngay. Mỗi kỳ 30 ngày có 1 phiếu nhập kho
--   (đủ cho đơn của kỳ và đưa tồn về mức ban đầu) ghi trước các đơn của kỳ đó.
-- Số khách theo CustomerID của bản cài mới (Lan = 1, Minh = 2, khach01–28 = 3–30), giống cách phân bố ở mục 3.
-- Cột: khách, số ngày trước hôm nay, VariantID, số lượng, voucher (G = Quà tặng 10% do quản lý cấp, W = Chào mừng).
DECLARE @base datetime2(3)=CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2(3));   -- 0 giờ UTC = 7 giờ sáng giờ Việt Nam
DECLARE @plan TABLE(Seq int IDENTITY(1,1) PRIMARY KEY,CustomerID int,DaysAgo int,VariantID int,Qty int,Voucher char(1) NULL,At datetime2(3));
INSERT @plan(CustomerID,DaysAgo,VariantID,Qty,Voucher,At)   -- giờ đặt 8–18 giờ, cố định theo khách và ngày
SELECT CustomerID,DaysAgo,VariantID,Qty,Voucher,DATEADD(minute,(CustomerID*7+DaysAgo)%50,DATEADD(hour,1+(CustomerID*3+DaysAgo)%11,DATEADD(day,-DaysAgo,@base))) FROM (VALUES
(24,330,5,1,NULL),(22,320,9,1,NULL),(7,310,8,2,NULL),(3,300,15,30,NULL),(14,300,5,1,NULL),(16,300,7,1,NULL),
(21,300,3,2,NULL),(27,290,8,2,NULL),(4,280,7,1,NULL),(11,280,6,2,NULL),(3,265,7,1,NULL),(8,260,7,1,NULL),
(12,260,15,30,NULL),(7,250,3,2,NULL),(9,250,6,2,NULL),(22,240,9,1,NULL),(24,240,1,1,NULL),(16,230,9,1,NULL),
(3,220,15,30,NULL),(6,220,5,1,NULL),(11,210,8,2,NULL),(14,210,1,1,NULL),(4,200,12,25,NULL),(7,200,15,30,NULL),
(21,200,8,2,NULL),(27,200,6,2,NULL),(29,200,8,2,NULL),(3,180,10,2,NULL),(13,180,3,2,NULL),(19,180,10,2,NULL),
(22,170,1,1,NULL),(9,160,3,2,NULL),(16,160,1,1,NULL),(7,150,8,2,NULL),(11,150,12,25,NULL),(24,150,9,1,NULL),
(3,140,15,30,NULL),(8,140,1,2,'G'),(12,130,15,30,NULL),(27,130,8,2,NULL),(19,120,6,2,NULL),(22,120,1,2,'G'),
(25,120,3,2,NULL),(16,110,9,1,NULL),(29,110,6,2,NULL),(6,100,12,25,NULL),(9,100,8,2,NULL),(11,100,10,2,NULL),
(3,95,1,2,'G'),(4,90,9,1,NULL),(7,90,15,30,NULL),(17,90,6,2,NULL),(13,75,1,2,'G'),(16,75,12,25,NULL),
(19,75,9,1,NULL),(27,70,3,2,NULL),(21,65,9,1,NULL),(22,55,8,2,NULL),(3,50,15,30,NULL),(16,40,1,1,NULL),
(25,40,3,2,NULL),(7,35,5,1,NULL),(27,28,10,2,NULL),(11,25,7,1,NULL),(3,20,10,2,NULL),(5,20,3,2,'W'),
(15,12,6,2,'W'),(5,6,6,2,NULL)
) p(CustomerID,DaysAgo,VariantID,Qty,Voucher) ORDER BY 6;   -- Seq theo thời gian: mã đơn và phiếu kho tăng dần theo ngày giờ
DECLARE @lapsed TABLE(CustomerID int PRIMARY KEY);   -- mục 3 không tạo đơn hôm nay cho nhóm này
INSERT @lapsed VALUES(4),(9),(14),(19),(24),(29);
DECLARE @giftPromo int=(SELECT PromotionID FROM dbo.Promotions WHERE Name=N'Quà tặng 10%'),
        @welcomePromo int=(SELECT PromotionID FROM dbo.Promotions WHERE Name=N'Chào mừng');
DECLARE @hSeq int=1,@hMax int=(SELECT MAX(Seq) FROM @plan),@hc int,@hu int,@hActor int,@hDays int,@hVariant int,@hQty int,
        @hKind char(1),@hAt datetime2(3),@hBucket int,@hLastBucket int=-1,@hVoucher int,@hOrder int,
        @mN int,@mA int,@mL int,@mI int;
DECLARE @need TABLE(IngredientID int PRIMARY KEY,Qty decimal(18,3));
DECLARE @hist TABLE(OrderID int PRIMARY KEY,At datetime2(3),N0 int,A0 int,L0 int,I0 int,VoucherID int NULL);
WHILE @hSeq<=@hMax
BEGIN
 SELECT @hc=CustomerID,@hDays=DaysAgo,@hVariant=VariantID,@hQty=Qty,@hKind=Voucher,@hAt=At FROM @plan WHERE Seq=@hSeq;
 SELECT @hu=UserID FROM dbo.Customers WHERE CustomerID=@hc;
 SET @hBucket=@hDays/30;
 -- Phiếu nhập đầu kỳ: đủ nguyên liệu cho mọi đơn của kỳ và bù tồn về mức nhập ban đầu (06_seed.sql).
 IF @hBucket<>@hLastBucket
 BEGIN
  DELETE @need;
  INSERT @need SELECT g.IngredientID,CEILING(COALESCE(d.Qty,0)+f.Qty-g.Quantity)
  FROM dbo.Ingredients g
  CROSS APPLY (SELECT TOP(1) t.QuantityChange AS Qty FROM dbo.InventoryTransactions t WHERE t.IngredientID=g.IngredientID AND t.Kind='Import' ORDER BY t.TransactionID) f
  LEFT JOIN (SELECT r.IngredientID,SUM(p.Qty*r.Quantity) AS Qty FROM @plan p JOIN dbo.RecipeItems r ON r.VariantID=p.VariantID
             WHERE p.DaysAgo/30=@hBucket GROUP BY r.IngredientID) d ON d.IngredientID=g.IngredientID
  WHERE g.IsActive=1 AND COALESCE(d.Qty,0)+f.Qty-g.Quantity>0;
  INSERT dbo.InventoryTransactions(IngredientID,SupplierID,ActorID,Kind,QuantityChange,BalanceAfter,UnitCost,Note,CreatedAt)
  SELECT g.IngredientID,g.SupplierID,@manager,'Import',n.Qty,g.Quantity+n.Qty,g.LastUnitCost,N'Nhập hàng định kỳ',
         DATEADD(minute,30,DATEADD(day,-(@hBucket*30+29),@base))
  FROM dbo.Ingredients g JOIN @need n ON n.IngredientID=g.IngredientID;
  UPDATE g SET Quantity=g.Quantity+n.Qty,StockUpdatedAt=DATEADD(minute,30,DATEADD(day,-(@hBucket*30+29),@base))
  FROM dbo.Ingredients g JOIN @need n ON n.IngredientID=g.IngredientID;
  SET @hLastBucket=@hBucket;
 END;
 SELECT @mN=COALESCE(MAX(NotificationID),0) FROM dbo.Notifications;
 SELECT @mA=COALESCE(MAX(AuditID),0) FROM dbo.AuditLogs;
 SELECT @mL=COALESCE(MAX(LoyaltyTransactionID),0) FROM dbo.LoyaltyTransactions;
 SELECT @mI=COALESCE(MAX(TransactionID),0) FROM dbo.InventoryTransactions;
 SET @hVoucher=NULL;
 IF @hKind IS NOT NULL
 BEGIN
  SET @key=NEWID();
  IF @hKind='G' BEGIN SET @hActor=@manager; INSERT @r EXEC dbo.IssueVoucher @hActor,@hc,@giftPromo,@key; END
  ELSE BEGIN SET @hActor=@hu; INSERT @r EXEC dbo.IssueVoucher @hActor,@hc,@welcomePromo,@key; END;
  SELECT @hVoucher=ID FROM @r; DELETE @r;
 END;
 SET @key=NEWID();
 SET @lines=CONCAT(N'[{"variantId":',@hVariant,N',"quantity":',@hQty,
      CASE WHEN EXISTS(SELECT 1 FROM dbo.ProductVariants v JOIN dbo.ProductModifierGroups pg ON pg.ProductID=v.ProductID AND pg.ModifierGroupID=@iceGroup WHERE v.VariantID=@hVariant)
           THEN CONCAT(N',"optionIds":[',CASE WHEN (@hc+@hDays)%2=0 THEN @da ELSE @nong END,N']') ELSE N'' END,N'}]');
 INSERT @r EXEC dbo.CreateOrder @ActorID=@hu,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',
      @Items=@lines,@RequestKey=@key,@CustomerID=@hc,@CustomerPromotionID=@hVoucher;
 SELECT @hOrder=ID FROM @r; DELETE @r;
 -- Dời ngày tạo và mã đơn trước khi hoàn tất để phiếu "Bán hàng – đơn ..." mang đúng mã theo ngày.
 UPDATE dbo.Orders SET CreatedAt=@hAt,
      OrderCode=CONCAT('MB',FORMAT(DATEADD(hour,7,@hAt),'yyMMdd'),'-',CASE WHEN @hOrder>9999 THEN CONVERT(varchar(12),@hOrder) ELSE RIGHT(CONCAT('0000',@hOrder),4) END)
 WHERE OrderID=@hOrder;
 SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @hu,@hOrder,@key; SELECT @payment=ID FROM @r; DELETE @r;
 EXEC dbo.SimulatePayment @hu,@payment,'Succeeded';
 EXEC dbo.AdvanceOrder @staff,@hOrder,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@hOrder,'Preparing';
 EXEC dbo.AdvanceOrder @staff,@hOrder,'Ready'; EXEC dbo.AdvanceOrder @staff,@hOrder,'Completed';
 INSERT @hist VALUES(@hOrder,@hAt,@mN,@mA,@mL,@mI,@hVoucher);
 SET @hSeq+=1;
END;
-- Voucher Quà tặng 10% còn lại: 2 voucher chưa dùng (khách 17, 21), 2 voucher đã hết hạn (khách 24, 29).
DECLARE @extra TABLE(CustomerID int PRIMARY KEY,IssuedDaysAgo int,ExpiredDaysAgo int NULL,VoucherID int NULL);
INSERT @extra(CustomerID,IssuedDaysAgo,ExpiredDaysAgo) VALUES(17,15,NULL),(21,8,NULL),(24,100,10),(29,90,5);
SELECT @hc=MIN(CustomerID) FROM @extra;
WHILE @hc IS NOT NULL
BEGIN
 SET @key=NEWID(); INSERT @r EXEC dbo.IssueVoucher @manager,@hc,@giftPromo,@key;
 UPDATE @extra SET VoucherID=(SELECT ID FROM @r) WHERE CustomerID=@hc; DELETE @r;
 SELECT @hc=MIN(CustomerID) FROM @extra WHERE CustomerID>@hc;
END;
-- Dời thời gian các dòng do từng đơn sinh ra: thanh toán phút 1, xác nhận 2, pha chế 3, sẵn sàng 9, hoàn tất 12.
SELECT @mN=COALESCE(MAX(NotificationID),0) FROM dbo.Notifications;
SELECT @mA=COALESCE(MAX(AuditID),0) FROM dbo.AuditLogs;
SELECT @mL=COALESCE(MAX(LoyaltyTransactionID),0) FROM dbo.LoyaltyTransactions;
SELECT @mI=COALESCE(MAX(TransactionID),0) FROM dbo.InventoryTransactions;
DECLARE @span TABLE(OrderID int PRIMARY KEY,At datetime2(3),N0 int,N1 int,A0 int,A1 int,L0 int,L1 int,I0 int,I1 int,VoucherID int NULL);
INSERT @span SELECT OrderID,At,N0,LEAD(N0,1,@mN) OVER(ORDER BY OrderID),A0,LEAD(A0,1,@mA) OVER(ORDER BY OrderID),
       L0,LEAD(L0,1,@mL) OVER(ORDER BY OrderID),I0,LEAD(I0,1,@mI) OVER(ORDER BY OrderID),VoucherID FROM @hist;
UPDATE o SET PaymentDueAt=DATEADD(minute,15,s.At),CompletedAt=DATEADD(minute,12,s.At)
FROM dbo.Orders o JOIN @span s ON s.OrderID=o.OrderID;
UPDATE h SET CreatedAt=DATEADD(minute,CASE h.ToStatus WHEN 'Confirmed' THEN 2 WHEN 'Preparing' THEN 3 WHEN 'Ready' THEN 9 WHEN 'Completed' THEN 12
       ELSE CASE WHEN h.FromStatus IS NULL THEN 0 ELSE 1 END END,s.At)
FROM dbo.OrderStatusHistory h JOIN @span s ON s.OrderID=h.OrderID;
UPDATE a SET CreatedAt=DATEADD(second,20,s.At),SettledAt=CASE WHEN a.SettledAt IS NULL THEN NULL ELSE DATEADD(minute,1,s.At) END
FROM dbo.PaymentAttempts a JOIN @span s ON s.OrderID=a.OrderID;
UPDATE n SET CreatedAt=DATEADD(minute,CASE WHEN n.Body LIKE N'%Completed' THEN 12 WHEN n.Body LIKE N'%Ready' THEN 9 WHEN n.Body LIKE N'%Preparing' THEN 3
       WHEN n.Body LIKE N'%Confirmed' THEN 2 ELSE 1 END,s.At),ReadAt=DATEADD(hour,2,s.At)
FROM dbo.Notifications n JOIN @span s ON n.NotificationID>s.N0 AND n.NotificationID<=s.N1;
UPDATE l SET CreatedAt=DATEADD(minute,CASE WHEN l.Details LIKE '%Completed' THEN 12 WHEN l.Details LIKE '%Ready' THEN 9 WHEN l.Details LIKE '%Preparing' THEN 3
       WHEN l.Details LIKE '%Confirmed' THEN 2 ELSE 1 END,s.At)
FROM dbo.AuditLogs l JOIN @span s ON l.AuditID>s.A0 AND l.AuditID<=s.A1;
DISABLE TRIGGER dbo.TR_Loyalty_Immutable ON dbo.LoyaltyTransactions;
DISABLE TRIGGER dbo.TR_InventoryTransactions_Immutable ON dbo.InventoryTransactions;
DISABLE TRIGGER dbo.TR_CustomerPromotions_Terms ON dbo.CustomerPromotions;
UPDATE l SET CreatedAt=DATEADD(minute,12,s.At)
FROM dbo.LoyaltyTransactions l JOIN @span s ON l.LoyaltyTransactionID>s.L0 AND l.LoyaltyTransactionID<=s.L1;
UPDATE t SET CreatedAt=DATEADD(minute,12,s.At)
FROM dbo.InventoryTransactions t JOIN @span s ON t.TransactionID>s.I0 AND t.TransactionID<=s.I1
WHERE t.Kind='Export' AND t.ActorID IS NULL;
UPDATE cp SET IssuedAt=DATEADD(day,-3,s.At),UsedAt=DATEADD(minute,12,s.At)
FROM dbo.CustomerPromotions cp JOIN @span s ON s.VoucherID=cp.CustomerPromotionID;
UPDATE cp SET IssuedAt=DATEADD(hour,2,DATEADD(day,-x.IssuedDaysAgo,@base)),
       ExpiresAt=CASE WHEN x.ExpiredDaysAgo IS NULL THEN cp.ExpiresAt ELSE DATEADD(day,-x.ExpiredDaysAgo,@base) END
FROM dbo.CustomerPromotions cp JOIN @extra x ON x.VoucherID=cp.CustomerPromotionID;
ENABLE TRIGGER dbo.TR_CustomerPromotions_Terms ON dbo.CustomerPromotions;
ENABLE TRIGGER dbo.TR_InventoryTransactions_Immutable ON dbo.InventoryTransactions;
ENABLE TRIGGER dbo.TR_Loyalty_Immutable ON dbo.LoyaltyTransactions;
-- Thông báo và nhật ký phát hành voucher theo ngày phát hành mới.
UPDATE n SET CreatedAt=cp.IssuedAt FROM dbo.Notifications n JOIN dbo.CustomerPromotions cp ON cp.CustomerPromotionID=n.CustomerPromotionID;
UPDATE l SET CreatedAt=cp.IssuedAt FROM dbo.AuditLogs l JOIN dbo.CustomerPromotions cp ON l.EntityType='CustomerPromotions' AND l.EntityID=cp.CustomerPromotionID;
-- Tài khoản khách có lịch sử được tạo trước đơn đầu tiên 14 ngày.
UPDATE c SET CreatedAt=DATEADD(day,-14,x.FirstAt)
FROM dbo.Customers c JOIN (SELECT CustomerID,MIN(CreatedAt) AS FirstAt FROM dbo.Orders WHERE CustomerID IS NOT NULL GROUP BY CustomerID) x ON x.CustomerID=c.CustomerID
WHERE c.CreatedAt>DATEADD(day,-14,x.FirstAt);
UPDATE u SET CreatedAt=c.CreatedAt FROM dbo.Users u JOIN dbo.Customers c ON c.UserID=u.UserID WHERE u.CreatedAt>c.CreatedAt;
SET @key=NEWID();   -- khóa yêu cầu mới cho mục 1 (khóa cũ đã dùng cho voucher ở trên)

-- ===== 1. Các luồng mẫu gốc (Lan, Minh, POS) =====
INSERT @r EXEC dbo.IssueVoucher @lanUser,@lan,1,@key;
SELECT @voucher=ID FROM @r; DELETE @r;
SET @key=NEWID();
SET @lines=CONCAT(N'[{"variantId":1,"quantity":2,"optionIds":[',@da,N']}]');
INSERT @r EXEC dbo.CreateOrder @ActorID=@lanUser,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',@Items=@lines,@RequestKey=@key,@CustomerID=@lan,@CustomerPromotionID=@voucher;
SELECT @order=ID FROM @r; DELETE @r;
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @lanUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @lanUser,@payment,'Failed',N'Khách hủy lần thanh toán đầu';
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @lanUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @lanUser,@payment,'Succeeded';
EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
INSERT @r EXEC dbo.SubmitFeedback @ActorID=@lanUser,@CustomerID=@lan,@Content=N'Đồ uống ngon và phục vụ nhanh',@Type='Service',@Rating=5,@OrderID=@order,@BranchID=@branch;
SELECT @feedback=ID FROM @r; DELETE @r;
EXEC dbo.HandleFeedback @manager,@feedback,'Resolved',N'Cảm ơn bạn đã ghé Morning Brew!';
SET @key=NEWID();
SET @lines=CONCAT(N'[{"variantId":3,"quantity":1,"optionIds":[',@nong,N']}]');
INSERT @r EXEC dbo.CreateOrder @ActorID=@minhUser,@BranchID=@branch,@Source='App',@Fulfillment='Delivery',@PaymentMode='COD',@Items=@lines,@RequestKey=@key,@CustomerID=@minh,@AddressID=@minhAddress;
SELECT @order=ID FROM @r; DELETE @r;
EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready';
EXEC dbo.AdvanceOrder @ActorID=@staff,@OrderID=@order,@NextStatus='Delivering',@CourierName=N'Anh Bình',@CourierPhone='0908888888';
EXEC dbo.UpdateDelivery @staff,@order,'Failed',N'Khách chưa nghe điện thoại';
EXEC dbo.UpdateDelivery @staff,@order,'Delivering';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@staff,@BranchID=@branch,@Source='POS',@Fulfillment='Pickup',@PaymentMode='COD',@Items=N'[{"variantId":4,"quantity":1}]',@RequestKey=@key;
SELECT @order=ID FROM @r; DELETE @r;
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @staff,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @staff,@payment,'Succeeded';
EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing'; EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@lanUser,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',@Items=N'[{"variantId":4,"quantity":1}]',@RequestKey=@key,@CustomerID=@lan;
SELECT @order=ID FROM @r; DELETE @r;
EXEC dbo.CancelOrder @lanUser,@order,N'Khách đổi ý trước thanh toán';
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@minhUser,@BranchID=@branch,@Source='App',@Fulfillment='Delivery',@PaymentMode='Prepaid',@Items=N'[{"variantId":4,"quantity":1}]',@RequestKey=@key,@CustomerID=@minh,@AddressID=@minhAddress;
SELECT @order=ID FROM @r; DELETE @r;
SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @minhUser,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
EXEC dbo.SimulatePayment @minhUser,@payment,'Succeeded';
EXEC dbo.CancelOrder @minhUser,@order,N'Khách hủy trước xác nhận';
SELECT @refund=RefundID FROM dbo.Refunds WHERE OrderID=@order;
EXEC dbo.SimulateRefund @manager,@refund,'Failed',N'Lỗi mô phỏng lần đầu';
EXEC dbo.SimulateRefund @manager,@refund,'Succeeded';

-- ===== 2. Khảo sát: phát hành và mời toàn bộ 30 khách =====
DECLARE @s1 int=(SELECT SurveyID FROM dbo.Surveys WHERE Title=N'Khẩu vị và trải nghiệm'),
        @s2 int=(SELECT SurveyID FROM dbo.Surveys WHERE Title=N'Thăm dò món mới mùa đông');
DECLARE @s1q1 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s1 AND DisplayOrder=1),
        @s1q2 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s1 AND DisplayOrder=2),
        @s1q3 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s1 AND DisplayOrder=3),
        @s2q1 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=1),
        @s2q2 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=2),
        @s2q3 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=3),
        @s2q4 int=(SELECT QuestionID FROM dbo.SurveyQuestions WHERE SurveyID=@s2 AND DisplayOrder=4);
DECLARE @o1a int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s1q1 AND DisplayOrder=1),
        @o1b int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s1q1 AND DisplayOrder=2),
        @o1c int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s1q1 AND DisplayOrder=3),
        @newCacao int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=1),
        @newGinger int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=2),
        @newLatte int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=3),
        @newMatcha int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q1 AND DisplayOrder=4),
        @tMorning int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=1),
        @tNoon int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=2),
        @tAfternoon int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=3),
        @tEvening int=(SELECT OptionID FROM dbo.SurveyQuestionOptions WHERE QuestionID=@s2q3 AND DisplayOrder=4);
EXEC dbo.PublishSurvey @owner,@s1;
EXEC dbo.PublishSurvey @owner,@s2;
DECLARE @c int,@u int;
DECLARE invites CURSOR LOCAL FAST_FORWARD FOR SELECT CustomerID FROM dbo.Customers ORDER BY CustomerID;
OPEN invites; FETCH NEXT FROM invites INTO @c;
WHILE @@FETCH_STATUS=0
BEGIN
 EXEC dbo.InviteSurvey @owner,@s1,@c;
 EXEC dbo.InviteSurvey @owner,@s2,@c;
 FETCH NEXT FROM invites INTO @c;
END;
CLOSE invites; DEALLOCATE invites;
-- Lan đã trả lời khảo sát 1; khảo sát 2 để trống cho phần demo trên app.
DECLARE @answers nvarchar(max)=CONCAT(N'[{"questionId":',@s1q1,N',"optionIds":[',@o1a,N',',@o1b,N']},{"questionId":',@s1q2,N',"rating":5},{"questionId":',@s1q3,N',"text":"Thêm món theo mùa"}]');
EXEC dbo.SubmitSurvey @lanUser,@s1,@lan,@answers;

-- ===== 3. 28 khách còn lại: đơn hoàn tất, phản hồi, trả lời khảo sát =====
-- Phân bố theo CustomerID để dữ liệu cố định giữa các lần cài, không dùng số ngẫu nhiên.
DECLARE @items nvarchar(200),@variant int,@product int,@qty int,@rating int,@type varchar(15),@content nvarchar(400),@opts nvarchar(100),@text nvarchar(300);
DECLARE people CURSOR LOCAL FAST_FORWARD FOR SELECT CustomerID,UserID FROM dbo.Customers WHERE CustomerID NOT IN (@lan,@minh) ORDER BY CustomerID;
OPEN people; FETCH NEXT FROM people INTO @c,@u;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @order=NULL;
 -- 4/5 số khách có 1 đơn hoàn tất hôm nay (trừ nhóm lâu không quay lại); đồ uống (VariantID 5–8) kèm Đá / Nóng xen kẽ, bánh (4, 10) không có tùy chọn.
 IF @c % 5 <> 0 AND NOT EXISTS(SELECT 1 FROM @lapsed WHERE CustomerID=@c)   -- [DEMO-01]
 BEGIN
  SET @variant=CHOOSE(@c % 6 + 1, 4, 5, 6, 7, 8, 10);
  SET @qty=1 + @c % 2;
  SELECT @product=ProductID FROM dbo.ProductVariants WHERE VariantID=@variant;
  SET @key=NEWID();
  SET @items=CONCAT(N'[{"variantId":',@variant,N',"quantity":',@qty,
       CASE WHEN @variant BETWEEN 5 AND 8 THEN CONCAT(N',"optionIds":[',CASE WHEN @c % 2 = 0 THEN @da ELSE @nong END,N']') ELSE N'' END,N'}]');
  INSERT @r EXEC dbo.CreateOrder @ActorID=@u,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',
       @Items=@items,@RequestKey=@key,@CustomerID=@c;
  SELECT @order=ID FROM @r; DELETE @r;
  SET @key=NEWID(); INSERT @r EXEC dbo.StartPayment @u,@order,@key; SELECT @payment=ID FROM @r; DELETE @r;
  EXEC dbo.SimulatePayment @u,@payment,'Succeeded';
  EXEC dbo.AdvanceOrder @staff,@order,'Confirmed'; EXEC dbo.AdvanceOrder @staff,@order,'Preparing';
  EXEC dbo.AdvanceOrder @staff,@order,'Ready'; EXEC dbo.AdvanceOrder @staff,@order,'Completed';
 END;
 -- Phản hồi: gắn với món trong đơn; đủ 3 trạng thái Mới / Đang xử lý / Đã giải quyết.
 IF @order IS NOT NULL AND @c % 3 <> 0
 BEGIN
  SET @rating=CHOOSE(@c % 4 + 1, 5, 4, 3, 2);
  SET @content=CHOOSE(@c % 6 + 1, N'Món rất ngon, sẽ quay lại', N'Hơi ngọt so với khẩu vị của tôi', N'Đồ uống ra hơi chậm giờ cao điểm',
       N'Giá hợp lý, chất lượng ổn định', N'Ly nhỏ hơn mong đợi', N'Bánh mới, thơm, rất đáng thử');
  INSERT @r EXEC dbo.SubmitFeedback @ActorID=@u,@CustomerID=@c,@Content=@content,@Type='Product',@Rating=@rating,@OrderID=@order,@BranchID=@branch,@ProductID=@product;
  SELECT @feedback=ID FROM @r; DELETE @r;
  IF @c % 4 = 0 EXEC dbo.HandleFeedback @manager,@feedback,'Resolved',N'Cảm ơn bạn! Quán đã ghi nhận và điều chỉnh.';
  ELSE IF @c % 4 = 1 EXEC dbo.HandleFeedback @manager,@feedback,'Processing',N'Quán đang kiểm tra lại công thức.';
 END
 ELSE IF @order IS NULL AND @c % 2 = 1
 BEGIN
  SET @type=CASE WHEN @c % 4 = 1 THEN 'Space' ELSE 'Service' END;
  INSERT @r EXEC dbo.SubmitFeedback @ActorID=@u,@CustomerID=@c,@Content=N'Không gian thoáng nhưng thiếu ổ cắm điện',@Type=@type,@Rating=4,@BranchID=@branch;
  DELETE @r;
 END;
 -- Khảo sát 1: khoảng 3/4 số khách trả lời.
 IF @c % 4 <> 3
 BEGIN
  SET @opts=CASE @c % 3 WHEN 0 THEN CONCAT(@o1a,N'') WHEN 1 THEN CONCAT(@o1a,N',',@o1c) ELSE CONCAT(@o1b,N',',@o1c) END;
  SET @text=CASE WHEN @c % 5 = 1 THEN N'Mong có thêm món ít đường' ELSE NULL END;
  SET @answers=CONCAT(N'[{"questionId":',@s1q1,N',"optionIds":[',@opts,N']},{"questionId":',@s1q2,N',"rating":',3 + @c % 3,N'}',
       CASE WHEN @text IS NULL THEN N'' ELSE CONCAT(N',{"questionId":',@s1q3,N',"text":"',@text,N'"}') END,N']');
  EXEC dbo.SubmitSurvey @u,@s1,@c,@answers;
 END;
 -- Khảo sát 2 (thăm dò món mới): khoảng 2/3 số khách trả lời, Cacao muối dẫn đầu.
 IF @c % 3 <> 1
 BEGIN
  SET @opts=CONCAT(CASE WHEN @c % 7 IN (0,1,2) THEN @newCacao WHEN @c % 7 IN (3,4) THEN @newLatte WHEN @c % 7 = 5 THEN @newGinger ELSE @newMatcha END,N'');
  SET @text=CASE WHEN @c % 6 = 0 THEN N'Nên có size nhỏ cho món mới' ELSE NULL END;
  SET @answers=CONCAT(N'[{"questionId":',@s2q1,N',"optionIds":[',@opts,N']},{"questionId":',@s2q2,N',"rating":',2 + @c % 4,N'},',
       N'{"questionId":',@s2q3,N',"optionIds":[',CASE @c % 3 WHEN 0 THEN CONCAT(@tMorning,N'') WHEN 1 THEN CONCAT(@tMorning,N',',@tAfternoon) ELSE CONCAT(@tNoon,N',',@tEvening) END,N']}',
       CASE WHEN @text IS NULL THEN N'' ELSE CONCAT(N',{"questionId":',@s2q4,N',"text":"',@text,N'"}') END,N']');
  EXEC dbo.SubmitSurvey @u,@s2,@c,@answers;
 END;
 FETCH NEXT FROM people INTO @c,@u;
END;
CLOSE people; DEALLOCATE people;
-- ===== 4. [DEMO-01] Hủy hàng và kiểm kê cuối ngày (cảnh báo tồn thấp có dữ liệu) =====
INSERT dbo.InventoryTransactions(IngredientID,ActorID,Kind,QuantityChange,BalanceAfter,Note)
SELECT IngredientID,@manager,'Waste',-0.500,Quantity-0.500,N'Sữa hết hạn, đổ bỏ' FROM dbo.Ingredients WHERE Code='NL-SU01' AND Quantity>=0.500;
UPDATE dbo.Ingredients SET Quantity=Quantity-0.500,StockUpdatedAt=SYSUTCDATETIME() WHERE Code='NL-SU01' AND Quantity>=0.500;
DECLARE @count TABLE(Code varchar(30) PRIMARY KEY,Qty decimal(18,3));
INSERT @count VALUES('NL-TC02',1.000),('NL-TR02',0.300),('NL-SU03',2.000);   -- dưới mức tối thiểu
INSERT dbo.InventoryTransactions(IngredientID,ActorID,Kind,QuantityChange,BalanceAfter,Note)
SELECT g.IngredientID,@manager,'Adjust',k.Qty-g.Quantity,k.Qty,N'Kiểm kê cuối ngày: hao hụt thực tế'
FROM dbo.Ingredients g JOIN @count k ON k.Code=g.Code WHERE g.Quantity<>k.Qty;
UPDATE g SET Quantity=k.Qty,StockUpdatedAt=SYSUTCDATETIME() FROM dbo.Ingredients g JOIN @count k ON k.Code=g.Code WHERE g.Quantity<>k.Qty;
SELECT 'Demo ready' AS Result,
 (SELECT COUNT(*) FROM dbo.Customers) AS Customers,
 (SELECT COUNT(*) FROM dbo.Orders WHERE Status='Completed') AS CompletedOrders,
 (SELECT COUNT(*) FROM dbo.Feedbacks) AS Feedbacks,
 (SELECT COUNT(*) FROM dbo.SurveyRecipients WHERE CompletedAt IS NOT NULL) AS SurveyAnswers,
 (SELECT STRING_AGG(CONCAT(Segment,'=',n),', ') FROM (SELECT Segment,COUNT(*) AS n FROM dbo.CustomerStats GROUP BY Segment) s) AS Segments,   -- [DEMO-01]
 (SELECT STRING_AGG(CONCAT(TierCode,'=',n),', ') FROM (SELECT TierCode,COUNT(*) AS n FROM dbo.CustomerTiers GROUP BY TierCode) s) AS Tiers,
 (SELECT SUM(ABS(Difference)) FROM dbo.LoyaltyReconciliation) AS LoyaltyDifference;
GO
