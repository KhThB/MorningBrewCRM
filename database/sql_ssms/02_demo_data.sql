-- 02_demo_data.sql – dữ liệu mẫu (tùy chọn, chỉ chạy ngay sau 01 trên database mới)
-- =====================================================================================
-- CÁCH CHẠY (SQL Server Management Studio hoặc Azure Data Studio, KHÔNG cần PowerShell):
--   1. Kết nối tới server, ví dụ (localdb)\MSSQLLocalDB.
--   2. File → Open → chọn file này → bấm Execute (F5). Không cần chọn database trước:
--      file tự chuyển sang database [MorningBrewCRM_Crm].
--   3. Chạy lần lượt: 01_core → 02_demo_data (tùy chọn) → 03_crm_updates → 04_menu_updates.
--   4. Chạy xong, tab Messages phải có dòng "=== HOÀN TẤT ...". Nếu thấy dòng ">>> DỪNG"
--      hoặc không có "HOÀN TẤT" thì file đã dừng giữa chừng: đọc lỗi màu đỏ đầu tiên, sửa
--      nguyên nhân rồi mới chạy tiếp. Khi có lỗi, các lệnh phía sau tự động không chạy.
--   Muốn dùng tên database khác: Edit → Find and Replace "MorningBrewCRM_Crm" ở CẢ 4 file
--   (và sửa ConnectionStrings:MorningBrew của API cho khớp).
--   File lưu UTF-8 có BOM để SSMS đọc đúng tiếng Việt.
-- Nội dung SQL giữ nguyên bản database/sql (nhánh db-test); chỉ thêm phần chọn database,
-- kiểm tra điều kiện trước khi chạy và lệnh "dừng khi lỗi" giữa các khối lệnh.
-- =====================================================================================
GO
SET NOEXEC OFF;  -- bỏ trạng thái dừng còn sót từ lần chạy trước trong cùng cửa sổ
GO
USE [MorningBrewCRM_Crm];
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
IF DB_NAME() <> N'MorningBrewCRM_Crm'
BEGIN
    RAISERROR(N'>>> DỪNG: không tìm thấy database [MorningBrewCRM_Crm]. Chạy 01_core.sql trước.', 16, 1);
    SET NOEXEC ON;
END
IF OBJECT_ID(N'dbo.CreateOrder', N'P') IS NULL
BEGIN
    RAISERROR(N'>>> DỪNG: chưa có bảng/thủ tục lõi. Chạy 01_core.sql trước.', 16, 1);
    SET NOEXEC ON;
END
IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'>>> DỪNG: database đã chạy 03_crm_updates.sql. Dữ liệu mẫu (02) phải chạy TRƯỚC 03; hãy cài lại từ database trống.', 16, 1);
    SET NOEXEC ON;
END
IF EXISTS (SELECT 1 FROM dbo.Roles)
BEGIN
    RAISERROR(N'>>> DỪNG: database đã có dữ liệu. Không chạy lại 02_demo_data.sql.', 16, 1);
    SET NOEXEC ON;
END
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST PACKAGE: Preserves the original SQL batch order. See database/README.md.
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 06_seed.sql (historical filename)
-- Demo password for all accounts: MorningBrew!2026. ASP.NET PasswordHasher V3, SHA512.
-- Phạm vi demo CRM: 1 cửa hàng (Morning Brew Thủ Đức), không có phần nhân sự (HRM).
-- Tài khoản: 1 chủ cửa hàng, 1 quản lý, 1 thu ngân (POS) và 30 khách hàng.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF EXISTS(SELECT 1 FROM dbo.Roles) THROW 51000,N'Seed requires empty tables.',1;
BEGIN TRAN;
-- Vai trò AreaManager đã bị bỏ trong 11_single_store.sql (chỉ có 1 cửa hàng).
INSERT dbo.Roles(Code,Name) VALUES('Owner',N'Chủ cửa hàng'),('BranchManager',N'Quản lý cửa hàng'),('Staff',N'Nhân viên thu ngân'),('Customer',N'Khách hàng');
INSERT dbo.Permissions(Code,Description) VALUES
('Orders.Create',N'Tạo đơn tại POS'),('Orders.Manage',N'Cập nhật xử lý đơn'),('Orders.Cancel',N'Hủy đơn sau xác nhận'),('Orders.Read',N'Xem lịch sử đơn'),
('Payments.Manage',N'Ghi nhận tiền đã thu'),('Refunds.Manage',N'Xử lý hoàn tiền'),('Menu.Manage',N'Quản lý thực đơn'),('Customers.Lookup',N'Tra cứu thành viên bằng số điện thoại'),('Customers.Read',N'Xem và phân nhóm khách hàng'),('Promotions.Manage',N'Quản lý và cấp ưu đãi'),('Surveys.Manage',N'Quản lý khảo sát'),('Feedbacks.Manage',N'Xử lý phản hồi'),('Reports.Read',N'Xem báo cáo'),('Organization.Manage',N'Quản lý tổ chức'),('Security.Manage',N'Quản lý tài khoản và quyền'),('Catalog.Manage',N'Quản lý danh mục dùng chung');
INSERT dbo.RolePermissions SELECT r.RoleID,p.PermissionID FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Code='Owner' OR (r.Code='BranchManager' AND p.Code NOT IN ('Organization.Manage','Security.Manage','Catalog.Manage')) OR (r.Code='Staff' AND p.Code IN ('Orders.Create','Orders.Manage','Orders.Read','Payments.Manage','Customers.Lookup'));
DECLARE @hash varchar(512)='AQAAAAIAAYagAAAAEE8G2F5hjA72TxlV9NrR092YRGzNf1+QMFGndUjRlWxuS/DMviEjHdzD5daFtHJW2w==';
INSERT dbo.Users(RoleID,Email,NormalizedEmail,PasswordHash,FullName,Phone)
SELECT r.RoleID,v.Email,UPPER(v.Email),@hash,v.FullName,v.Phone FROM (VALUES
(1,'Owner','owner@morningbrew.test',N'Chủ cửa hàng','0900000001'),
(2,'BranchManager','manager.1@morningbrew.test',N'Quản lý cửa hàng','0900000003'),
(3,'Staff','staff.1@morningbrew.test',N'Thu ngân','0900000004'),
(4,'Customer','lan@morningbrew.test',N'Nguyễn Lan','0901234567'),
(5,'Customer','minh@morningbrew.test',N'Trần Minh','0907654321'),
(6,'Customer','khach01@morningbrew.test',N'Phạm Thu Hà','0912000001'),
(7,'Customer','khach02@morningbrew.test',N'Lê Quang Huy','0912000002'),
(8,'Customer','khach03@morningbrew.test',N'Võ Ngọc Anh','0912000003'),
(9,'Customer','khach04@morningbrew.test',N'Đặng Minh Khoa','0912000004'),
(10,'Customer','khach05@morningbrew.test',N'Bùi Thảo Vy','0912000005'),
(11,'Customer','khach06@morningbrew.test',N'Hoàng Gia Bảo','0912000006'),
(12,'Customer','khach07@morningbrew.test',N'Ngô Khánh Linh','0912000007'),
(13,'Customer','khach08@morningbrew.test',N'Trịnh Đức Thắng','0912000008'),
(14,'Customer','khach09@morningbrew.test',N'Phan Mỹ Duyên','0912000009'),
(15,'Customer','khach10@morningbrew.test',N'Đỗ Hoàng Nam','0912000010'),
(16,'Customer','khach11@morningbrew.test',N'Huỳnh Bảo Trâm','0912000011'),
(17,'Customer','khach12@morningbrew.test',N'Lý Tuấn Kiệt','0912000012'),
(18,'Customer','khach13@morningbrew.test',N'Mai Phương Thảo','0912000013'),
(19,'Customer','khach14@morningbrew.test',N'Tạ Quốc Việt','0912000014'),
(20,'Customer','khach15@morningbrew.test',N'Châu Ngọc Hân','0912000015'),
(21,'Customer','khach16@morningbrew.test',N'Kiều Anh Tú','0912000016'),
(22,'Customer','khach17@morningbrew.test',N'Lâm Thanh Tâm','0912000017'),
(23,'Customer','khach18@morningbrew.test',N'Dương Văn Lộc','0912000018'),
(24,'Customer','khach19@morningbrew.test',N'Tô Hồng Nhung','0912000019'),
(25,'Customer','khach20@morningbrew.test',N'Quách Minh Trí','0912000020'),
(26,'Customer','khach21@morningbrew.test',N'Vương Thu Trang','0912000021'),
(27,'Customer','khach22@morningbrew.test',N'La Đình Phúc','0912000022'),
(28,'Customer','khach23@morningbrew.test',N'Thái Kim Oanh','0912000023'),
(29,'Customer','khach24@morningbrew.test',N'Hà Văn Sơn','0912000024'),
(30,'Customer','khach25@morningbrew.test',N'Lục Thị Mai','0912000025'),
(31,'Customer','khach26@morningbrew.test',N'Cao Văn Hùng','0912000026'),
(32,'Customer','khach27@morningbrew.test',N'Âu Thị Hồng','0912000027'),
(33,'Customer','khach28@morningbrew.test',N'Nghiêm Văn Toàn','0912000028')
) v(Seq,RoleCode,Email,FullName,Phone) JOIN dbo.Roles r ON r.Code=v.RoleCode ORDER BY v.Seq;
DECLARE @owner int=(SELECT UserID FROM dbo.Users WHERE Email='owner@morningbrew.test'),
        @manager int=(SELECT UserID FROM dbo.Users WHERE Email='manager.1@morningbrew.test'),
        @staff int=(SELECT UserID FROM dbo.Users WHERE Email='staff.1@morningbrew.test');
-- Một khu vực và một cửa hàng duy nhất (bảng Areas/Branches giữ nguyên trong schema).
INSERT dbo.Areas(Name) VALUES(N'TP. Hồ Chí Minh');
-- Demo shop opens all day so exercises work at any local time. Production should set real hours.
INSERT dbo.Branches(AreaID,ManagerID,Code,Name,Address,Phone,OpeningTime,ClosingTime,DeliveryFee) VALUES
(1,@manager,'MB01',N'Morning Brew Thủ Đức',N'12 Võ Văn Ngân, TP.HCM','0281111111','00:00','23:59:59',15000);
INSERT dbo.StaffBranches(UserID,BranchID) VALUES(@staff,1);
-- 30 khách hàng: ngày sinh trải đều các nhóm tuổi để báo cáo tỷ lệ độ tuổi có ý nghĩa.
INSERT dbo.Customers(UserID,DateOfBirth,Gender,LoyaltyPoints)
SELECT u.UserID,v.DateOfBirth,v.Gender,v.Points FROM (VALUES
('lan@morningbrew.test','2002-05-12','Female',100),('minh@morningbrew.test','1998-10-10','Male',100)
,
('khach01@morningbrew.test','2006-03-14','Female',50),
('khach02@morningbrew.test','2005-11-02','Male',50),
('khach03@morningbrew.test','2004-07-21','Female',50),
('khach04@morningbrew.test','2003-01-09','Male',50),
('khach05@morningbrew.test','2003-09-30','Female',50),
('khach06@morningbrew.test','2002-12-18','Male',50),
('khach07@morningbrew.test','2001-04-25','Female',50),
('khach08@morningbrew.test','2000-08-08','Male',50),
('khach09@morningbrew.test','1999-02-11','Female',50),
('khach10@morningbrew.test','1998-06-03','Male',50),
('khach11@morningbrew.test','1997-10-27','Female',50),
('khach12@morningbrew.test','1996-05-15','Male',50),
('khach13@morningbrew.test','1995-01-20','Female',50),
('khach14@morningbrew.test','1994-03-07','Male',50),
('khach15@morningbrew.test','1993-12-01','Female',50),
('khach16@morningbrew.test','1992-09-19','Other',50),
('khach17@morningbrew.test','1990-07-04','Female',50),
('khach18@morningbrew.test','1989-11-23','Male',50),
('khach19@morningbrew.test','1987-02-28','Female',50),
('khach20@morningbrew.test','1985-08-16','Male',50),
('khach21@morningbrew.test','1983-05-09','Female',50),
('khach22@morningbrew.test','1981-10-12','Male',50),
('khach23@morningbrew.test','1979-04-02','Female',50),
('khach24@morningbrew.test','1976-01-30','Male',50),
('khach25@morningbrew.test','1973-06-21','Female',50),
('khach26@morningbrew.test','1970-09-05','Male',50),
('khach27@morningbrew.test','1967-12-14','Undisclosed',50),
('khach28@morningbrew.test','1964-03-26','Male',50)
) v(Email,DateOfBirth,Gender,Points) JOIN dbo.Users u ON u.Email=v.Email ORDER BY u.UserID;
INSERT dbo.LoyaltyTransactions(CustomerID,Kind,Points,RequestKey,Note)
SELECT CustomerID,'Adjust',LoyaltyPoints,NEWID(),N'Điểm tặng khởi tạo dữ liệu demo' FROM dbo.Customers;
INSERT dbo.CustomerAddresses(CustomerID,Label,RecipientName,Phone,AddressText,IsDefault) VALUES(1,N'Nhà',N'Nguyễn Lan','0901234567',N'12 đường A, TP.HCM',1),(2,N'Công ty',N'Trần Minh','0907654321',N'20 đường B, TP.HCM',1);
-- Thực đơn 10 món (ID trùng với thực đơn mẫu trong app khách hàng).
INSERT dbo.Categories(Name) VALUES(N'Cà phê'),(N'Trà'),(N'Bánh');
INSERT dbo.Products(CategoryID,Name,Description,ImageURL) VALUES
(1,N'Cà phê sữa',N'Cà phê và sữa đặc','/images/coffee.jpg'),(2,N'Trà đào',N'Trà đào thơm mát','/images/tea.jpg'),(3,N'Bánh tiramisu',N'Bánh vị cà phê','/images/cake.jpg'),
(1,N'Cà phê đen đá',N'Đậm vị robusta',NULL),(1,N'Bạc xỉu',N'Nhiều sữa, ít cà phê',NULL),(1,N'Cold brew cam sả',N'Ủ lạnh 16 giờ',NULL),
(2,N'Trà vải lài',N'Trà lài và vải tươi',NULL),(2,N'Trà sen vàng',N'Hạt sen, kem sữa',NULL),(3,N'Bánh croissant bơ',N'Nướng mỗi sáng',NULL),(3,N'Bánh mousse chanh dây',N'Chua ngọt nhẹ',NULL);
INSERT dbo.ProductVariants(ProductID,Name,BasePrice,DisplayOrder) VALUES
(1,'M',30000,1),(1,'L',40000,2),(2,'M',35000,1),(3,'Standard',45000,1),
(4,'M',29000,1),(5,'M',32000,1),(6,'M',45000,1),(7,'M',39000,1),(8,'M',45000,1),(9,'Standard',29000,1),(10,'Standard',39000,1);
INSERT dbo.BranchProducts(BranchID,ProductID) SELECT 1,ProductID FROM dbo.Products;
UPDATE dbo.BranchProducts SET IsAvailable=0 WHERE ProductID IN (8,10); -- minh họa món tạm hết
INSERT dbo.BranchProductVariants(BranchID,VariantID,ProductID,SellingPrice) VALUES(1,1,1,30000);
INSERT dbo.ModifierGroups(Name,Kind) VALUES(N'Đường','Sugar'),(N'Đá','Ice'),(N'Topping','Topping');
INSERT dbo.ModifierOptions(ModifierGroupID,Name,ExtraPrice,DisplayOrder) VALUES(1,N'50% đường',0,1),(1,N'100% đường',0,2),(2,N'50% đá',0,1),(2,N'100% đá',0,2),(3,N'Trân châu',5000,1),(3,N'Pudding',7000,2);
INSERT dbo.ProductModifierGroups(ProductID,ModifierGroupID,MinSelections,MaxSelections) VALUES(1,1,1,1),(1,2,1,1),(1,3,0,2),(2,1,1,1),(2,2,1,1),(2,3,0,2);
-- Sở thích: mỗi khách 2 món, phân bố lệch để báo cáo "món được yêu thích" có xếp hạng rõ.
INSERT dbo.CustomerPreferences(CustomerID,ProductID)
SELECT c.CustomerID,p.ProductID FROM dbo.Customers c CROSS APPLY (VALUES
 (CASE c.CustomerID % 5 WHEN 0 THEN 1 WHEN 1 THEN 1 WHEN 2 THEN 5 WHEN 3 THEN 2 ELSE 7 END),
 (CASE c.CustomerID % 4 WHEN 0 THEN 3 WHEN 1 THEN 9 WHEN 2 THEN 6 ELSE 4 END)) p(ProductID)
GROUP BY c.CustomerID,p.ProductID;
INSERT dbo.LoyaltyPolicies(Name,AmountPerPoint,EffectiveFrom) VALUES(N'10.000 đồng đổi 1 điểm',10000,'2020-01-01');
INSERT dbo.Promotions(Name,Description,Scope,CreatedBy,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit) VALUES
(N'Đổi 50 điểm giảm 10.000',N'Đơn từ 50.000 đồng','Chain',@owner,'Amount',10000,NULL,50000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),50,1000,3),
(N'Quà tặng 10%',N'Đơn từ 30.000 đồng, giảm tối đa 20.000','Chain',@owner,'Percent',10,20000,30000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),NULL,1000,1),
(N'Chào mừng',N'Nhận miễn phí, giảm 5.000 đồng từ đơn 10.000','Chain',@owner,'Amount',5000,NULL,10000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),0,1000,1);
-- Khảo sát 1: trải nghiệm hiện tại. Khảo sát 2: thăm dò trước khi ra mắt món mới.
INSERT dbo.Surveys(Title,Description,CreatedBy,Scope,StartsAt,EndsAt) VALUES
(N'Khẩu vị và trải nghiệm',N'Khảo sát cải thiện sản phẩm',@owner,'Chain',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME())),
(N'Thăm dò món mới mùa đông',N'Giúp Morning Brew chọn món ra mắt tháng 12',@owner,'Chain',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(month,2,SYSUTCDATETIME()));
INSERT dbo.SurveyQuestions(SurveyID,Text,Type,IsRequired,DisplayOrder,RatingMin,RatingMax) VALUES
(1,N'Bạn thích những vị nào?','MultipleChoice',1,1,NULL,NULL),(1,N'Bạn hài lòng mức nào?','Rating',1,2,1,5),(1,N'Góp ý thêm','Text',0,3,NULL,NULL),
(2,N'Bạn muốn thử món mới nào nhất?','SingleChoice',1,1,NULL,NULL),(2,N'Mức giá 45.000đ cho món mới có hợp lý?','Rating',1,2,1,5),(2,N'Bạn thường ghé quán vào lúc nào?','MultipleChoice',1,3,NULL,NULL),(2,N'Gợi ý khác cho món mới','Text',0,4,NULL,NULL);
INSERT dbo.SurveyQuestionOptions(QuestionID,Text,DisplayOrder) VALUES
(1,N'Cà phê',1),(1,N'Socola',2),(1,N'Trà',3),
(4,N'Cacao muối',1),(4,N'Trà gừng mật ong',2),(4,N'Latte hạt dẻ',3),(4,N'Matcha khoai môn',4),
(6,N'Buổi sáng',1),(6,N'Buổi trưa',2),(6,N'Buổi chiều',3),(6,N'Buổi tối',4);
COMMIT;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 07_demo.sql (historical filename)
-- Dữ liệu giao dịch demo cho phạm vi CRM (1 cửa hàng, 30 khách).
-- Mọi ID được tra theo email / thứ tự hiển thị, không gán cứng.
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
SELECT @minhAddress=AddressID FROM dbo.CustomerAddresses WHERE CustomerID=@minh AND IsDefault=1;

-- ===== 1. Các luồng mẫu gốc (Lan, Minh, POS) =====
INSERT @r EXEC dbo.IssueVoucher @lanUser,@lan,1,@key;
SELECT @voucher=ID FROM @r; DELETE @r;
SET @key=NEWID();
INSERT @r EXEC dbo.CreateOrder @ActorID=@lanUser,@BranchID=@branch,@Source='App',@Fulfillment='Pickup',@PaymentMode='Prepaid',@Items=N'[{"variantId":1,"quantity":2,"optionIds":[1,3,5]}]',@RequestKey=@key,@CustomerID=@lan,@CustomerPromotionID=@voucher;
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
INSERT @r EXEC dbo.CreateOrder @ActorID=@minhUser,@BranchID=@branch,@Source='App',@Fulfillment='Delivery',@PaymentMode='COD',@Items=N'[{"variantId":3,"quantity":1,"optionIds":[1,3]}]',@RequestKey=@key,@CustomerID=@minh,@AddressID=@minhAddress;
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
 -- 4/5 số khách có 1 đơn hoàn tất (món không cần chọn topping/đường/đá).
 IF @c % 5 <> 0
 BEGIN
  SET @variant=CHOOSE(@c % 6 + 1, 4, 5, 6, 7, 8, 10);
  SET @qty=1 + @c % 2;
  SELECT @product=ProductID FROM dbo.ProductVariants WHERE VariantID=@variant;
  SET @key=NEWID();
  SET @items=CONCAT(N'[{"variantId":',@variant,N',"quantity":',@qty,N'}]');
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
SELECT 'Demo ready' AS Result,
 (SELECT COUNT(*) FROM dbo.Customers) AS Customers,
 (SELECT COUNT(*) FROM dbo.Orders WHERE Status='Completed') AS CompletedOrders,
 (SELECT COUNT(*) FROM dbo.Feedbacks) AS Feedbacks,
 (SELECT COUNT(*) FROM dbo.SurveyRecipients WHERE CompletedAt IS NOT NULL) AS SurveyAnswers;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
PRINT N'=== HOÀN TẤT 02_demo_data.sql trên database [MorningBrewCRM_Crm]. Tiếp theo: 03_crm_updates.sql.';
SELECT (SELECT COUNT(*) FROM dbo.Users) AS SoTaiKhoan, (SELECT COUNT(*) FROM dbo.Customers) AS SoKhachHang, (SELECT COUNT(*) FROM dbo.Products) AS SoSanPham;
GO
SET NOEXEC OFF;
GO
