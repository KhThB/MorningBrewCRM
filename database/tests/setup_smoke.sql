-- Read-only checks for a NEW database installed with the full demo seed.
-- Do not use seed-count assertions as checks against a database with user data.
SET NOCOUNT ON;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>41
 THROW 51000,'Expected 41 user tables.',1;
IF (SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id WHERE t.is_ms_shipped=0)<>318
 THROW 51000,'Expected 318 table columns.',1;
IF OBJECT_ID('dbo.AreaManagers') IS NOT NULL OR OBJECT_ID('dbo.SetBranchPrice') IS NOT NULL
 THROW 51000,'Retired area-manager/branch-price objects remain.',1;
IF COL_LENGTH('dbo.Users','DeletedAt') IS NULL OR OBJECT_ID('dbo.SoftDeleteUser','P') IS NULL
 THROW 51000,'Soft-delete migration missing.',1;
IF OBJECT_ID('dbo.TR_Orders_ShortCode','TR') IS NULL
 THROW 51000,'Order-code migration missing.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 THROW 51000,'Disabled or untrusted data constraints.',1;
IF (SELECT COUNT(*) FROM dbo.Roles)<>4 OR EXISTS(SELECT 1 FROM dbo.Roles WHERE Code='AreaManager')
 THROW 51000,'Expected four single-store roles.',1;
IF (SELECT COUNT(*) FROM dbo.Branches)<>1 OR (SELECT COUNT(*) FROM dbo.Customers)<>30
 THROW 51000,'Single-store customer seed missing.',1;
IF (SELECT COUNT(*) FROM dbo.Products)<>10 OR (SELECT COUNT(*) FROM dbo.ProductVariants)<>17
 THROW 51000,'Expected 10 products / 17 variants after menu migration.',1;
IF (SELECT COUNT(*) FROM dbo.Suppliers)<>6
 THROW 51000,'Expected six demo suppliers.',1;
IF EXISTS(SELECT 1 FROM dbo.ModifierGroups WHERE Kind IN ('Sugar','Topping') AND IsActive=1)
 THROW 51000,'Retired modifier groups are still active.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.ModifierGroups WHERE Kind='Ice' AND Name=N'Nhiệt độ' AND IsActive=1)
 THROW 51000,'Temperature group missing.',1;
IF (SELECT COUNT(*) FROM dbo.ModifierOptions o JOIN dbo.ModifierGroups g ON g.ModifierGroupID=o.ModifierGroupID
 WHERE g.Kind='Ice' AND o.IsActive=1 AND o.Name IN (N'Đá',N'Nóng'))<>2
 THROW 51000,'Temperature options missing.',1;
IF EXISTS(SELECT 1 FROM dbo.LoyaltyReconciliation WHERE Difference<>0)
 THROW 51000,'Loyalty cache/ledger mismatch.',1;
IF DATABASE_PRINCIPAL_ID('MorningBrewApi') IS NULL OR DATABASE_PRINCIPAL_ID('MorningBrewRuntime') IS NULL
 THROW 51000,'Database roles missing.',1;
SELECT 'PASS: schema, migrations, seed, constraints and loyalty reconciliation' AS SetupSmoke;
