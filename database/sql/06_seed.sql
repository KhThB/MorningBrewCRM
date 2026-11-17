-- Demo password for all accounts: MorningBrew!2026. ASP.NET PasswordHasher V3, SHA512.
-- Phạm vi demo CRM: 1 cửa hàng (Morning Brew Thủ Đức), không có phần nhân sự (HRM).
-- Tài khoản: 1 chủ cửa hàng, 1 quản lý, 1 thu ngân (POS) và 30 khách hàng.
-- [GOP-01] Bản gộp: dữ liệu nền ở trạng thái cuối, thay cho các đoạn dữ liệu trước nằm ở script nâng cấp:
--   quyền Customers.Manage (11), Inventory.Manage (14), Loyalty.Adjust (16); quản lý được gán vào StaffBranches (11);
--   thực đơn chỉ còn Size M/L + Nhiệt độ Đá/Nóng, mọi món đang bán (13); nhà cung cấp mẫu (13);
--   16 nguyên liệu + phiếu kho mẫu (14); định mức nguyên liệu cho 10 món (15); 4 hạng thành viên (17).
SET NOCOUNT ON; SET XACT_ABORT ON;
IF EXISTS(SELECT 1 FROM dbo.Roles) THROW 51000,N'Seed requires empty tables.',1;
BEGIN TRAN;
-- Không có vai trò AreaManager (mô hình một cửa hàng).
INSERT dbo.Roles(Code,Name) VALUES('Owner',N'Chủ cửa hàng'),('BranchManager',N'Quản lý cửa hàng'),('Staff',N'Nhân viên thu ngân'),('Customer',N'Khách hàng');
-- [CRM-00] Không còn quyền Organization.Manage (thừa từ mô hình nhiều chi nhánh).
-- Quản lý có mọi quyền trừ Security.Manage, Catalog.Manage và Loyalty.Adjust (chủ cửa hàng cấp thêm ở Admin server → Phân quyền).
INSERT dbo.Permissions(Code,Description) VALUES
('Orders.Create',N'Tạo đơn tại POS'),('Orders.Manage',N'Cập nhật xử lý đơn'),('Orders.Cancel',N'Hủy đơn sau xác nhận'),('Orders.Read',N'Xem lịch sử đơn'),
('Payments.Manage',N'Ghi nhận tiền đã thu'),('Refunds.Manage',N'Xử lý hoàn tiền'),('Menu.Manage',N'Quản lý thực đơn'),('Customers.Lookup',N'Tra cứu thành viên bằng số điện thoại'),('Customers.Read',N'Xem và phân nhóm khách hàng'),('Promotions.Manage',N'Quản lý và cấp ưu đãi'),('Surveys.Manage',N'Quản lý khảo sát'),('Feedbacks.Manage',N'Xử lý phản hồi'),('Reports.Read',N'Xem báo cáo'),('Security.Manage',N'Quản lý tài khoản và quyền'),('Catalog.Manage',N'Quản lý danh mục dùng chung'),
('Customers.Manage',N'Thêm, xóa, khóa tài khoản khách hàng'),('Inventory.Manage',N'Quản lý kho nguyên liệu'),('Loyalty.Adjust',N'Điều chỉnh điểm thành viên bằng tay');
INSERT dbo.RolePermissions SELECT r.RoleID,p.PermissionID FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Code='Owner' OR (r.Code='BranchManager' AND p.Code NOT IN ('Security.Manage','Catalog.Manage','Loyalty.Adjust')) OR (r.Code='Staff' AND p.Code IN ('Orders.Create','Orders.Manage','Orders.Read','Payments.Manage','Customers.Lookup'));
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
INSERT dbo.StaffBranches(UserID,BranchID) VALUES(@staff,1),(@manager,1); -- quản lý gán cả ở StaffBranches để có thể có nhiều quản lý
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
(4,'M',29000,1),(5,'M',32000,1),(6,'M',45000,1),(7,'M',39000,1),(8,'M',45000,1),(9,'Standard',29000,1),(10,'Standard',39000,1),
-- Size L = giá M + 10.000đ cho đồ uống (VariantID 12–17).
(2,'L',45000,2),(4,'L',39000,2),(5,'L',42000,2),(6,'L',55000,2),(7,'L',49000,2),(8,'L',55000,2);
INSERT dbo.BranchProducts(BranchID,ProductID) SELECT 1,ProductID FROM dbo.Products;
INSERT dbo.BranchProductVariants(BranchID,VariantID,ProductID,SellingPrice) VALUES(1,1,1,30000);
-- Chọn món chỉ còn Size + Nhiệt độ (Đá / Nóng, bắt buộc với đồ uống). Nhóm Đường, Topping giữ ở trạng thái tắt để có thể bật lại.
INSERT dbo.ModifierGroups(Name,Kind,IsActive) VALUES(N'Đường','Sugar',0),(N'Nhiệt độ','Ice',1),(N'Topping','Topping',0);
INSERT dbo.ModifierOptions(ModifierGroupID,Name,ExtraPrice,DisplayOrder) VALUES(1,N'50% đường',0,1),(1,N'100% đường',0,2),(2,N'Đá',0,1),(2,N'Nóng',0,2),(3,N'Trân châu',5000,1),(3,N'Pudding',7000,2);
INSERT dbo.ProductModifierGroups(ProductID,ModifierGroupID,MinSelections,MaxSelections) VALUES(1,1,1,1),(1,3,0,2),(2,1,1,1),(2,3,0,2),
(1,2,1,1),(2,2,1,1),(4,2,1,1),(5,2,1,1),(6,2,1,1),(7,2,1,1),(8,2,1,1); -- đồ uống (Cà phê, Trà) bắt buộc chọn Đá / Nóng
-- Sở thích: mỗi khách 2 món, phân bố lệch để báo cáo "món được yêu thích" có xếp hạng rõ.
INSERT dbo.CustomerPreferences(CustomerID,ProductID)
SELECT c.CustomerID,p.ProductID FROM dbo.Customers c CROSS APPLY (VALUES
 (CASE c.CustomerID % 5 WHEN 0 THEN 1 WHEN 1 THEN 1 WHEN 2 THEN 5 WHEN 3 THEN 2 ELSE 7 END),
 (CASE c.CustomerID % 4 WHEN 0 THEN 3 WHEN 1 THEN 9 WHEN 2 THEN 6 ELSE 4 END)) p(ProductID)
GROUP BY c.CustomerID,p.ProductID;
-- [DEMO-01] Ưu đãi bắt đầu từ 365 ngày trước để đơn lịch sử ở 07_demo.sql dùng được voucher.
INSERT dbo.LoyaltyPolicies(Name,AmountPerPoint,EffectiveFrom) VALUES(N'10.000 đồng đổi 1 điểm',10000,'2020-01-01');
INSERT dbo.Promotions(Name,Description,Scope,CreatedBy,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit) VALUES
(N'Đổi 50 điểm giảm 10.000',N'Đơn từ 50.000 đồng','Chain',@owner,'Amount',10000,NULL,50000,DATEADD(day,-365,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),50,1000,3),
(N'Quà tặng 10%',N'Đơn từ 30.000 đồng, giảm tối đa 20.000','Chain',@owner,'Percent',10,20000,30000,DATEADD(day,-365,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),NULL,1000,1),
(N'Chào mừng',N'Nhận miễn phí, giảm 5.000 đồng từ đơn 10.000','Chain',@owner,'Amount',5000,NULL,10000,DATEADD(day,-365,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),0,1000,1);
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

-- ===== Nhà cung cấp mẫu =====
INSERT dbo.Suppliers(Code,Name,Phone,Email,ProductGroup,IsActive) VALUES
('NCC-CF01',N'Công ty TNHH Cà phê Cầu Đất','0263 3812 456','kinhdoanh@caudatcoffee.test',N'Cà phê hạt Robusta, Arabica',1),
('NCC-SU01',N'Nhà phân phối Sữa Phương Nam','028 3896 1122','order@suaphuongnam.test',N'Sữa tươi, sữa đặc, kem béo',1),
('NCC-TR01',N'Trà Thái Nguyên Xanh','0208 3655 789','sales@trathainguyen.test',N'Trà lài, trà xanh, trà sen',1),
('NCC-TC01',N'Vựa trái cây Bốn Mùa','0909 456 123','bonmua.fruit@mail.test',N'Đào, vải, cam, chanh dây',1),
('NCC-BK01',N'Lò bánh Ngọc Lan','028 3512 7788','ngoclan.bakery@mail.test',N'Bánh croissant, tiramisu, mousse',1),
('NCC-BB01',N'Bao bì Minh Phát','028 3754 2299','minhphat.pack@mail.test',N'Ly nhựa, ống hút, túi giấy',0);
-- ===== Kho nguyên liệu: 16 nguyên liệu và phiếu nhập khởi tạo =====
-- [DEMO-01] Phiếu nhập định kỳ, xuất bán theo đơn, hủy hàng và kiểm kê được sinh ở 07_demo.sql theo đúng thời gian.
DECLARE @cf  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-CF01'),
        @su  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-SU01'),
        @tr  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-TR01'),
        @tc  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-TC01'),
        @bk  int = (SELECT SupplierID FROM dbo.Suppliers WHERE Code='NCC-BK01');

INSERT dbo.Ingredients(Code,Name,Unit,Quantity,MinStock,LastUnitCost,SupplierID,StockUpdatedAt) VALUES
-- Cà phê
('NL-CF01', N'Cà phê Robusta',         N'kg',    15.500, 5.000,  180000, @cf, SYSUTCDATETIME()),
('NL-CF02', N'Cà phê Arabica',          N'kg',     8.200, 3.000,  320000, @cf, SYSUTCDATETIME()),
-- Sữa
('NL-SU01', N'Sữa tươi không đường',    N'lít',   24.000, 10.000,  32000, @su, SYSUTCDATETIME()),
('NL-SU02', N'Sữa đặc có đường',        N'hộp',   48.000, 20.000,  22000, @su, SYSUTCDATETIME()),
('NL-SU03', N'Kem béo (whipping cream)',N'lít',    6.000,  3.000,  95000, @su, SYSUTCDATETIME()),
-- Trà
('NL-TR01', N'Trà lài khô',             N'kg',     3.500,  1.500, 280000, @tr, SYSUTCDATETIME()),
('NL-TR02', N'Trà xanh bột (matcha)',   N'kg',     1.200,  0.500, 650000, @tr, SYSUTCDATETIME()),
-- Trái cây
('NL-TC01', N'Đào tươi (peach)',        N'kg',     8.000,  3.000,  45000, @tc, SYSUTCDATETIME()),
('NL-TC02', N'Chanh tươi',              N'kg',     5.500,  2.000,  18000, @tc, SYSUTCDATETIME()),
('NL-TC03', N'Cam tươi',               N'kg',     4.000,  2.000,  22000, @tc, SYSUTCDATETIME()),
-- Bánh & nguyên liệu làm bánh
('NL-BK01', N'Bột mì đa dụng',         N'kg',    10.000,  5.000,  18000, @bk, SYSUTCDATETIME()),
('NL-BK02', N'Cream cheese',           N'kg',     3.000,  1.000, 320000, @bk, SYSUTCDATETIME()),
('NL-BK03', N'Đường cát trắng',        N'kg',    12.000,  5.000,  18000, NULL, SYSUTCDATETIME()),
-- Vật tư / bao bì (không có NCC tương ứng trong seed)
('NL-PP01', N'Ly nhựa 500ml',          N'cái',  350.000, 100.000,   800, NULL, SYSUTCDATETIME()),
('NL-PP02', N'Ống hút giấy',           N'cái',  500.000, 100.000,   200, NULL, SYSUTCDATETIME()),
('NL-PP03', N'Túi giấy take-away',     N'cái',  200.000,  50.000,  2500, NULL, SYSUTCDATETIME());

-- Ghi phiếu nhập kho mẫu cho từng nguyên liệu vừa thêm
-- (BalanceAfter = Quantity, Kind='Import', QuantityChange = Quantity)
INSERT dbo.InventoryTransactions(IngredientID,Kind,QuantityChange,BalanceAfter,UnitCost,Note,CreatedAt)
SELECT IngredientID,'Import',Quantity,Quantity,LastUnitCost,
       N'Nhập kho khởi tạo dữ liệu demo',
       DATEADD(day,-365,SYSUTCDATETIME())   -- [DEMO-01] mở kho 365 ngày trước, trước mọi đơn lịch sử
FROM dbo.Ingredients;

-- ===== Định mức nguyên liệu (size M / Standard; size L dùng gấp 1,3 lần, ly và ống hút giữ 1) =====
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
-- ===== Hạng thành viên (chi tiêu 12 tháng; quản lý sửa ở Quản lý CRM → Hạng thành viên) =====
INSERT dbo.MemberTiers(Code,Name,MinSpent,Benefit) VALUES
    ('Member', N'Thành viên',       0, N'Tích điểm cho mọi đơn hoàn tất, đổi điểm lấy voucher.'),
    ('Silver', N'Bạc',        1000000, N'Ưu tiên nhận voucher riêng hạng Bạc.'),
    ('Gold',   N'Vàng',       3000000, N'Voucher sinh nhật và ưu đãi riêng hạng Vàng.'),
    ('Diamond',N'Kim cương',  6000000, N'Ưu đãi cao nhất, được mời thử món mới đầu tiên.');
COMMIT;
GO
