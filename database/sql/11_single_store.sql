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
CREATE OR ALTER FUNCTION dbo.CustomerInScope(@CustomerID int,@Scope varchar(10),@AreaID int,@BranchID int)
RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID
  WHERE c.CustomerID=@CustomerID AND u.DeletedAt IS NULL) THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_StaffBranches_Role ON dbo.StaffBranches AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Users u ON u.UserID=i.UserID JOIN dbo.Roles r ON r.RoleID=u.RoleID WHERE r.Code NOT IN ('Staff','BranchManager'))
  THROW 51001,N'Assignment requires Staff or BranchManager role.',1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_Users_Role ON dbo.Users AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF UPDATE(RoleID) AND EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.UserID=i.UserID WHERE i.RoleID<>d.RoleID AND
 (EXISTS(SELECT 1 FROM dbo.Customers WHERE UserID=i.UserID) OR EXISTS(SELECT 1 FROM dbo.StaffBranches WHERE UserID=i.UserID AND IsActive=1) OR EXISTS(SELECT 1 FROM dbo.Branches WHERE ManagerID=i.UserID)))
 THROW 51001,N'Remove incompatible assignments/profile before changing role.',1;
END;
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
-- Giá riêng theo cửa hàng đã ngừng từ 09_shared_prices.sql.
DROP PROCEDURE IF EXISTS dbo.SetBranchPrice;
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
