"""Generate deterministic demo records. The public demo password is not for production."""
from pathlib import Path
import base64,hashlib,struct
ROOT=Path(__file__).resolve().parents[1]
salt=hashlib.sha256(b'MorningBrew-demo-only').digest()[:16]
password='MorningBrew!2026'
hashed=base64.b64encode(b'\x01'+struct.pack('>III',2,100000,len(salt))+salt+hashlib.pbkdf2_hmac('sha512',password.encode(),salt,100000,32)).decode()
sql=f"""-- Demo password for all accounts: MorningBrew!2026. ASP.NET PasswordHasher V3, SHA512.
SET NOCOUNT ON; SET XACT_ABORT ON;
IF EXISTS(SELECT 1 FROM dbo.Roles) THROW 51000,N'Seed requires empty tables.',1;
BEGIN TRAN;
INSERT dbo.Roles(Code,Name) VALUES('Owner',N'Chủ doanh nghiệp'),('AreaManager',N'Quản lý khu vực'),('BranchManager',N'Quản lý chi nhánh'),('Staff',N'Nhân viên'),('Customer',N'Khách hàng');
INSERT dbo.Permissions(Code,Description) VALUES
('Orders.Create',N'Tạo đơn tại POS'),('Orders.Manage',N'Cập nhật xử lý đơn'),('Orders.Cancel',N'Hủy đơn sau xác nhận'),('Orders.Read',N'Xem lịch sử đơn'),
('Payments.Manage',N'Ghi nhận tiền đã thu'),('Refunds.Manage',N'Xử lý hoàn tiền'),('Menu.Manage',N'Quản lý giá tại chi nhánh'),('Customers.Lookup',N'Tra cứu thành viên bằng số điện thoại'),('Customers.Read',N'Phân nhóm khách trong phạm vi'),('Promotions.Manage',N'Quản lý và cấp ưu đãi'),('Surveys.Manage',N'Quản lý khảo sát'),('Feedbacks.Manage',N'Xử lý phản hồi'),('Reports.Read',N'Xem báo cáo'),('Organization.Manage',N'Quản lý tổ chức'),('Security.Manage',N'Quản lý tài khoản và quyền'),('Catalog.Manage',N'Quản lý danh mục dùng chung');
INSERT dbo.RolePermissions SELECT r.RoleID,p.PermissionID FROM dbo.Roles r CROSS JOIN dbo.Permissions p WHERE r.Code='Owner' OR (r.Code IN ('AreaManager','BranchManager') AND p.Code NOT IN ('Organization.Manage','Security.Manage','Catalog.Manage')) OR (r.Code='Staff' AND p.Code IN ('Orders.Create','Orders.Manage','Orders.Read','Payments.Manage','Customers.Lookup'));
INSERT dbo.Users(RoleID,Email,NormalizedEmail,PasswordHash,FullName,Phone) VALUES
(1,'owner@morningbrew.test','OWNER@MORNINGBREW.TEST','{hashed}',N'Chủ chuỗi','0900000001'),
(2,'area.east@morningbrew.test','AREA.EAST@MORNINGBREW.TEST','{hashed}',N'Quản lý khu Đông','0900000002'),
(3,'manager.1@morningbrew.test','MANAGER.1@MORNINGBREW.TEST','{hashed}',N'Quản lý quán 1','0900000003'),
(4,'staff.1@morningbrew.test','STAFF.1@MORNINGBREW.TEST','{hashed}',N'Thu ngân quán 1','0900000004'),
(5,'lan@morningbrew.test','LAN@MORNINGBREW.TEST','{hashed}',N'Nguyễn Lan','0901234567'),
(5,'minh@morningbrew.test','MINH@MORNINGBREW.TEST','{hashed}',N'Trần Minh','0907654321'),
(3,'manager.2@morningbrew.test','MANAGER.2@MORNINGBREW.TEST','{hashed}',N'Quản lý quán 2','0900000007'),
(4,'staff.2@morningbrew.test','STAFF.2@MORNINGBREW.TEST','{hashed}',N'Thu ngân quán 2','0900000008'),
(2,'area.west@morningbrew.test','AREA.WEST@MORNINGBREW.TEST','{hashed}',N'Quản lý khu Tây','0900000009'),
(3,'manager.3@morningbrew.test','MANAGER.3@MORNINGBREW.TEST','{hashed}',N'Quản lý quán 3','0900000010'),
(4,'staff.3@morningbrew.test','STAFF.3@MORNINGBREW.TEST','{hashed}',N'Thu ngân quán 3','0900000011');
INSERT dbo.Areas(Name) VALUES(N'Khu vực Đông'),(N'Khu vực Tây');
-- Demo shops open all day so exercises work at any local time. Production should set real hours.
INSERT dbo.Branches(AreaID,ManagerID,Code,Name,Address,Phone,OpeningTime,ClosingTime,DeliveryFee) VALUES
(1,3,'MB01',N'Morning Brew Thủ Đức',N'12 Võ Văn Ngân, TP.HCM','0281111111','00:00','23:59:59',15000),
(1,7,'MB02',N'Morning Brew Bình Thạnh',N'20 Điện Biên Phủ, TP.HCM','0282222222','00:00','23:59:59',18000),
(2,10,'MB03',N'Morning Brew Bình Tân',N'30 Tên Lửa, TP.HCM','0283333333','00:00','23:59:59',20000);
INSERT dbo.AreaManagers(AreaID,UserID) VALUES(1,2),(2,9);
INSERT dbo.StaffBranches(UserID,BranchID) VALUES(4,1),(8,2),(11,3);
INSERT dbo.Customers(UserID,DateOfBirth,Gender,LoyaltyPoints) VALUES(5,'2002-05-12','Female',100),(6,'1998-10-10','Male',100);
INSERT dbo.LoyaltyTransactions(CustomerID,Kind,Points,RequestKey,Note) VALUES(1,'Adjust',100,NEWID(),N'Điểm tặng khởi tạo dữ liệu demo'),(2,'Adjust',100,NEWID(),N'Điểm tặng khởi tạo dữ liệu demo');
INSERT dbo.CustomerAddresses(CustomerID,Label,RecipientName,Phone,AddressText,IsDefault) VALUES(1,N'Nhà',N'Nguyễn Lan','0901234567',N'12 đường A, TP.HCM',1),(2,N'Công ty',N'Trần Minh','0907654321',N'20 đường B, TP.HCM',1);
INSERT dbo.Categories(Name) VALUES(N'Cà phê'),(N'Trà'),(N'Bánh');
INSERT dbo.Products(CategoryID,Name,Description,ImageURL) VALUES(1,N'Cà phê sữa',N'Cà phê và sữa đặc','/images/coffee.jpg'),(2,N'Trà đào',N'Trà đào thơm mát','/images/tea.jpg'),(3,N'Bánh tiramisu',N'Bánh vị cà phê','/images/cake.jpg');
INSERT dbo.ProductVariants(ProductID,Name,BasePrice,DisplayOrder) VALUES(1,'M',30000,1),(1,'L',40000,2),(2,'M',35000,1),(3,'Standard',45000,1);
INSERT dbo.BranchProducts(BranchID,ProductID) SELECT b.BranchID,p.ProductID FROM dbo.Branches b CROSS JOIN dbo.Products p;
INSERT dbo.BranchProductVariants(BranchID,VariantID,ProductID,SellingPrice) VALUES(1,1,1,32000),(2,1,1,34000);
INSERT dbo.ModifierGroups(Name,Kind) VALUES(N'Đường','Sugar'),(N'Đá','Ice'),(N'Topping','Topping');
INSERT dbo.ModifierOptions(ModifierGroupID,Name,ExtraPrice,DisplayOrder) VALUES(1,N'50% đường',0,1),(1,N'100% đường',0,2),(2,N'50% đá',0,1),(2,N'100% đá',0,2),(3,N'Trân châu',5000,1),(3,N'Pudding',7000,2);
INSERT dbo.ProductModifierGroups(ProductID,ModifierGroupID,MinSelections,MaxSelections) VALUES(1,1,1,1),(1,2,1,1),(1,3,0,2),(2,1,1,1),(2,2,1,1),(2,3,0,2);
INSERT dbo.BranchModifierOptions(BranchID,ModifierOptionID,ExtraPrice,IsAvailable) VALUES(2,5,6000,1),(3,6,7000,0);
INSERT dbo.CustomerPreferences(CustomerID,ProductID) VALUES(1,1),(1,3),(2,2);
INSERT dbo.LoyaltyPolicies(Name,AmountPerPoint,EffectiveFrom) VALUES(N'10.000 đồng đổi 1 điểm',10000,'2020-01-01');
INSERT dbo.Promotions(Name,Description,Scope,CreatedBy,DiscountType,DiscountValue,MaxDiscount,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit) VALUES
(N'Đổi 50 điểm giảm 10.000',N'Đơn từ 50.000 đồng','Chain',1,'Amount',10000,NULL,50000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),50,1000,3),
(N'Quà tặng 10%',N'Đơn từ 30.000 đồng, giảm tối đa 20.000','Chain',1,'Percent',10,20000,30000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),NULL,1000,1),
(N'Chào mừng',N'Nhận miễn phí, giảm 5.000 đồng từ đơn 10.000','Chain',1,'Amount',5000,NULL,10000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()),0,1000,1);
INSERT dbo.Surveys(Title,Description,CreatedBy,Scope,StartsAt,EndsAt) VALUES(N'Khẩu vị và trải nghiệm',N'Khảo sát cải thiện sản phẩm',1,'Chain',DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(year,1,SYSUTCDATETIME()));
INSERT dbo.SurveyQuestions(SurveyID,Text,Type,IsRequired,DisplayOrder,RatingMin,RatingMax) VALUES(1,N'Bạn thích những vị nào?','MultipleChoice',1,1,NULL,NULL),(1,N'Bạn hài lòng mức nào?','Rating',1,2,1,5),(1,N'Góp ý thêm','Text',0,3,NULL,NULL);
INSERT dbo.SurveyQuestionOptions(QuestionID,Text,DisplayOrder) VALUES(1,N'Cà phê',1),(1,N'Socola',2),(1,N'Trà',3);
COMMIT;
GO
"""
(ROOT/'sql/06_seed.sql').write_text(sql,encoding='utf-8')
print('Generated seed; public demo password: MorningBrew!2026')
