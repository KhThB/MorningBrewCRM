-- LƯU Ý: dbo.SoftDeleteUser được 11_single_store.sql định nghĩa lại (cho quản lý xóa khách). Nếu chạy lại file này, hãy chạy lại 11 sau đó.
-- Xóa mềm tài khoản / khách hàng (yêu cầu 4.1 "Xóa khách hàng", II.2 "Thêm, xóa account").
-- Chạy lặp lại được. Không xóa dòng nào: chỉ đánh dấu DeletedAt, khóa đăng nhập và ẩn danh dữ liệu cá nhân,
-- để đơn hàng, điểm, phản hồi, khảo sát (khóa ngoại tới Users/Customers) và các báo cáo vẫn đúng.
-- Database đã tạo trước đó: sqlcmd -S "(localdb)\MSSQLLocalDB" -d MorningBrewCRM_Crm -E -C -I -b -f 65001 -i database\sql\10_soft_delete.sql
SET NOCOUNT ON; SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON;
IF COL_LENGTH('dbo.Users','DeletedAt') IS NULL
    ALTER TABLE dbo.Users ADD DeletedAt datetime2(3) NULL;
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
