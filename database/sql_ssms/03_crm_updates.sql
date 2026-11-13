-- 03_crm_updates.sql – nhà cung cấp, giá chung, xóa mềm, chế độ một cửa hàng, sửa lỗi CRM (cài mới hoặc nâng cấp)
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
    RAISERROR(N'>>> DỪNG: không tìm thấy database [MorningBrewCRM_Crm].', 16, 1);
    SET NOEXEC ON;
END
IF OBJECT_ID(N'dbo.CreateOrder', N'P') IS NULL OR OBJECT_ID(N'dbo.GetMenu', N'P') IS NULL
BEGIN
    RAISERROR(N'>>> DỪNG: chưa có bảng/thủ tục lõi. Chạy 01_core.sql trước.', 16, 1);
    SET NOEXEC ON;
END
IF (SELECT COUNT(*) FROM dbo.Branches WHERE IsActive = 1) > 1
BEGIN
    RAISERROR(N'>>> DỪNG: bản cập nhật này chỉ dành cho mô hình MỘT cửa hàng.', 16, 1);
    SET NOEXEC ON;
END
IF OBJECT_ID(N'dbo.AreaManagers', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID = u.RoleID WHERE r.Code = 'AreaManager')
BEGIN
    RAISERROR(N'>>> DỪNG: còn tài khoản AreaManager. Chuyển các tài khoản này sang vai trò khác trước.', 16, 1);
    SET NOEXEC ON;
END
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- Sao lưu tự động trước khi cập nhật nếu database đã có dữ liệu (giống Upgrade.ps1).
-- Muốn bỏ qua: đổi @SaoLuu = 0.
DECLARE @SaoLuu bit = 1;
IF @SaoLuu = 1 AND EXISTS (SELECT 1 FROM dbo.Users)
BEGIN
    DECLARE @dir nvarchar(300) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(300));
    DECLARE @file nvarchar(400) = CONCAT(CASE WHEN @dir IS NULL THEN N'' WHEN RIGHT(@dir, 1) = N'\' THEN @dir ELSE @dir + N'\' END, N'MorningBrewCRM_Crm_truoc-cap-nhat_', FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmmss'), N'.bak');
    DECLARE @sql nvarchar(max) = N'BACKUP DATABASE [MorningBrewCRM_Crm] TO DISK = @f WITH COPY_ONLY, CHECKSUM, INIT; RESTORE VERIFYONLY FROM DISK = @f WITH CHECKSUM;';
    EXEC sp_executesql @sql, N'@f nvarchar(400)', @f = @file;
    PRINT CONCAT(N'Đã sao lưu và kiểm tra bản sao lưu: ', @file);
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
-- DB-TEST SOURCE: 08_admin.sql (historical filename)
-- Additive, repeatable migration for Admin features. No existing data is reset.
-- Phạm vi CRM: đã bỏ phần nhân sự (bảng StaffShifts và quyền Staff.Manage).
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;
IF OBJECT_ID('dbo.Suppliers','U') IS NULL
CREATE TABLE dbo.Suppliers(
 SupplierID int IDENTITY PRIMARY KEY,
 Code varchar(30) NOT NULL UNIQUE,
 Name nvarchar(150) NOT NULL,
 Phone varchar(20) NULL,
 Email nvarchar(254) NULL,
 ProductGroup nvarchar(150) NULL,
 IsActive bit NOT NULL DEFAULT 1
);
COMMIT;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- Trusted API service role: metadata CRUD and business procedures, without schema/DDL ownership.
IF DATABASE_PRINCIPAL_ID('MorningBrewApi') IS NULL CREATE ROLE MorningBrewApi;
GRANT SELECT,INSERT,UPDATE,DELETE,EXECUTE ON SCHEMA::dbo TO MorningBrewApi;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 09_shared_prices.sql (historical filename)
-- Repeatable upgrade: shared catalog prices for all future orders/menu reads.
-- Retains existing availability rows and historical order snapshots. No data is deleted.
SET NOCOUNT ON; SET XACT_ABORT ON;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
BEGIN TRAN;
DECLARE @sql nvarchar(max), @name sysname;
DECLARE procedures CURSOR LOCAL FAST_FORWARD FOR
 SELECT name FROM sys.procedures WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('CreateOrder','GetMenu');
OPEN procedures;
FETCH NEXT FROM procedures INTO @name;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @sql=OBJECT_DEFINITION(OBJECT_ID('dbo.'+@name));
 IF @sql IS NULL THROW 51100,'Install core procedures before 09_shared_prices.sql.',1;
 SET @sql='ALTER '+SUBSTRING(@sql,CHARINDEX('PROCEDURE',@sql),LEN(@sql));
 SET @sql=REPLACE(@sql,'COALESCE(bv.SellingPrice,v.BasePrice)','v.BasePrice');
 SET @sql=REPLACE(@sql,'COALESCE(bo.ExtraPrice,mo.ExtraPrice)','mo.ExtraPrice');
 SET @sql=REPLACE(@sql,'COALESCE(bo.ExtraPrice,o.ExtraPrice)','o.ExtraPrice');
 EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM procedures INTO @name;
END;
CLOSE procedures; DEALLOCATE procedures;
IF OBJECT_ID('dbo.CreateOrder','P') IS NULL OR OBJECT_ID('dbo.GetMenu','P') IS NULL
 THROW 51100,'Install core procedures before 09_shared_prices.sql.',1;
COMMIT;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER PROCEDURE dbo.SetBranchPrice
 @ActorID int,@BranchID int,@VariantID int,@Price decimal(18,2),@IsAvailable bit
AS
BEGIN
 SET NOCOUNT ON;
 THROW 51100,'Branch-specific prices are retired. Use the owner-only shared variant price API.',1;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 10_soft_delete.sql (historical filename)
-- LƯU Ý: dbo.SoftDeleteUser được 11_single_store.sql định nghĩa lại (cho quản lý xóa khách). Nếu chạy lại file này, hãy chạy lại 11 sau đó.
-- Xóa mềm tài khoản / khách hàng (yêu cầu 4.1 "Xóa khách hàng", II.2 "Thêm, xóa account").
-- Chạy lặp lại được. Không xóa dòng nào: chỉ đánh dấu DeletedAt, khóa đăng nhập và ẩn danh dữ liệu cá nhân,
-- để đơn hàng, điểm, phản hồi, khảo sát (khóa ngoại tới Users/Customers) và các báo cáo vẫn đúng.
-- Database đã tạo trước đó: sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\10_soft_delete.sql
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
IF COL_LENGTH('dbo.Users','DeletedAt') IS NULL
    ALTER TABLE dbo.Users ADD DeletedAt datetime2(3) NULL;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER PROCEDURE dbo.SoftDeleteUser @ActorID int,@UserID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRAN;
 IF dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Security.Manage')=0 THROW 51103,N'Chỉ chủ cửa hàng được xóa tài khoản.',1;
 IF @UserID=@ActorID THROW 51121,N'Không thể tự xóa tài khoản đang đăng nhập.',1;
 DECLARE @role varchar(30);
 SELECT @role=r.Code FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.UserID=@UserID AND u.DeletedAt IS NULL;
 IF @role IS NULL THROW 51104,N'Account not found.',1;
 IF @role='Owner' AND NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
    WHERE r.Code='Owner' AND u.UserID<>@UserID AND u.IsActive=1 AND u.DeletedAt IS NULL)
    THROW 51122,N'Phải còn ít nhất một chủ cửa hàng đang hoạt động.',1;
 -- Ẩn danh: email đổi thành giá trị không dùng được để email gốc có thể đăng ký lại.
 DECLARE @email nvarchar(254)=CONCAT(N'deleted-',@UserID,N'@deleted.invalid');
 UPDATE dbo.Users SET IsActive=0,DeletedAt=SYSUTCDATETIME(),Email=@email,NormalizedEmail=UPPER(@email),
    FullName=CASE WHEN @role='Customer' THEN N'Khách đã xóa' ELSE N'Tài khoản đã xóa' END,Phone=NULL
 WHERE UserID=@UserID;
 UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@UserID AND RevokedAt IS NULL;
 IF @role='BranchManager' UPDATE dbo.Branches SET ManagerID=NULL WHERE ManagerID=@UserID;
 INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@ActorID,'Account.SoftDelete','Users',@UserID,@role);
 COMMIT;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 11_single_store.sql (historical filename)
-- Chế độ MỘT CỬA HÀNG (phạm vi đồ án hiện tại). Chạy lặp lại được, không xóa dữ liệu nghiệp vụ.
-- Áp dụng sau 01–10. Database đã có:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\11_single_store.sql
-- Thay đổi:
--  1. CanScope: phạm vi chung (Chain) = cửa hàng duy nhất. Người có quyền tại một cửa hàng đang hoạt động
--     thì có quyền đó ở phạm vi chung -> quản lý thấy toàn bộ khách, phản hồi không gắn cửa hàng,
--     khảo sát/ưu đãi do chủ cửa hàng tạo.
--  2. CustomerInScope: mọi khách chưa bị xóa đều thuộc cửa hàng (không còn phụ thuộc đơn hàng Completed).
--  3. CanBranch: quản lý được gán qua Branches.ManagerID HOẶC StaffBranches -> có thể có nhiều quản lý.
--  4. Bỏ vai trò AreaManager (bảng AreaManagers, trigger, nhánh kiểm tra quyền) và procedure SetBranchPrice đã ngừng dùng.
--  5. Quyền Customers.Manage (thêm/xóa/khóa khách hàng) cho chủ cửa hàng và quản lý.
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER FUNCTION dbo.CanBranch(@UserID int,@BranchID int,@Permission varchar(80))
RETURNS bit AS
BEGIN
 DECLARE @ok bit=0;
 IF EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
  JOIN dbo.RolePermissions rp ON rp.RoleID=u.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID
  JOIN dbo.Branches b ON b.BranchID=@BranchID
  WHERE u.UserID=@UserID AND u.IsActive=1 AND p.Code=@Permission AND
  (r.Code='Owner'
   OR (r.Code='BranchManager' AND b.ManagerID=u.UserID)
   OR (r.Code IN ('BranchManager','Staff') AND EXISTS(SELECT 1 FROM dbo.StaffBranches s WHERE s.UserID=u.UserID AND s.BranchID=b.BranchID AND s.IsActive=1)))) SET @ok=1;
 RETURN @ok;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER FUNCTION dbo.CanScope(@UserID int,@Scope varchar(10),@AreaID int,@BranchID int,@Permission varchar(80))
RETURNS bit AS
BEGIN
 IF @Scope='Branch' RETURN dbo.CanBranch(@UserID,@BranchID,@Permission);
 -- Chain/Area: chủ cửa hàng có quyền, hoặc người có quyền tại một cửa hàng đang hoạt động (chỉ có 1 cửa hàng).
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
   JOIN dbo.RolePermissions rp ON rp.RoleID=r.RoleID JOIN dbo.Permissions p ON p.PermissionID=rp.PermissionID
   WHERE u.UserID=@UserID AND u.IsActive=1 AND p.Code=@Permission AND r.Code='Owner')
  OR EXISTS(SELECT 1 FROM dbo.Branches b WHERE b.IsActive=1 AND dbo.CanBranch(@UserID,b.BranchID,@Permission)=1)
  THEN 1 ELSE 0 END;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER FUNCTION dbo.CustomerInScope(@CustomerID int,@Scope varchar(10),@AreaID int,@BranchID int)
RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
  WHERE c.CustomerID=@CustomerID AND u.DeletedAt IS NULL) THEN 1 ELSE 0 END;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER TRIGGER dbo.TR_StaffBranches_Role ON dbo.StaffBranches AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Users u ON u.UserID=i.UserID JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE r.Code NOT IN ('Staff','BranchManager'))
  THROW 51001,N'Assignment requires Staff or BranchManager role.',1;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
CREATE OR ALTER TRIGGER dbo.TR_Users_Role ON dbo.Users AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF UPDATE(RoleID) AND EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.UserID=i.UserID WHERE i.RoleID<>d.RoleID AND
 (EXISTS(SELECT 1 FROM dbo.Customers WHERE UserID=i.UserID) OR EXISTS(SELECT 1 FROM dbo.StaffBranches WHERE UserID=i.UserID AND IsActive=1) OR EXISTS(SELECT 1 FROM dbo.Branches WHERE ManagerID=i.UserID)))
 THROW 51001,N'Remove incompatible assignments/profile before changing role.',1;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
BEGIN TRAN;
-- Quản lý hiện tại (Branches.ManagerID) cũng được ghi vào StaffBranches để thống nhất cách gán.
INSERT dbo.StaffBranches(UserID,BranchID)
SELECT b.ManagerID,b.BranchID FROM dbo.Branches b
WHERE b.ManagerID IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.StaffBranches s WHERE s.UserID=b.ManagerID AND s.BranchID=b.BranchID);
-- Quyền thêm/xóa/khóa khách hàng.
IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE Code='Customers.Manage')
 INSERT dbo.Permissions(Code,Description) VALUES('Customers.Manage',N'Thêm, xóa, khóa tài khoản khách hàng');
INSERT dbo.RolePermissions(RoleID,PermissionID)
SELECT r.RoleID,p.PermissionID FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.Code IN ('Owner','BranchManager') AND p.Code='Customers.Manage'
 AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions x WHERE x.RoleID=r.RoleID AND x.PermissionID=p.PermissionID);
COMMIT;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- Bỏ vai trò AreaManager (không có tài khoản nào dùng trong chế độ một cửa hàng).
IF OBJECT_ID('dbo.TR_AreaManagers_Role','TR') IS NOT NULL DROP TRIGGER dbo.TR_AreaManagers_Role;
IF OBJECT_ID('dbo.AreaManagers','U') IS NOT NULL
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.AreaManagers) THROW 51130,N'AreaManagers còn dữ liệu: chuyển các tài khoản này sang vai trò khác trước khi chạy script.',1;
 DROP TABLE dbo.AreaManagers;
END;
IF EXISTS(SELECT 1 FROM dbo.Roles WHERE Code='AreaManager') THROW 51130,N'Còn vai trò AreaManager trong dbo.Roles.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Roles_0' AND definition LIKE '%AreaManager%')
BEGIN
 ALTER TABLE dbo.Roles DROP CONSTRAINT CK_Roles_0;
 ALTER TABLE dbo.Roles ADD CONSTRAINT CK_Roles_0 CHECK (Code IN ('Owner','BranchManager','Staff','Customer'));
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- Giá riêng theo cửa hàng đã ngừng từ 09_shared_prices.sql.
DROP PROCEDURE IF EXISTS dbo.SetBranchPrice;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- Xóa mềm: thu hồi phân công cửa hàng; nếu xóa quản lý chính thì chuyển ManagerID cho quản lý khác còn hoạt động.
CREATE OR ALTER PROCEDURE dbo.SoftDeleteUser @ActorID int,@UserID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRAN;
 IF dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Security.Manage')=0
    AND NOT (dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Customers.Manage')=1
             AND EXISTS(SELECT 1 FROM dbo.Customers WHERE UserID=@UserID))
    THROW 51103,N'Không có quyền xóa tài khoản này.',1;
 IF @UserID=@ActorID THROW 51121,N'Không thể tự xóa tài khoản đang đăng nhập.',1;
 DECLARE @role varchar(30);
 SELECT @role=r.Code FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE u.UserID=@UserID AND u.DeletedAt IS NULL;
 IF @role IS NULL THROW 51104,N'Account not found.',1;
 IF @role='Owner' AND NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Roles r ON r.RoleID=u.RoleID
    WHERE r.Code='Owner' AND u.UserID<>@UserID AND u.IsActive=1 AND u.DeletedAt IS NULL)
    THROW 51122,N'Phải còn ít nhất một chủ cửa hàng đang hoạt động.',1;
 DECLARE @email nvarchar(254)=CONCAT(N'deleted-',@UserID,N'@deleted.invalid');
 UPDATE dbo.Users SET IsActive=0,DeletedAt=SYSUTCDATETIME(),Email=@email,NormalizedEmail=UPPER(@email),
    FullName=CASE WHEN @role='Customer' THEN N'Khách đã xóa' ELSE N'Tài khoản đã xóa' END,Phone=NULL
 WHERE UserID=@UserID;
 UPDATE dbo.UserSessions SET RevokedAt=SYSUTCDATETIME() WHERE UserID=@UserID AND RevokedAt IS NULL;
 UPDATE dbo.StaffBranches SET IsActive=0 WHERE UserID=@UserID;
 IF @role='BranchManager'
  UPDATE b SET ManagerID=(SELECT TOP(1) s.UserID FROM dbo.StaffBranches s JOIN dbo.Users u ON u.UserID=s.UserID
       JOIN dbo.Roles r ON r.RoleID=u.RoleID
       WHERE s.BranchID=b.BranchID AND s.IsActive=1 AND r.Code='BranchManager' AND u.IsActive=1 AND u.UserID<>@UserID ORDER BY s.AssignedAt)
  FROM dbo.Branches b WHERE b.ManagerID=@UserID;
 INSERT dbo.AuditLogs(ActorID,Action,EntityType,EntityID,Details) VALUES(@ActorID,'Account.SoftDelete','Users',@UserID,@role);
 COMMIT;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- DB-TEST SOURCE: 12_bugfix.sql (historical filename)
-- Đợt sửa lỗi theo docs/BugReport_MorningBrew_App-Admin.xlsx. Chạy lặp lại được, áp dụng sau 01–11.
-- Database đã có:
--   sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\12_bugfix.sql
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- [BUG-06] Tách lỗi "sai trạng thái" (51124 → HTTP 409, thông báo tiếng Việt) khỏi lỗi "thiếu quyền" (51103 → 403).
-- [BUG-07] Không cho phát hành khảo sát đã quá ngày kết thúc.
CREATE OR ALTER PROCEDURE dbo.PublishSurvey @ActorID int,@SurveyID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int,@ends datetime2(3);
 SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID,@ends=EndsAt FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@SurveyID;
 IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
 IF dbo.CanScope(@ActorID,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,N'Survey outside scope.',1;
 IF @status<>'Draft' THROW 51124,N'Khảo sát đã được phát hành hoặc đã đóng, không phát hành lại được.',1;
 IF @ends<=SYSUTCDATETIME() THROW 51124,N'Khảo sát đã quá ngày kết thúc, không thể phát hành.',1; -- [BUG-07]
 UPDATE dbo.Surveys SET Status='Open' WHERE SurveyID=@SurveyID;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID) VALUES(@ActorID,@branch,'Survey.Publish','Surveys',@SurveyID);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- [BUG-06] Mời vào khảo sát chưa phát hành / đã đóng / hết hạn → 51124 thay vì 51103.
CREATE OR ALTER PROCEDURE dbo.InviteSurvey @ActorID int,@SurveyID int,@CustomerID int
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @status varchar(10),@scope varchar(10),@area int,@branch int,@ends datetime2(3);
 SELECT @status=Status,@scope=Scope,@area=AreaID,@branch=BranchID,@ends=EndsAt FROM dbo.Surveys WITH(UPDLOCK,HOLDLOCK) WHERE SurveyID=@SurveyID;
 IF @status IS NULL THROW 51104,N'Không tìm thấy khảo sát.',1;
 IF dbo.CanScope(@ActorID,@scope,@area,@branch,'Surveys.Manage')=0 THROW 51103,N'Survey outside scope.',1;
 IF @status<>'Open' OR @ends<=SYSUTCDATETIME() THROW 51124,N'Khảo sát chưa phát hành, đã đóng hoặc đã hết hạn nên không mời thêm được.',1;
 IF dbo.CustomerInScope(@CustomerID,@scope,@area,@branch)=0 THROW 51104,N'Không tìm thấy khách hàng.',1;
 IF EXISTS(SELECT 1 FROM dbo.SurveyRecipients WHERE SurveyID=@SurveyID AND CustomerID=@CustomerID) BEGIN COMMIT; RETURN; END;
 INSERT dbo.SurveyRecipients(SurveyID,CustomerID) VALUES(@SurveyID,@CustomerID);
 INSERT dbo.Notifications(UserID,Title,Body,SurveyID) SELECT c.UserID,N'Khảo sát mới',s.Title,@SurveyID FROM dbo.Customers c CROSS JOIN dbo.Surveys s WHERE c.CustomerID=@CustomerID AND s.SurveyID=@SurveyID;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- [Mã đơn ngắn] Mã đơn cũ 'MB-' + GUID (35 ký tự) quá dài để đọc / gọi tại quầy.
-- Mã mới: MB + ngày đặt (giờ Việt Nam, yyMMdd) + '-' + số thứ tự đơn 4 chữ số, ví dụ MB261108-0028 (13 ký tự).
-- Duy nhất vì gắn với OrderID. Trigger chạy sau khi CreateOrder thêm đơn nên không phải sửa procedure.
CREATE OR ALTER TRIGGER dbo.TR_Orders_ShortCode ON dbo.Orders AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 UPDATE o SET OrderCode=CONCAT('MB',FORMAT(DATEADD(hour,7,i.CreatedAt),'yyMMdd'),'-',
   CASE WHEN i.OrderID>9999 THEN CONVERT(varchar(12),i.OrderID) ELSE RIGHT(CONCAT('0000',i.OrderID),4) END)
 FROM dbo.Orders o JOIN inserted i ON i.OrderID=o.OrderID;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- Đổi mã các đơn đã có sang dạng ngắn (chạy lại không đổi gì vì chỉ áp dụng cho mã dạng cũ 'MB-…').
UPDATE dbo.Orders SET OrderCode=CONCAT('MB',FORMAT(DATEADD(hour,7,CreatedAt),'yyMMdd'),'-',
  CASE WHEN OrderID>9999 THEN CONVERT(varchar(12),OrderID) ELSE RIGHT(CONCAT('0000',OrderID),4) END)
WHERE OrderCode LIKE 'MB-%';
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
-- [BUG-09] Không cho sửa phản hồi đã giải quyết (trước đây trả lời lại sẽ ghi đè câu trả lời cũ).
CREATE OR ALTER PROCEDURE dbo.HandleFeedback @ActorID int,@FeedbackID int,@Status varchar(12),@Response nvarchar(4000)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @branch int,@customer int,@old varchar(12);
 SELECT @branch=BranchID,@customer=CustomerID,@old=Status FROM dbo.Feedbacks WITH(UPDLOCK,HOLDLOCK) WHERE FeedbackID=@FeedbackID;
 IF @customer IS NULL OR (@branch IS NULL AND dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Feedbacks.Manage')=0) OR (@branch IS NOT NULL AND dbo.CanBranch(@ActorID,@branch,'Feedbacks.Manage')=0) THROW 51103,N'Feedback is outside scope.',1;
 -- [BUG-09] Phản hồi đã giải quyết thì khóa: không đổi trạng thái, không ghi đè câu trả lời đã gửi khách.
 IF @old='Resolved' THROW 51124,N'Phản hồi đã được giải quyết, không thể sửa trạng thái hoặc câu trả lời.',1;
 IF @Status NOT IN ('Processing','Resolved') OR (@Status='Resolved' AND NULLIF(@Response,'') IS NULL) THROW 51100,N'Resolved feedback needs a response.',1;
 UPDATE dbo.Feedbacks SET Status=@Status,HandledBy=@ActorID,ResponseContent=@Response,HandledAt=SYSUTCDATETIME() WHERE FeedbackID=@FeedbackID;
 INSERT dbo.Notifications(UserID,Title,Body) SELECT UserID,N'Cập nhật phản hồi',COALESCE(@Response,N'Phản hồi đang được xử lý.') FROM dbo.Customers WHERE CustomerID=@customer;
 INSERT dbo.AuditLogs(ActorID,BranchID,Action,EntityType,EntityID,Details) VALUES(@ActorID,@branch,'Feedback.Handle','Feedbacks',@FeedbackID,@Status);
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF @@ERROR <> 0
BEGIN
    RAISERROR(N'>>> DỪNG: khối lệnh phía trên bị lỗi. Các lệnh còn lại trong file KHÔNG được chạy. Xem dòng lỗi màu đỏ phía trên.', 16, 1);
    SET NOEXEC ON;
END
GO
PRINT N'=== HOÀN TẤT 03_crm_updates.sql trên database [MorningBrewCRM_Crm]. Tiếp theo: 04_menu_updates.sql.';
GO
SET NOEXEC OFF;
GO
