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
-- Trusted API service role: metadata CRUD and business procedures, without schema/DDL ownership.
IF DATABASE_PRINCIPAL_ID('MorningBrewApi') IS NULL CREATE ROLE MorningBrewApi;
GRANT SELECT,INSERT,UPDATE,DELETE,EXECUTE ON SCHEMA::dbo TO MorningBrewApi;
GO
