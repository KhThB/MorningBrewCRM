-- Additive, repeatable migration for Admin branch features. No existing data is reset.
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
IF OBJECT_ID('dbo.StaffShifts','U') IS NULL
BEGIN
 CREATE TABLE dbo.StaffShifts(
  ShiftID int IDENTITY PRIMARY KEY,
  UserID int NOT NULL REFERENCES dbo.Users(UserID),
  BranchID int NOT NULL REFERENCES dbo.Branches(BranchID),
  StartsAt datetime2(3) NOT NULL,
  EndsAt datetime2(3) NOT NULL,
  CreatedBy int NOT NULL REFERENCES dbo.Users(UserID),
  IsCancelled bit NOT NULL DEFAULT 0,
  CONSTRAINT CK_StaffShifts_Time CHECK(EndsAt>StartsAt AND DATEDIFF(hour,StartsAt,EndsAt)<=24)
 );
 CREATE INDEX IX_StaffShifts_User_Time ON dbo.StaffShifts(UserID,StartsAt,EndsAt);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WHERE Code='Staff.Manage')
BEGIN
 INSERT dbo.Permissions(Code,Description) VALUES('Staff.Manage',N'Quản lý ca làm trong phạm vi chi nhánh');
 INSERT dbo.RolePermissions(RoleID,PermissionID)
 SELECT r.RoleID,p.PermissionID FROM dbo.Roles r CROSS JOIN dbo.Permissions p
 WHERE r.Code IN ('Owner','AreaManager','BranchManager') AND p.Code='Staff.Manage';
END;
COMMIT;
GO
-- Trusted API service role: metadata CRUD and business procedures, without schema/DDL ownership.
IF DATABASE_PRINCIPAL_ID('MorningBrewApi') IS NULL CREATE ROLE MorningBrewApi;
GRANT SELECT,INSERT,UPDATE,DELETE,EXECUTE ON SCHEMA::dbo TO MorningBrewApi;
GO
