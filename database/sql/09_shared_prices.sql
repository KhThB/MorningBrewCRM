-- Repeatable upgrade: shared catalog prices for all future orders/menu reads.
-- Retains existing availability rows and historical order snapshots. No data is deleted.
SET NOCOUNT ON; SET XACT_ABORT ON;
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
CREATE OR ALTER PROCEDURE dbo.SetBranchPrice
 @ActorID int,@BranchID int,@VariantID int,@Price decimal(18,2),@IsAvailable bit
AS
BEGIN
 SET NOCOUNT ON;
 THROW 51100,'Branch-specific prices are retired. Use the owner-only shared variant price API.',1;
END;
GO
